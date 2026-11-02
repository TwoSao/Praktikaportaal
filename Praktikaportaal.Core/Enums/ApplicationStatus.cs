namespace Praktikaportaal.Core.Enums;

/// <summary>
/// Lifecycle status of an internship application.
/// A student may reapply after Withdrawn if business rules permit.
/// </summary>
public enum ApplicationStatus
{
    /// <summary>Submitted and awaiting company review.</summary>
    Pending = 0,

    /// <summary>Company has accepted the application.</summary>
    Accepted = 1,

    /// <summary>Company has rejected the application.</summary>
    Rejected = 2,

    /// <summary>Student has voluntarily withdrawn the application.</summary>
    Withdrawn = 3
}
