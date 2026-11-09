namespace Praktikaportaal.Core.Exceptions;

/// <summary>
/// Base class for expected business errors thrown by the services layer.
/// The API's global exception handler maps each subtype to an HTTP status code
/// and a ProblemDetails response, so controllers need no try/catch.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }

    /// <summary>Short, stable title for the ProblemDetails response.</summary>
    public abstract string Title { get; }
}

/// <summary>Requested resource does not exist (HTTP 404).</summary>
public class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string resource, object key)
        : base($"{resource} '{key}' was not found.") { }

    public override string Title => "Resource not found";
}

/// <summary>Caller is authenticated but may not access this resource (HTTP 403).</summary>
public class ForbiddenException : DomainException
{
    public ForbiddenException(string message = "You do not have access to this resource.") : base(message) { }

    public override string Title => "Forbidden";
}

/// <summary>Invalid credentials or inactive account (HTTP 401).</summary>
public class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message = "Invalid email or password.") : base(message) { }

    public override string Title => "Unauthorized";
}

/// <summary>Request conflicts with current state, e.g. duplicate email or application (HTTP 409).</summary>
public class ConflictException : DomainException
{
    public ConflictException(string message) : base(message) { }

    public override string Title => "Conflict";
}

/// <summary>A business rule was violated, e.g. deadline passed or invalid status change (HTTP 400).</summary>
public class BusinessRuleException : DomainException
{
    public BusinessRuleException(string message) : base(message) { }

    public override string Title => "Business rule violation";
}
