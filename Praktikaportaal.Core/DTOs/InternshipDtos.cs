using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.DTOs;

public class CreateInternshipDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string? Requirements { get; set; }
    public string? Location { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? ApplicationDeadline { get; set; }
    public bool IsRemote { get; set; }
    public List<Guid>? SkillIds { get; set; }
}

public class InternshipDto
{
    public Guid Id { get; set; }
    public Guid CompanyProfileId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyLocation { get; set; }
    public string? CompanyWebsite { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string? Requirements { get; set; }
    public string? Location { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? ApplicationDeadline { get; set; }
    public bool IsRemote { get; set; }
    public InternshipStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Skills { get; set; } = [];
    public int ApplicationsCount { get; set; }
}

public class UpdateInternshipStatusDto
{
    public InternshipStatus Status { get; set; }
}
