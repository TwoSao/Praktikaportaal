using System.Text;
using System.Text.Json;

namespace Praktikaportaal.Web.Services;

/// <summary>
/// Reads the "exp" claim of a JWT without validating it (the API validates tokens; the site only needs to know
/// when the stored token stops working so the login cookie can expire at the same time).
/// </summary>
public static class JwtExpiry
{
    public static DateTimeOffset? TryGetExpiry(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
            return null;

        string[] parts = jwt.Split('.');
        if (parts.Length != 3)
            return null;

        try
        {
            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return doc.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out long seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }
}
