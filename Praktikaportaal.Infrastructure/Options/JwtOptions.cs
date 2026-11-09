using System.ComponentModel.DataAnnotations;

namespace Praktikaportaal.Infrastructure.Options;

/// <summary>
/// JWT settings bound from the "Jwt" configuration section and validated on startup.
/// The secret is never hard-coded: set it via user-secrets / environment variable
/// (Jwt__Secret) in production. appsettings.Development.json holds a dev-only value.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 signing key. At least 32 characters (256 bits).</summary>
    [Required, MinLength(32)]
    public string Secret { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Range(1, 24 * 30)]
    public int ExpiresInHours { get; set; } = 24;
}
