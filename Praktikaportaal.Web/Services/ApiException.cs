using System.Net;

namespace Praktikaportaal.Web.Services;

/// <summary>
/// The API answered with an error. Carries the RFC 7807 ProblemDetails the API produces
/// (title, detail and, for validation errors, the per-field messages).
/// </summary>
public class ApiException : Exception
{
    public ApiException(HttpStatusCode statusCode, string title, string? detail, IReadOnlyDictionary<string, string[]>? errors = null)
        : base(detail ?? title)
    {
        StatusCode = statusCode;
        Title = title;
        Detail = detail;
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    public HttpStatusCode StatusCode { get; }
    public string Title { get; }
    public string? Detail { get; }

    /// <summary>Field name → messages, for 400 validation responses.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;
    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;
    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;

    /// <summary>The user did something the rules do not allow (400 / 409): show the message next to the form.</summary>
    public bool IsUserError => StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict;
}

/// <summary>The API could not be reached at all.</summary>
public class ApiUnavailableException : Exception
{
    public ApiUnavailableException(Exception inner)
        : base("Praktikaportaali server ei vasta. Proovi mõne hetke pärast uuesti.", inner)
    {
    }
}
