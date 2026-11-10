using System.Net;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Web.WebSockets;

namespace Praktikaportaal.Web.Tests;

/// <summary>A stand-in for the API's /ws/applications on a real local port.</summary>
public sealed class FakeUpstream : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly TaskCompletionSource<WebSocket> _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Url { get; private set; } = string.Empty;
    public string? ReceivedToken { get; private set; }
    public List<string> ReceivedFromBrowser { get; } = [];
    public TaskCompletionSource<WebSocketCloseStatus?> ClosedByBrowser { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private FakeUpstream(WebApplication app) => _app = app;

    public static async Task<FakeUpstream> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        var fake = new FakeUpstream(app);

        app.UseWebSockets();
        app.Map("/ws/applications", async (HttpContext context) =>
        {
            fake.ReceivedToken = context.Request.Query["access_token"];
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            fake._connected.TrySetResult(socket);

            var buffer = new byte[1024];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    fake.ClosedByBrowser.TrySetResult(result.CloseStatus);
                    break;
                }

                string text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                fake.ReceivedFromBrowser.Add(text);
                if (text == "ping")
                    await socket.SendAsync(Encoding.UTF8.GetBytes("""{"type":"pong"}"""), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        });

        await app.StartAsync();
        fake.Url = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return fake;
    }

    public async Task<WebSocket> WaitForConnectionAsync() => await _connected.Task.WaitAsync(TimeSpan.FromSeconds(5));

    public async Task SendAsync(string text) =>
        await (await WaitForConnectionAsync()).SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);

    public async Task CloseAsync(WebSocketCloseStatus status, string reason) =>
        await (await WaitForConnectionAsync()).CloseOutputAsync(status, reason, CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

public class ProxyFactory : WebFactory
{
    public string UpstreamUrl { get; init; } = "http://127.0.0.1:1"; // nothing listens there

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.PostConfigure<SocketProxyOptions>(o => o.ApiBaseUrl = UpstreamUrl));
    }
}

public class SocketProxyTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    /// <summary>Logs a browser in through the real form and returns the Cookie header value for the socket handshake.</summary>
    private static async Task<(string Cookie, string Token)> LoginCookieAsync(ProxyFactory factory, UserRole role)
    {
        var client = factory.CreateBrowser();
        var result = WebFactory.Result(role);
        factory.Api.Reset();
        factory.Api.Setup(a => a.LoginAsync(It.IsAny<Praktikaportaal.Core.DTOs.LoginDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        factory.Api.Setup(a => a.GetNotificationsAsync(It.IsAny<Praktikaportaal.Core.DTOs.NotificationQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Praktikaportaal.Core.DTOs.NotificationListDto()); // pages rendered for a logged-in user show the unread badge

        var response = await WebFactory.PostFormAsync(client, "/Account/Login", new() { ["Email"] = "u@test.ee", ["Password"] = "Passw0rd1" });
        string setCookie = response.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("Praktikaportaal.Auth="));
        return (setCookie.Split(';')[0], result.Token);
    }

    private static Task<WebSocket> ConnectAsync(ProxyFactory factory, string? cookie, string? origin = null)
    {
        var client = factory.Server.CreateWebSocketClient();
        client.ConfigureRequest = request =>
        {
            if (cookie != null) request.Headers["Cookie"] = cookie;
            if (origin != null) request.Headers["Origin"] = origin;
        };
        return client.ConnectAsync(new Uri("ws://localhost/ws/applications"), CancellationToken.None);
    }

    private static async Task<string?> ReceiveAsync(WebSocket socket)
    {
        using var cts = new CancellationTokenSource(Wait);
        var buffer = new byte[4096];
        var result = await socket.ReceiveAsync(buffer, cts.Token);
        return result.MessageType == WebSocketMessageType.Close ? null : Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    [Fact]
    public async Task Company_ReceivesTheApisMessages_AndTheTokenIsSentUpstream()
    {
        await using var upstream = await FakeUpstream.StartAsync();
        using var factory = new ProxyFactory { UpstreamUrl = upstream.Url };
        var (cookie, token) = await LoginCookieAsync(factory, UserRole.Company);

        using var browser = await ConnectAsync(factory, cookie, "http://localhost");
        await upstream.SendAsync("""{"type":"ApplicationReceived","studentName":"Mari Tamm"}""");

        Assert.Equal("""{"type":"ApplicationReceived","studentName":"Mari Tamm"}""", await ReceiveAsync(browser));
        Assert.Equal(token, upstream.ReceivedToken);   // the site, not the browser, holds the JWT
    }

    [Fact]
    public async Task Heartbeat_PingGoesUpstream_PongComesBack()
    {
        await using var upstream = await FakeUpstream.StartAsync();
        using var factory = new ProxyFactory { UpstreamUrl = upstream.Url };
        var (cookie, _) = await LoginCookieAsync(factory, UserRole.Company);
        using var browser = await ConnectAsync(factory, cookie);
        await upstream.WaitForConnectionAsync();

        await browser.SendAsync(Encoding.UTF8.GetBytes("ping"), WebSocketMessageType.Text, true, CancellationToken.None);

        Assert.Equal("""{"type":"pong"}""", await ReceiveAsync(browser));
        Assert.Contains("ping", upstream.ReceivedFromBrowser);
    }

    [Fact]
    public async Task UpstreamClosingWithPolicyViolation_IsPassedToTheBrowser()
    {
        await using var upstream = await FakeUpstream.StartAsync();
        using var factory = new ProxyFactory { UpstreamUrl = upstream.Url };
        var (cookie, _) = await LoginCookieAsync(factory, UserRole.Company);
        using var browser = await ConnectAsync(factory, cookie);
        await upstream.WaitForConnectionAsync();

        await upstream.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Token expired"); // what the API does when the JWT expires

        Assert.Null(await ReceiveAsync(browser));
        Assert.Equal(WebSocketCloseStatus.PolicyViolation, browser.CloseStatus);
        Assert.Equal("Token expired", browser.CloseStatusDescription);
    }

    [Fact]
    public async Task BrowserClosing_ClosesTheUpstreamSocketToo()
    {
        await using var upstream = await FakeUpstream.StartAsync();
        using var factory = new ProxyFactory { UpstreamUrl = upstream.Url };
        var (cookie, _) = await LoginCookieAsync(factory, UserRole.Company);
        var browser = await ConnectAsync(factory, cookie);
        await upstream.WaitForConnectionAsync();

        await browser.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);

        Assert.Equal(WebSocketCloseStatus.NormalClosure, await upstream.ClosedByBrowser.Task.WaitAsync(Wait));
    }

    [Fact]
    public async Task Anonymous_IsRejected_AndNothingIsOpenedUpstream()
    {
        await using var upstream = await FakeUpstream.StartAsync();
        using var factory = new ProxyFactory { UpstreamUrl = upstream.Url };

        await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync(factory, cookie: null));

        Assert.Null(upstream.ReceivedToken);
    }

    [Theory]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Admin)]
    public async Task NonCompanyRoles_AreRejected(UserRole role)
    {
        await using var upstream = await FakeUpstream.StartAsync();
        using var factory = new ProxyFactory { UpstreamUrl = upstream.Url };
        var (cookie, _) = await LoginCookieAsync(factory, role);

        await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync(factory, cookie));

        Assert.Null(upstream.ReceivedToken);
    }

    [Fact]
    public async Task ForeignOrigin_IsRejected_EvenWithAValidCookie()
    {
        await using var upstream = await FakeUpstream.StartAsync();
        using var factory = new ProxyFactory { UpstreamUrl = upstream.Url };
        var (cookie, _) = await LoginCookieAsync(factory, UserRole.Company);

        // A page on another site making the visitor's browser open the socket (cookies would be sent along).
        await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync(factory, cookie, "https://evil.example"));

        Assert.Null(upstream.ReceivedToken);
    }

    [Fact]
    public async Task UpstreamDown_RefusesTheHandshake_SoTheBrowserCanRetry()
    {
        using var factory = new ProxyFactory(); // upstream: a closed port
        var (cookie, _) = await LoginCookieAsync(factory, UserRole.Company);

        await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync(factory, cookie, "http://localhost"));
    }

    [Fact]
    public async Task PlainHttpRequest_IsBadRequest()
    {
        using var factory = new ProxyFactory();
        var (cookie, _) = await LoginCookieAsync(factory, UserRole.Company);
        var client = factory.CreateBrowser();
        client.DefaultRequestHeaders.Add("Cookie", cookie);

        var response = await client.GetAsync("/ws/applications");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public class SocketProxyHelpersTests
{
    private static HttpRequest Request(string host, string? origin)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        if (origin != null) context.Request.Headers.Origin = origin;
        return context.Request;
    }

    [Theory]
    [InlineData("localhost:5026", "http://localhost:5026", true)]
    [InlineData("localhost:5026", "HTTP://LOCALHOST:5026", true)]
    [InlineData("localhost:5026", null, true)]                        // not a browser: the cookie is still required
    [InlineData("localhost:5026", "http://evil.example", false)]
    [InlineData("localhost:5026", "http://localhost:9999", false)]    // another port is another origin
    [InlineData("localhost:5026", "http://localhost:5026.evil.example", false)]
    [InlineData("localhost:5026", "not a url", false)]
    public void SameOriginCheck(string host, string? origin, bool expected) =>
        Assert.Equal(expected, ApplicationsSocketProxy.IsSameOrigin(Request(host, origin)));

    [Theory]
    [InlineData("http://localhost:5117", "ws://localhost:5117/ws/applications?access_token=abc")]
    [InlineData("https://api.example.ee/", "wss://api.example.ee/ws/applications?access_token=abc")]
    public void UpstreamUri_UsesWsOrWss(string apiBase, string expected) =>
        Assert.Equal(expected, ApplicationsSocketProxy.UpstreamUri(apiBase, "abc").ToString());

    [Fact]
    public void UpstreamUri_EscapesTheToken() =>
        Assert.Contains("access_token=a%2Bb%26c%3D", ApplicationsSocketProxy.UpstreamUri("http://x", "a+b&c=").Query);
}
