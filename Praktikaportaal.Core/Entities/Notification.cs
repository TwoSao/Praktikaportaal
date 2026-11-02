namespace Praktikaportaal.Core.Entities;

/// <summary>
/// A notification delivered to a specific user (e.g. application status change,
/// new message, internship match).
/// Notifications belong to a User, not a profile, so Admins can also receive them.
/// </summary>
public class Notification : BaseEntity
{
    /// <summary>FK to the recipient user.</summary>
    public Guid UserId { get; set; }

    /// <summary>Short notification title shown in the UI. Max 200 chars.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Full notification message body. Max 1000 chars.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Whether the user has acknowledged / read the notification.</summary>
    public bool IsRead { get; set; } = false;

    // ── Navigation properties ──────────────────────────────────────────────

    public User User { get; set; } = null!;
}
