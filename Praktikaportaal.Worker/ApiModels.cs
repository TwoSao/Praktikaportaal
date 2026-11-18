namespace Praktikaportaal.Worker;

// The Worker's own view of the API's JSON. It does not share code with the API on purpose.

public record LoginRequest(string Email, string Password);

public record LoginResponse(string Token);

public record PageOf<T>(List<T> Items, int TotalCount);

public record StudentSummary(Guid Id, Guid UserId, string FirstName, string LastName, string Email);

public record InternshipSummary(Guid Id, string Title, string CompanyName, string? Location);

public record Recommendation(double OverlapPercent, InternshipSummary Internship, List<string> MatchedSkills);

public record CreateNotificationRequest(Guid UserId, string Title, string Message);
