using System.Net.WebSockets;
using Microsoft.Extensions.Options;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Web.Services;

namespace Praktikaportaal.Web.WebSockets;

/// <summary>Opens the WebSocket to the API (replaceable in tests).</summary>
public interface IUpstreamSocketConnector
{
    Task<WebSocket> ConnectAsync(Uri uri, CancellationToken ct);
}

public class ClientWebSocketConnector : IUpstreamSocketConnector
{
    public async Task<WebSocket> ConnectAsync(Uri uri, CancellationToken ct)
    {
        var socket = new ClientWebSocket();
        try
        {
            await socket.ConnectAsync(uri, ct);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}

public class SocketProxyOptions
{
    /// <summary>Base URL of the API (http/https); converted to ws/wss.</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5117";
}

/// <summary>
/// <c>/ws/applications</c> on the MVC site: relays the API's live "ApplicationReceived" messages to the browser.
///
/// Why a proxy instead of connecting the browser straight to the API?
///  * the browser authenticates with the HttpOnly login cookie, so the JWT never reaches page scripts (XSS cannot steal it);
///  * the browser only talks to its own origin (no CORS / mixed-content questions).
/// Because a cookie authenticates the socket, the Origin header is checked (cross-site WebSocket hijacking).
/// </summary>
public static class ApplicationsSocketProxy
{
    public const string Path = "/ws/applications";

    public static void MapApplicationsProxy(this WebApplication app)
    {
        app.Map(Path, async (HttpContext context, IUpstreamSocketConnector connector, IOptions<SocketProxyOptions> options, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Praktikaportaal.Web.WebSockets");

            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            // Only the logged-in company's own pages may listen.
            if (context.User.Identity?.IsAuthenticated != true)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            if (!context.User.IsInRole(nameof(UserRole.Company)))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            if (!IsSameOrigin(context.Request))
            {
                logger.LogWarning("Rejected WebSocket from foreign origin {Origin}", context.Request.Headers.Origin.ToString());
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            string? token = context.User.FindFirst(BearerTokenHandler.TokenClaim)?.Value;
            if (string.IsNullOrEmpty(token))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            // Connect to the API first: if that fails the browser gets a plain HTTP error and retries later.
            WebSocket upstream;
            try
            {
                upstream = await connector.ConnectAsync(UpstreamUri(options.Value.ApiBaseUrl, token), context.RequestAborted);
            }
            catch (Exception ex) when (ex is WebSocketException or HttpRequestException or OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not open the upstream WebSocket to the API");
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return;
            }

            using (upstream)
            using (var browser = await context.WebSockets.AcceptWebSocketAsync())
            {
                await RelayAsync(browser, upstream, context.RequestAborted);
            }
        });
    }

    /// <summary>The Origin header (sent by browsers on every WebSocket handshake) must be this very site.</summary>
    public static bool IsSameOrigin(HttpRequest request)
    {
        string origin = request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin))
            return true; // not a browser (no Origin): it still needs the login cookie

        return Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
               string.Equals(uri.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase);
    }

    public static Uri UpstreamUri(string apiBaseUrl, string token)
    {
        var builder = new UriBuilder(apiBaseUrl.TrimEnd('/'))
        {
            Scheme = apiBaseUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws",
            Path = "/ws/applications",
            Query = "access_token=" + Uri.EscapeDataString(token)
        };
        return builder.Uri;
    }

    /// <summary>Copies messages both ways until one side closes, then closes the other side too.</summary>
    private static async Task RelayAsync(WebSocket browser, WebSocket upstream, CancellationToken ct)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var toBrowser = PumpAsync(upstream, browser, stop.Token);
        var toUpstream = PumpAsync(browser, upstream, stop.Token);

        await Task.WhenAny(toBrowser, toUpstream);
        await stop.CancelAsync();

        try { await Task.WhenAll(toBrowser, toUpstream); }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException) { /* the other side was already gone */ }
    }

    private static async Task PumpAsync(WebSocket source, WebSocket destination, CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            while (source.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await source.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // Pass the close (and its reason, e.g. "token expired") on to the other side.
                    if (destination.State is WebSocketState.Open or WebSocketState.CloseReceived)
                    {
                        await destination.CloseOutputAsync(
                            result.CloseStatus ?? WebSocketCloseStatus.NormalClosure, result.CloseStatusDescription, CancellationToken.None);
                    }
                    return;
                }

                if (destination.State != WebSocketState.Open)
                    return;

                await destination.SendAsync(buffer.AsMemory(0, result.Count), result.MessageType, result.EndOfMessage, ct);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
        {
            // the connection broke or we are shutting down: the other pump / RelayAsync cleans up
        }
        finally
        {
            // If this side ended without a proper close, close the other side so it does not hang.
            if (destination.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try { await destination.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, "Connection ended", CancellationToken.None); }
                catch (WebSocketException) { }
            }
        }
    }
}
