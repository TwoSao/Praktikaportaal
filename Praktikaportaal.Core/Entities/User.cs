using Praktikaportaal.Core.Enums;

namespace Praktikaportaal.Core.Entities;

/// <summary>
/// Represents an authenticated principal in the system.
/// A User may have exactly one StudentProfile OR one CompanyProfile,
/// determined by their Role. Admin users have neither profile.
/// Passwords are never stored in plain text; only the bcrypt/PBKDF2 hash is kept.
/// </summary>
public class User : BaseEntity
{
    /// <summary>Unique email address used for authentication. Max 256 chars (RFC 5321).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Hashed password (bcrypt / PBKDF2). Never the plain-text password.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Legal first name. Max 100 chars.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Legal last name. Max 100 chars.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>Role determines which profile type (if any) this user has.</summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Whether the account is active. Inactive accounts cannot sign in
    /// but their data is preserved (see BaseEntity.IsDeleted for hard removal).
    /// </summary>
    public bool IsActive { get; set; } = true;

    // ── Navigation properties ──────────────────────────────────────────────

    /// <summary>Present only when Role == Student.</summary>
    public StudentProfile? StudentProfile { get; set; }

    /// <summary>Present only when Role == Company.</summary>
    public CompanyProfile? CompanyProfile { get; set; }

    public ICollection<Notification> Notifications { get; set; } = [];
}
