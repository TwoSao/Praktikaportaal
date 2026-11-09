using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Core.Services;

public class ApplicationService : IApplicationService
{
    private readonly IApplicationRepository _applications;
    private readonly IInternshipRepository _internships;
    private readonly IStudentProfileRepository _students;
    private readonly IUnitOfWork _unitOfWork;

    public ApplicationService(
        IApplicationRepository applications,
        IInternshipRepository internships,
        IStudentProfileRepository students,
        IUnitOfWork unitOfWork)
    {
        _applications = applications;
        _internships = internships;
        _students = students;
        _unitOfWork = unitOfWork;
    }

    public async Task<(bool Success, string? Error, ApplicationDto? Application)> ApplyAsync(
        Guid studentProfileId,
        CreateApplicationDto dto,
        CancellationToken ct = default)
    {
        var internship = await _internships.GetByIdAsync(dto.InternshipId, ct);

        if (internship == null)
            return (false, "Internship listing not found.", null);

        if (internship.Status != InternshipStatus.Published)
            return (false, "This internship is not currently accepting applications.", null);

        if (internship.ApplicationDeadline.HasValue && internship.ApplicationDeadline.Value < DateTime.UtcNow)
            return (false, "The application deadline for this internship has passed.", null);

        // Business rule: one active (non-withdrawn) application per student per internship.
        if (await _applications.HasActiveApplicationAsync(studentProfileId, dto.InternshipId, ct))
            return (false, "You already have an active application for this internship.", null);

        var studentProfile = await _students.GetByIdAsync(studentProfileId, ct);
        if (studentProfile == null)
            return (false, "Student profile not found.", null);

        var application = new Application
        {
            StudentProfileId = studentProfileId,
            StudentProfile = studentProfile,
            InternshipId = internship.Id,
            Internship = internship,
            CoverLetter = dto.CoverLetter?.Trim(),
            Status = ApplicationStatus.Submitted,
            AppliedAt = DateTime.UtcNow
        };

        await _applications.AddAsync(application, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return (true, null, MapToDto(application));
    }

    public async Task<IReadOnlyList<ApplicationDto>> GetStudentApplicationsAsync(
        Guid studentProfileId,
        CancellationToken ct = default)
    {
        var list = await _applications.GetByStudentAsync(studentProfileId, ct);
        return list.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<ApplicationDto>> GetInternshipApplicationsAsync(
        Guid internshipId,
        Guid companyProfileId,
        CancellationToken ct = default)
    {
        if (!await _internships.IsOwnedByCompanyAsync(internshipId, companyProfileId, ct))
            return [];

        var list = await _applications.GetByInternshipAsync(internshipId, ct);
        return list.Select(MapToDto).ToList();
    }

    public async Task<bool> UpdateApplicationStatusAsync(
        Guid applicationId,
        Guid companyProfileId,
        ApplicationStatus newStatus,
        CancellationToken ct = default)
    {
        var application = await _applications.GetByIdAsync(applicationId, ct);

        if (application == null)
            return false;

        // Only the company that owns the internship may change the status.
        if (application.Internship.CompanyProfileId != companyProfileId)
            return false;

        application.Status = newStatus;
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> WithdrawApplicationAsync(
        Guid applicationId,
        Guid studentProfileId,
        CancellationToken ct = default)
    {
        var application = await _applications.GetByIdAsync(applicationId, ct);

        if (application == null || application.StudentProfileId != studentProfileId)
            return false;

        application.Status = ApplicationStatus.Withdrawn;
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private static ApplicationDto MapToDto(Application a) => new()
    {
        Id = a.Id,
        InternshipId = a.InternshipId,
        InternshipTitle = a.Internship.Title,
        CompanyName = a.Internship.CompanyProfile.CompanyName,
        StudentProfileId = a.StudentProfileId,
        StudentName = $"{a.StudentProfile.User.FirstName} {a.StudentProfile.User.LastName}".Trim(),
        StudentEmail = a.StudentProfile.User.Email,
        StudentPhoneNumber = a.StudentProfile.PhoneNumber,
        SchoolName = a.StudentProfile.SchoolName,
        StudyProgram = a.StudentProfile.StudyProgram,
        Course = a.StudentProfile.Course,
        CoverLetter = a.CoverLetter,
        Status = a.Status,
        AppliedAt = a.AppliedAt
    };
}
