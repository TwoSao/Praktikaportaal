namespace Praktikaportaal.Core.Enums;

/// <summary>
/// Lifecycle status of an internship application.
/// Allowed transitions are validated in the services layer:
/// Submitted → Viewed → Interview → Accepted/Rejected (Rejected is possible from any open state);
/// the student may withdraw while the application is still open.
/// </summary>
public enum ApplicationStatus
{
    /// <summary>Submitted by the student, not yet opened by the company.</summary>
    Submitted = 0,

    /// <summary>The company has viewed the application.</summary>
    Viewed = 1,

    /// <summary>The student has been invited to an interview.</summary>
    Interview = 2,

    /// <summary>The company has accepted the application.</summary>
    Accepted = 3,

    /// <summary>The company has rejected the application.</summary>
    Rejected = 4,

    /// <summary>The student has voluntarily withdrawn the application.</summary>
    Withdrawn = 5
}
