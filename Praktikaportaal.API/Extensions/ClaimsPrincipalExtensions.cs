using System.Security.Claims;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Exceptions;
using Praktikaportaal.Infrastructure.Services;

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
        string? idStr = principal.FindFirstValue(JwtTokenService.ProfileIdClaim);
        return Guid.TryParse(idStr, out var id) ? id : null;
    }

    /// <summary>Profile id of a Student/Company caller; throws 403 if the token has none.</summary>
    public static Guid RequireProfileId(this ClaimsPrincipal principal) =>
        principal.GetProfileId() ?? throw new ForbiddenException("This action requires a student or company profile.");

    public static string? GetRole(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Role);
    }

    /// <summary>Builds the services-layer view of the caller from JWT claims.</summary>
    public static CurrentUser ToCurrentUser(this ClaimsPrincipal principal) => new(
        principal.GetUserId(),
        Enum.TryParse<UserRole>(principal.GetRole(), out var role) ? role : throw new ForbiddenException(),
        principal.GetProfileId());
}
