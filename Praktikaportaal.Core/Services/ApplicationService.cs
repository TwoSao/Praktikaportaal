using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Exceptions;
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

    public async Task<ApplicationDto> ApplyAsync(
        Guid studentProfileId,
        Guid internshipId,
        CreateApplicationDto dto,
        CancellationToken ct = default)
    {
        var internship = await _internships.GetByIdAsync(internshipId, ct)
            ?? throw new NotFoundException("Internship", internshipId);

        if (internship.Status != InternshipStatus.Published)
            throw new BusinessRuleException("This internship is not currently accepting applications.");

        if (internship.ApplicationDeadline.HasValue && internship.ApplicationDeadline.Value < DateTime.UtcNow)
            throw new BusinessRuleException("The application deadline for this internship has passed.");

        // Business rule: one active (non-withdrawn) application per student per internship.
        if (await _applications.HasActiveApplicationAsync(studentProfileId, internshipId, ct))
            throw new ConflictException("You already have an active application for this internship.");

        var studentProfile = await _students.GetByIdAsync(studentProfileId, ct)
            ?? throw new NotFoundException("Student profile", studentProfileId);

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

        return MapToDto(application);
    }

    public async Task<ApplicationDto> GetByIdAsync(Guid applicationId, CurrentUser caller, CancellationToken ct = default)
    {
        var application = await _applications.GetByIdAsync(applicationId, ct)
            ?? throw new NotFoundException("Application", applicationId);

        bool allowed = caller.Role switch
        {
            UserRole.Admin => true,
            UserRole.Student => application.StudentProfileId == caller.ProfileId,
            UserRole.Company => application.Internship.CompanyProfileId == caller.ProfileId,
            _ => false
        };

        if (!allowed)
            throw new ForbiddenException();

        return MapToDto(application);
    }

    public async Task<PagedResult<ApplicationDto>> GetStudentApplicationsAsync(
        Guid studentProfileId,
        ApplicationQueryParameters query,
        CancellationToken ct = default)
    {
        var (items, total) = await _applications.GetByStudentAsync(studentProfileId, query, ct);
        return ToPage(items, total, query);
    }

    public async Task<PagedResult<ApplicationDto>> GetInternshipApplicationsAsync(
        Guid internshipId,
        CurrentUser caller,
        ApplicationQueryParameters query,
        CancellationToken ct = default)
    {
        var internship = await _internships.GetByIdAsync(internshipId, ct)
            ?? throw new NotFoundException("Internship", internshipId);

        bool allowed = caller.Role == UserRole.Admin ||
                       (caller.Role == UserRole.Company && caller.ProfileId == internship.CompanyProfileId);
        if (!allowed)
            throw new ForbiddenException("Only the company that owns this internship can see its applications.");

        var (items, total) = await _applications.GetByInternshipAsync(internshipId, query, ct);
        return ToPage(items, total, query);
    }

    public async Task UpdateApplicationStatusAsync(
        Guid applicationId,
        Guid companyProfileId,
        ApplicationStatus newStatus,
        CancellationToken ct = default)
    {
        var application = await _applications.GetByIdAsync(applicationId, ct)
            ?? throw new NotFoundException("Application", applicationId);

        if (application.Internship.CompanyProfileId != companyProfileId)
            throw new ForbiddenException("Only the company that owns the internship can change the application status.");

        application.Status = newStatus;
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task WithdrawApplicationAsync(
        Guid applicationId,
        Guid studentProfileId,
        CancellationToken ct = default)
    {
        var application = await _applications.GetByIdAsync(applicationId, ct)
            ?? throw new NotFoundException("Application", applicationId);

        if (application.StudentProfileId != studentProfileId)
            throw new ForbiddenException("You can only withdraw your own applications.");

        if (application.Status is ApplicationStatus.Accepted or ApplicationStatus.Rejected or ApplicationStatus.Withdrawn)
            throw new BusinessRuleException($"An application with status {application.Status} cannot be withdrawn.");

        application.Status = ApplicationStatus.Withdrawn;
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static PagedResult<ApplicationDto> ToPage(IReadOnlyList<Application> items, int total, PagingParameters query) => new()
    {
        Items = items.Select(MapToDto).ToList(),
        TotalCount = total,
        Page = query.Page,
        PageSize = query.PageSize
    };

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
