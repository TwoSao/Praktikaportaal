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
    private readonly INotificationRepository _notifications;
    private readonly IApplicationNotifier _notifier;
    private readonly IUnitOfWork _unitOfWork;

    public ApplicationService(
        IApplicationRepository applications,
        IInternshipRepository internships,
        IStudentProfileRepository students,
        INotificationRepository notifications,
        IApplicationNotifier notifier,
        IUnitOfWork unitOfWork)
    {
        _notifier = notifier;
        _applications = applications;
        _internships = internships;
        _students = students;
        _notifications = notifications;
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

        var studentProfile = await _students.GetWithSkillsAsync(studentProfileId, ct)
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

        // In-app notification for the company (saved in the same transaction as the application).
        await _notifications.AddAsync(new Notification
        {
            UserId = internship.CompanyProfile.UserId,
            Title = "New application",
            Message = $"{studentProfile.User.FirstName} {studentProfile.User.LastName} applied for \"{internship.Title}\"."
        }, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        // Real-time push to the company's open dashboards (after the data is safely saved).
        await _notifier.ApplicationReceivedAsync(new ApplicationReceivedEvent(
            internship.CompanyProfileId,
            application.Id,
            internship.Id,
            internship.Title,
            $"{studentProfile.User.FirstName} {studentProfile.User.LastName}".Trim(),
            application.AppliedAt), ct);

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

        if (!ApplicationStatusRules.CanCompanyChange(application.Status, newStatus))
        {
            var allowed = ApplicationStatusRules.AllowedForCompany(application.Status);
            string hint = allowed.Count == 0
                ? "This status is final."
                : $"Allowed next statuses: {string.Join(", ", allowed)}.";
            throw new BusinessRuleException($"Cannot change application status from {application.Status} to {newStatus}. {hint}");
        }

        application.Status = newStatus;

        // Tell the student what happened to their application.
        await _notifications.AddAsync(new Notification
        {
            UserId = application.StudentProfile.UserId,
            Title = "Application status updated",
            Message = $"Your application for \"{application.Internship.Title}\" at " +
                      $"{application.Internship.CompanyProfile.CompanyName} is now: {newStatus}."
        }, ct);

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

        if (!ApplicationStatusRules.CanStudentWithdraw(application.Status))
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
        HasCv = a.StudentProfile.CVs.Any(cv => !cv.IsDeleted),
        Status = a.Status,
        AppliedAt = a.AppliedAt
    };
}
