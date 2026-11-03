using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.DTOs;

public class CreateApplicationDto
{
    public Guid InternshipId { get; set; }
    public string? CoverLetter { get; set; }
}

public class ApplicationDto
{
    public Guid Id { get; set; }
    public Guid InternshipId { get; set; }
    public string InternshipTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public Guid StudentProfileId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public string? StudentPhoneNumber { get; set; }
    public string? SchoolName { get; set; }
    public string? StudyProgram { get; set; }
    public int Course { get; set; }
    public string? CoverLetter { get; set; }
    public ApplicationStatus Status { get; set; }
    public DateTime AppliedAt { get; set; }
}

public class UpdateApplicationStatusDto
{
    public ApplicationStatus Status { get; set; }
}
