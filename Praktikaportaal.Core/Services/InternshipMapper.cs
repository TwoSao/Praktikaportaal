using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.Services;

/// <summary>Maps an <see cref="Internship"/> (loaded with company, skills and applications) to its DTO.</summary>
internal static class InternshipMapper
{
    public static InternshipDto ToDto(Internship i) => new()
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
