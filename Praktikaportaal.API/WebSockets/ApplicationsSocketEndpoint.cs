using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Praktikaportaal.API.Extensions;
using Praktikaportaal.Core.Constants;

namespace Praktikaportaal.API.WebSockets;

/// <summary>
/// <c>/ws/applications?access_token={JWT}</c> — live "ApplicationReceived" messages for companies.
/// Browsers cannot set an Authorization header on a WebSocket, so the JWT travels in the query string
/// (see <see cref="IsSocketPath"/> and the JwtBearer OnMessageReceived hook in Program.cs).
/// </summary>
public static class ApplicationsSocketEndpoint
{
    public const string Path = "/ws/applications";

    public static bool IsSocketPath(PathString path) => path.StartsWithSegments("/ws");

    public static void MapApplicationsSocket(this WebApplication app)
    {
        app.Map(Path, async (HttpContext context, ConnectionGroups groups, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Praktikaportaal.API.WebSockets");

            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("This endpoint accepts WebSocket connections only.");
                return;
            }

            var auth = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            if (!auth.Succeeded || auth.Principal is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var user = auth.Principal;
            var companyId = user.GetProfileId();
            if (user.GetRole() != Roles.Company || companyId is null)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden; // companies only
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var connection = new SocketConnection(socket);
            string group = ConnectionGroups.CompanyGroup(companyId.Value);
            groups.Add(group, connection);
            logger.LogInformation("WebSocket {ConnectionId} joined {Group}", connection.Id, group);

            // Close the connection when the JWT expires so a stolen/old token cannot listen forever.
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            if (ExpiresAt(user) is { } expiresAt)
                lifetime.CancelAfter(expiresAt - DateTimeOffset.UtcNow > TimeSpan.Zero ? expiresAt - DateTimeOffset.UtcNow : TimeSpan.Zero);

            try
            {
                await connection.SendTextAsync("""{"type":"Connected"}""", lifetime.Token);
                await ReceiveUntilClosedAsync(connection, lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                // token expired or server shutting down
            }
            catch (WebSocketException ex)
            {
                logger.LogDebug(ex, "WebSocket {ConnectionId} ended abruptly", connection.Id);
            }
            finally
            {
                groups.Remove(group, connection);
                logger.LogInformation("WebSocket {ConnectionId} left {Group}", connection.Id, group);
                await CloseQuietlyAsync(socket, lifetime.IsCancellationRequested && !context.RequestAborted.IsCancellationRequested);
            }
        });
    }

    /// <summary>The server sends only; incoming messages are ignored (but must be read to detect a close).</summary>
    private static async Task ReceiveUntilClosedAsync(SocketConnection connection, CancellationToken ct)
    {
        var buffer = new byte[256];
        while (connection.IsOpen && !ct.IsCancellationRequested)
        {
            var result = await connection.Socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
                break;

            // A client "ping" gets a "pong" so browser code can use an application-level heartbeat.
            if (result.MessageType == WebSocketMessageType.Text && result.EndOfMessage &&
                Encoding.UTF8.GetString(buffer, 0, result.Count).Trim() == "ping")
            {
                await connection.SendTextAsync("""{"type":"pong"}""", ct);
            }
        }
    }

    private static async Task CloseQuietlyAsync(WebSocket socket, bool tokenExpired)
    {
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseOutputAsync(
                    tokenExpired ? WebSocketCloseStatus.PolicyViolation : WebSocketCloseStatus.NormalClosure,
                    tokenExpired ? "Token expired" : "Closing",
                    CancellationToken.None);
            }
        }
        catch (WebSocketException)
        {
            // peer already gone
        }
    }

    private static DateTimeOffset? ExpiresAt(ClaimsPrincipal user) =>
        long.TryParse(user.FindFirstValue("exp"), out long exp) ? DateTimeOffset.FromUnixTimeSeconds(exp) : null;
}
