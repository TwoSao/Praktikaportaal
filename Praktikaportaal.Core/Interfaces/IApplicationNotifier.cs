namespace Praktikaportaal.Core.Interfaces;

/// <summary>A new application was saved; the owning company should hear about it right away.</summary>
public record ApplicationReceivedEvent(
    Guid CompanyProfileId,
    Guid ApplicationId,
    Guid InternshipId,
    string InternshipTitle,
    string StudentName,
    DateTime CreatedAt);

/// <summary>
/// Real-time push to connected clients. The services layer only knows this abstraction;
/// the API project implements it with WebSockets. Implementations must not throw:
/// a failed push must never fail the request that saved the application.
/// </summary>
public interface IApplicationNotifier
{
    Task ApplicationReceivedAsync(ApplicationReceivedEvent applicationEvent, CancellationToken ct = default);
}
