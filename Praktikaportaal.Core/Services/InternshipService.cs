using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Exceptions;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Core.Services;

public class InternshipService : IInternshipService
{
    private readonly IInternshipRepository _internships;
    private readonly ISkillRepository _skills;
    private readonly IUnitOfWork _unitOfWork;

    public InternshipService(IInternshipRepository internships, ISkillRepository skills, IUnitOfWork unitOfWork)
    {
        _internships = internships;
        _skills = skills;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<InternshipDto>> GetPublishedInternshipsAsync(
        string? search = null,
        bool? isRemote = null,
        CancellationToken ct = default)
    {
        var list = await _internships.GetPublishedAsync(search, isRemote, ct);
        return list.Select(MapToDto).ToList();
    }

    public async Task<InternshipDto> GetInternshipByIdAsync(Guid id, CurrentUser? caller = null, CancellationToken ct = default)
    {
        var internship = await _internships.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Internship", id);

        // Drafts/closed listings are hidden from the public; don't reveal that they exist.
        if (internship.Status != InternshipStatus.Published && (caller == null || !CanManage(internship, caller)))
            throw new NotFoundException("Internship", id);

        return MapToDto(internship);
    }

    public async Task<IReadOnlyList<InternshipDto>> GetCompanyInternshipsAsync(Guid companyProfileId, CancellationToken ct = default)
    {
        var list = await _internships.GetByCompanyAsync(companyProfileId, ct);
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
            Status = InternshipStatus.Published // published on creation so students can see it
        };
        ApplyFields(internship, dto);

        foreach (var skillId in await ValidateSkillIdsAsync(dto.SkillIds, ct))
            internship.InternshipSkills.Add(new InternshipSkill { InternshipId = internship.Id, SkillId = skillId });

        await _internships.AddAsync(internship, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // Reload with company and skill names for the response.
        return MapToDto((await _internships.GetByIdAsync(internship.Id, ct))!);
    }

    public async Task<InternshipDto> UpdateInternshipAsync(
        Guid internshipId,
        UpdateInternshipDto dto,
        CurrentUser caller,
        CancellationToken ct = default)
    {
        var internship = await GetManageableAsync(internshipId, caller, ct);
        ApplyFields(internship, dto);

        // Sync required skills: remove dropped ones, add new ones (keeps unchanged rows tracked).
        var wanted = await ValidateSkillIdsAsync(dto.SkillIds, ct);
        foreach (var removed in internship.InternshipSkills.Where(s => !wanted.Contains(s.SkillId)).ToList())
            internship.InternshipSkills.Remove(removed);
        foreach (var added in wanted.Where(id => internship.InternshipSkills.All(s => s.SkillId != id)))
            internship.InternshipSkills.Add(new InternshipSkill { InternshipId = internship.Id, SkillId = added });

        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto((await _internships.GetByIdAsync(internship.Id, ct))!);
    }

    public async Task DeleteInternshipAsync(Guid internshipId, CurrentUser caller, CancellationToken ct = default)
    {
        var internship = await GetManageableAsync(internshipId, caller, ct);

        internship.IsDeleted = true;
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task UpdateInternshipStatusAsync(
        Guid internshipId,
        InternshipStatus newStatus,
        CurrentUser caller,
        CancellationToken ct = default)
    {
        var internship = await GetManageableAsync(internshipId, caller, ct);

        internship.Status = newStatus;
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Owning company or admin (moderation).</summary>
    private static bool CanManage(Internship internship, CurrentUser caller) =>
        caller.Role == UserRole.Admin ||
        (caller.Role == UserRole.Company && caller.ProfileId == internship.CompanyProfileId);

    private async Task<Internship> GetManageableAsync(Guid internshipId, CurrentUser caller, CancellationToken ct)
    {
        var internship = await _internships.GetByIdAsync(internshipId, ct)
            ?? throw new NotFoundException("Internship", internshipId);

        if (!CanManage(internship, caller))
            throw new ForbiddenException("Only the owning company or an admin can manage this internship.");

        return internship;
    }

    private async Task<HashSet<Guid>> ValidateSkillIdsAsync(IEnumerable<Guid>? skillIds, CancellationToken ct)
    {
        var ids = skillIds?.ToHashSet() ?? [];
        if (ids.Count == 0)
            return ids;

        var found = await _skills.GetByIdsAsync(ids, ct);
        var missing = ids.Except(found.Select(s => s.Id)).ToList();
        if (missing.Count > 0)
            throw new BusinessRuleException($"Unknown skill id(s): {string.Join(", ", missing)}.");

        return ids;
    }

    private static void ApplyFields(Internship internship, CreateInternshipDto dto)
    {
        internship.Title = dto.Title.Trim();
        internship.Description = dto.Description.Trim();
        internship.Field = dto.Field.Trim();
        internship.Requirements = dto.Requirements?.Trim();
        internship.Location = dto.Location?.Trim();
        internship.StartDate = dto.StartDate;
        internship.EndDate = dto.EndDate;
        internship.ApplicationDeadline = dto.ApplicationDeadline;
        internship.IsRemote = dto.IsRemote;
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
        Field = i.Field,
        Requirements = i.Requirements,
        Location = i.Location,
        StartDate = i.StartDate,
        EndDate = i.EndDate,
        ApplicationDeadline = i.ApplicationDeadline,
        IsRemote = i.IsRemote,
        Status = i.Status,
        CreatedAt = i.CreatedAt,
        Skills = i.InternshipSkills.Where(s => s.Skill != null).Select(s => s.Skill.Name).ToList(),
        ApplicationsCount = i.Applications.Count(a => a.Status != ApplicationStatus.Withdrawn)
    };
}
