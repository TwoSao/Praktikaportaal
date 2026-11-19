namespace Praktikaportaal.Web.Models;

public class ApplicationItemViewModel
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
    public string Status { get; set; } = "Pending";
    public DateTime AppliedAt { get; set; }
}

