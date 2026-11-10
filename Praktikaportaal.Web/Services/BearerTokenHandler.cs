using System.Net.Http.Headers;

namespace Praktikaportaal.Web.Services;

/// <summary>
/// Adds the logged-in user's JWT (stored in the auth cookie) to every API request.
/// A handler is used instead of changing the HttpClient's default headers because the client is
/// shared between requests of different users.
/// </summary>
public class BearerTokenHandler : DelegatingHandler
{
    public const string TokenClaim = "jwt_token";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public BearerTokenHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? token = _httpContextAccessor.HttpContext?.User.FindFirst(TokenClaim)?.Value;
        if (!string.IsNullOrEmpty(token) && request.Headers.Authorization == null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return base.SendAsync(request, cancellationToken);
    }
}
