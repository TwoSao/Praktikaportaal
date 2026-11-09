using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Core.Interfaces.Repositories;

namespace Praktikaportaal.Core.Services;

public class InternshipService : IInternshipService
{
    private readonly IInternshipRepository _internships;
    private readonly IUnitOfWork _unitOfWork;

    public InternshipService(IInternshipRepository internships, IUnitOfWork unitOfWork)
    {
        _internships = internships;
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

    public async Task<InternshipDto?> GetInternshipByIdAsync(Guid id, CancellationToken ct = default)
    {
        var internship = await _internships.GetByIdAsync(id, ct);
        return internship == null ? null : MapToDto(internship);
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
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            Field = dto.Field.Trim(),
            Requirements = dto.Requirements?.Trim(),
            Location = dto.Location?.Trim(),
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            ApplicationDeadline = dto.ApplicationDeadline,
            IsRemote = dto.IsRemote,
            Status = InternshipStatus.Published // published on creation so students can see it
        };

        foreach (var skillId in dto.SkillIds?.Distinct() ?? [])
        {
            internship.InternshipSkills.Add(new InternshipSkill
            {
                InternshipId = internship.Id,
                SkillId = skillId
            });
        }

        await _internships.AddAsync(internship, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // Reload with company and skill names for the response.
        return (await GetInternshipByIdAsync(internship.Id, ct))!;
    }

    public async Task<bool> UpdateInternshipStatusAsync(
        Guid internshipId,
        Guid companyProfileId,
        InternshipStatus newStatus,
        CancellationToken ct = default)
    {
        var internship = await _internships.GetByIdAsync(internshipId, ct);

        if (internship == null || internship.CompanyProfileId != companyProfileId)
            return false;

        internship.Status = newStatus;
        await _unitOfWork.SaveChangesAsync(ct);
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
        Field = i.Field,
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
