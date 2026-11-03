using Microsoft.EntityFrameworkCore;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Infrastructure.Persistence;

namespace Praktikaportaal.Infrastructure.Services;

public class ApplicationService : IApplicationService
{
    private readonly PraktikaportaalDbContext _context;

    public ApplicationService(PraktikaportaalDbContext context)
    {
        _context = context;
    }

    public async Task<(bool Success, string? Error, ApplicationDto? Application)> ApplyAsync(
        Guid studentProfileId,
        CreateApplicationDto dto,
        CancellationToken ct = default)
    {
        var internship = await _context.Internships
            .Include(i => i.CompanyProfile)
            .FirstOrDefaultAsync(i => i.Id == dto.InternshipId, ct);

        if (internship == null)
            return (false, "Internship listing not found.", null);

        if (internship.Status != InternshipStatus.Published)
            return (false, "This internship is not currently accepting applications.", null);

        if (internship.ApplicationDeadline.HasValue && internship.ApplicationDeadline.Value < DateTime.UtcNow)
            return (false, "The application deadline for this internship has passed.", null);

        // Business rule: Student cannot have more than one active (non-withdrawn) application for the same internship.
        bool hasActiveApplication = await _context.Applications
            .AnyAsync(a => a.StudentProfileId == studentProfileId &&
                           a.InternshipId == dto.InternshipId &&
                           a.Status != ApplicationStatus.Withdrawn, ct);

        if (hasActiveApplication)
            return (false, "You already have an active application for this internship.", null);

        var application = new Application
        {
            StudentProfileId = studentProfileId,
            InternshipId = dto.InternshipId,
            CoverLetter = dto.CoverLetter?.Trim(),
            Status = ApplicationStatus.Pending,
            AppliedAt = DateTime.UtcNow
        };

        await _context.Applications.AddAsync(application, ct);
        await _context.SaveChangesAsync(ct);

        var studentProfile = await _context.StudentProfiles
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.Id == studentProfileId, ct);

        var applicationDto = new ApplicationDto
        {
            Id = application.Id,
            InternshipId = internship.Id,
            InternshipTitle = internship.Title,
            CompanyName = internship.CompanyProfile.CompanyName,
            StudentProfileId = studentProfileId,
            StudentName = $"{studentProfile?.User.FirstName} {studentProfile?.User.LastName}".Trim(),
            StudentEmail = studentProfile?.User.Email ?? string.Empty,
            StudentPhoneNumber = studentProfile?.PhoneNumber,
            SchoolName = studentProfile?.SchoolName,
            StudyProgram = studentProfile?.StudyProgram,
            Course = studentProfile?.Course ?? 0,
            CoverLetter = application.CoverLetter,
            Status = application.Status,
            AppliedAt = application.AppliedAt
        };

        return (true, null, applicationDto);
    }

    public async Task<IReadOnlyList<ApplicationDto>> GetStudentApplicationsAsync(
        Guid studentProfileId,
        CancellationToken ct = default)
    {
        var list = await _context.Applications
            .Include(a => a.Internship)
                .ThenInclude(i => i.CompanyProfile)
            .Include(a => a.StudentProfile)
                .ThenInclude(sp => sp.User)
            .Where(a => a.StudentProfileId == studentProfileId)
            .OrderByDescending(a => a.AppliedAt)
            .ToListAsync(ct);

        return list.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<ApplicationDto>> GetInternshipApplicationsAsync(
        Guid internshipId,
        Guid companyProfileId,
        CancellationToken ct = default)
    {
        // Verify company owns this internship
        bool ownsInternship = await _context.Internships
            .AnyAsync(i => i.Id == internshipId && i.CompanyProfileId == companyProfileId, ct);

        if (!ownsInternship)
            return [];

        var list = await _context.Applications
            .Include(a => a.Internship)
                .ThenInclude(i => i.CompanyProfile)
            .Include(a => a.StudentProfile)
                .ThenInclude(sp => sp.User)
            .Where(a => a.InternshipId == internshipId)
            .OrderByDescending(a => a.AppliedAt)
            .ToListAsync(ct);

        return list.Select(MapToDto).ToList();
    }

    public async Task<bool> UpdateApplicationStatusAsync(
        Guid applicationId,
        Guid companyProfileId,
        ApplicationStatus newStatus,
        CancellationToken ct = default)
    {
        var application = await _context.Applications
            .Include(a => a.Internship)
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);

        if (application == null)
            return false;

        // Verify company owns the internship
        if (application.Internship.CompanyProfileId != companyProfileId)
            return false;

        application.Status = newStatus;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> WithdrawApplicationAsync(
        Guid applicationId,
        Guid studentProfileId,
        CancellationToken ct = default)
    {
        var application = await _context.Applications
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.StudentProfileId == studentProfileId, ct);

        if (application == null)
            return false;

        application.Status = ApplicationStatus.Withdrawn;
        await _context.SaveChangesAsync(ct);
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
