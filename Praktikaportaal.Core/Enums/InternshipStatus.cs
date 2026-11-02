namespace Praktikaportaal.Core.Enums;

/// <summary>
/// Lifecycle status of an internship listing.
/// </summary>
public enum InternshipStatus
{
    /// <summary>Not yet published; visible only to the owning company.</summary>
    Draft = 0,

    /// <summary>Visible to students and accepting applications.</summary>
    Published = 1,

    /// <summary>Application deadline passed or company manually closed it.</summary>
    Closed = 2,

    /// <summary>Archived for record-keeping; no longer shown in active listings.</summary>
    Archived = 3
}
