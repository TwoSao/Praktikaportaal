using Microsoft.EntityFrameworkCore;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Infrastructure.Persistence;

namespace Praktikaportaal.Infrastructure.Services;

public class InternshipService : IInternshipService
{
    private readonly PraktikaportaalDbContext _context;

    public InternshipService(PraktikaportaalDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<InternshipDto>> GetPublishedInternshipsAsync(
        string? search = null,
        bool? isRemote = null,
        CancellationToken ct = default)
    {
        var query = _context.Internships
            .Include(i => i.CompanyProfile)
            .Include(i => i.InternshipSkills)
                .ThenInclude(isk => isk.Skill)
            .Include(i => i.Applications)
            .Where(i => i.Status == InternshipStatus.Published);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string s = search.Trim().ToLower();
            query = query.Where(i =>
                i.Title.ToLower().Contains(s) ||
                i.Description.ToLower().Contains(s) ||
                (i.Location != null && i.Location.ToLower().Contains(s)) ||
                i.CompanyProfile.CompanyName.ToLower().Contains(s));
        }

        if (isRemote.HasValue)
        {
            query = query.Where(i => i.IsRemote == isRemote.Value);
        }

        var list = await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        return list.Select(MapToDto).ToList();
    }

    public async Task<InternshipDto?> GetInternshipByIdAsync(Guid id, CancellationToken ct = default)
    {
        var internship = await _context.Internships
            .Include(i => i.CompanyProfile)
            .Include(i => i.InternshipSkills)
                .ThenInclude(isk => isk.Skill)
            .Include(i => i.Applications)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        return internship == null ? null : MapToDto(internship);
    }

    public async Task<IReadOnlyList<InternshipDto>> GetCompanyInternshipsAsync(Guid companyProfileId, CancellationToken ct = default)
    {
        var list = await _context.Internships
            .Include(i => i.CompanyProfile)
            .Include(i => i.InternshipSkills)
                .ThenInclude(isk => isk.Skill)
            .Include(i => i.Applications)
            .Where(i => i.CompanyProfileId == companyProfileId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        return list.Select(MapToDto).ToList();
    }

    public async Task<InternshipDto> CreateInternshipAsync(
        Guid companyProfileId,
        CreateInternshipDto dto,
        CancellationToken ct = default)
    {
        var internship = new Internship
        {
            CompanyProfileId = companyProfileId,
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            Requirements = dto.Requirements?.Trim(),
            Location = dto.Location?.Trim(),
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            ApplicationDeadline = dto.ApplicationDeadline,
            IsRemote = dto.IsRemote,
            Status = InternshipStatus.Published // by default published on creation so students can see it
        };

        if (dto.SkillIds != null && dto.SkillIds.Count > 0)
        {
            foreach (var skillId in dto.SkillIds.Distinct())
            {
                internship.InternshipSkills.Add(new InternshipSkill
                {
                    InternshipId = internship.Id,
                    SkillId = skillId
                });
            }
        }

        await _context.Internships.AddAsync(internship, ct);
        await _context.SaveChangesAsync(ct);

        return (await GetInternshipByIdAsync(internship.Id, ct))!;
    }

    public async Task<bool> UpdateInternshipStatusAsync(
        Guid internshipId,
        Guid companyProfileId,
        InternshipStatus newStatus,
        CancellationToken ct = default)
    {
        var internship = await _context.Internships
            .FirstOrDefaultAsync(i => i.Id == internshipId && i.CompanyProfileId == companyProfileId, ct);

        if (internship == null)
            return false;

        internship.Status = newStatus;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private static InternshipDto MapToDto(Internship i) => new()
    {
        Id = i.Id,
        CompanyProfileId = i.CompanyProfileId,
        CompanyName = i.CompanyProfile.CompanyName,
        CompanyLocation = i.CompanyProfile.Address,
        CompanyWebsite = i.CompanyProfile.Website,
        Title = i.Title,
        Description = i.Description,
        Requirements = i.Requirements,
        Location = i.Location,
        StartDate = i.StartDate,
        EndDate = i.EndDate,
        ApplicationDeadline = i.ApplicationDeadline,
        IsRemote = i.IsRemote,
        Status = i.Status,
        CreatedAt = i.CreatedAt,
        Skills = i.InternshipSkills.Select(s => s.Skill.Name).ToList(),
        ApplicationsCount = i.Applications.Count(a => a.Status != ApplicationStatus.Withdrawn)
    };
}
