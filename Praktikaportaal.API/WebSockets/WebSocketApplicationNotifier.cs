using System.Text.Json;
using Praktikaportaal.Core.Interfaces;

namespace Praktikaportaal.API.WebSockets;

/// <summary>
/// Pushes "ApplicationReceived" messages to every open WebSocket of the company that owns the internship.
/// Never throws: a broken connection must not fail the request that created the application.
/// </summary>
public class WebSocketApplicationNotifier : IApplicationNotifier
{
    public const string MessageType = "ApplicationReceived";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConnectionGroups _groups;
    private readonly ILogger<WebSocketApplicationNotifier> _logger;

    public WebSocketApplicationNotifier(ConnectionGroups groups, ILogger<WebSocketApplicationNotifier> logger)
    {
        _groups = groups;
        _logger = logger;
    }

    public async Task ApplicationReceivedAsync(ApplicationReceivedEvent e, CancellationToken ct = default)
    {
        var connections = _groups.GetOpen(ConnectionGroups.CompanyGroup(e.CompanyProfileId));
        if (connections.Count == 0)
            return;

        string json = JsonSerializer.Serialize(new
        {
            type = MessageType,
            applicationId = e.ApplicationId,
            internshipId = e.InternshipId,
            internshipTitle = e.InternshipTitle,
            studentName = e.StudentName,
            createdAt = e.CreatedAt
        }, JsonOptions);

        foreach (var connection in connections)
        {
            try
            {
                await connection.SendTextAsync(json, ct);
            }
            catch (Exception ex)
            {
                // Typically the browser went away; its receive loop will clean the connection up.
                _logger.LogWarning(ex, "Could not push {Type} to connection {ConnectionId}", MessageType, connection.Id);
            }
        }

        _logger.LogInformation("Pushed {Type} for application {ApplicationId} to {Count} connection(s) of company {CompanyId}",
            MessageType, e.ApplicationId, connections.Count, e.CompanyProfileId);
    }
}
