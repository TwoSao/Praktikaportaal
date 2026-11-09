using System.Security.Claims;

namespace Praktikaportaal.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        string? idStr = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
    }

    public static Guid? GetProfileId(this ClaimsPrincipal principal)
    {
        string? idStr = principal.FindFirstValue("profile_id");
        return Guid.TryParse(idStr, out var id) ? id : null;
    }

    public static string? GetRole(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Role);
    }
}

