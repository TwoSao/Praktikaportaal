using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Exceptions;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Core.Services;

public class CvService : ICvService
{
    private readonly ICvRepository _cvs;
    private readonly IStudentProfileRepository _students;
    private readonly IApplicationRepository _applications;
    private readonly IFileStorage _storage;
    private readonly IUnitOfWork _unitOfWork;

    public CvService(
        ICvRepository cvs,
        IStudentProfileRepository students,
        IApplicationRepository applications,
        IFileStorage storage,
        IUnitOfWork unitOfWork)
    {
        _cvs = cvs;
        _students = students;
        _applications = applications;
        _storage = storage;
        _unitOfWork = unitOfWork;
    }

    public async Task<CvDto> UploadAsync(
        Guid studentProfileId,
        string? fileName,
        string? contentType,
        long length,
        Stream content,
        CancellationToken ct = default)
    {
        _ = await _students.GetByIdAsync(studentProfileId, ct)
            ?? throw new NotFoundException("Student profile", studentProfileId);

        var type = CvFileRules.Validate(fileName, contentType, length);

        // The signature check needs to rewind, so make sure the stream is seekable (size already capped above).
        if (!content.CanSeek)
        {
            var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            content = buffer;
        }

        var header = new byte[CvFileRules.SignatureLength];
        int read = await content.ReadAsync(header, ct);
        content.Position = 0;
        if (!CvFileRules.HasValidSignature(type, header.AsSpan(0, read)))
            throw new BusinessRuleException($"The file content is not a valid {type.Extension} document.");

        string storageKey = await _storage.SaveAsync(content, type.Extension, ct);

        var previous = await _cvs.GetByStudentAsync(studentProfileId, ct);
        foreach (var old in previous)
            old.IsDeleted = true;

        var cv = new CV
        {
            StudentProfileId = studentProfileId,
            FileName = CvFileRules.SanitizeFileName(fileName, type),
            StorageKey = storageKey,
            ContentType = type.ContentType,
            UploadedAt = DateTime.UtcNow
        };
        await _cvs.AddAsync(cv, ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            await _storage.DeleteAsync(storageKey, CancellationToken.None); // don't leave an orphan file
            throw;
        }

        // The database now points to the new file; remove the replaced files.
        foreach (var old in previous)
            await _storage.DeleteAsync(old.StorageKey, CancellationToken.None);

        return MapToDto(cv);
    }

    public async Task<CvDto> GetMyCvAsync(Guid studentProfileId, CancellationToken ct = default) =>
        MapToDto(await GetCurrentCvAsync(studentProfileId, ct));

    public async Task<CvFile> DownloadMyCvAsync(Guid studentProfileId, CancellationToken ct = default) =>
        Open(await GetCurrentCvAsync(studentProfileId, ct));

    public async Task DeleteMyCvAsync(Guid studentProfileId, CancellationToken ct = default)
    {
        var cvs = await _cvs.GetByStudentAsync(studentProfileId, ct);
        if (cvs.Count == 0)
            throw new NotFoundException("CV", studentProfileId);

        foreach (var cv in cvs)
            cv.IsDeleted = true;

        await _unitOfWork.SaveChangesAsync(ct);

        foreach (var cv in cvs)
            await _storage.DeleteAsync(cv.StorageKey, CancellationToken.None);
    }

    public async Task<CvFile> DownloadForApplicationAsync(Guid applicationId, CurrentUser caller, CancellationToken ct = default)
    {
        var application = await _applications.GetByIdAsync(applicationId, ct)
            ?? throw new NotFoundException("Application", applicationId);

        bool isOwner = caller.Role == UserRole.Company && caller.ProfileId == application.Internship.CompanyProfileId;
        if (caller.Role != UserRole.Admin && !isOwner)
            throw new ForbiddenException("Only the company that received the application can download the CV.");

        if (application.Status == ApplicationStatus.Withdrawn)
            throw new ForbiddenException("The student has withdrawn this application.");

        return Open(await GetCurrentCvAsync(application.StudentProfileId, ct));
    }

    private async Task<CV> GetCurrentCvAsync(Guid studentProfileId, CancellationToken ct)
    {
        var cvs = await _cvs.GetByStudentAsync(studentProfileId, ct);
        return cvs.FirstOrDefault() ?? throw new NotFoundException("The student has not uploaded a CV.");
    }

    private CvFile Open(CV cv)
    {
        try
        {
            return new CvFile(_storage.OpenRead(cv.StorageKey), cv.FileName, cv.ContentType);
        }
        catch (FileNotFoundException)
        {
            throw new NotFoundException("The CV file is no longer available. Please upload it again.");
        }
    }

    private static CvDto MapToDto(CV cv) => new()
    {
        Id = cv.Id,
        FileName = cv.FileName,
        ContentType = cv.ContentType,
        UploadedAt = cv.UploadedAt
    };
}
