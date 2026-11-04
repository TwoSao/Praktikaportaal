namespace Praktikaportaal.Core.Entities;

/// <summary>
/// Company-specific profile linked one-to-one to a User with Role == Company.
/// Contains business identity and contact information.
/// </summary>
public class CompanyProfile : BaseEntity
{
    /// <summary>FK to the owning User (1-to-1).</summary>
    public Guid UserId { get; set; }

    /// <summary>Official registered company name. Max 200 chars.</summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>
    /// Official company registration code (business registry number).
    /// Max 50 chars. Used for verification purposes.
    /// </summary>
    public string RegistrationCode { get; set; } = string.Empty;

    /// <summary>Company description / about section. Max 3000 chars.</summary>
    public string? Description { get; set; }

    /// <summary>Company website URL. Max 300 chars.</summary>
    public string? Website { get; set; }

    /// <summary>Company street address. Max 300 chars.</summary>
    public string? Address { get; set; }

    /// <summary>Primary business contact email. Max 256 chars.</summary>
    public string? ContactEmail { get; set; }

    /// <summary>Primary business contact phone. Max 30 chars.</summary>
    public string? ContactPhone { get; set; }

    // ── Navigation properties ──────────────────────────────────────────────

    public User User { get; set; } = null!;

    public ICollection<Internship> Internships { get; set; } = [];
}
