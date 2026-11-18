using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Praktikaportaal.API.WebSockets;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Interfaces;
using Xunit;

namespace Praktikaportaal.Tests.Integration;

/// <summary>/ws/applications end to end against the in-memory API.</summary>
public class WebSocketTests : IClassFixture<ApiFactory>
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);
    private readonly ApiFactory _factory;

    public WebSocketTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private Task<WebSocket> ConnectAsync(string? token)
    {
        var uri = new UriBuilder(_factory.Server.BaseAddress)
        {
            Scheme = "ws",
            Path = ApplicationsSocketEndpoint.Path,
            Query = token == null ? string.Empty : "access_token=" + Uri.EscapeDataString(token)
        }.Uri;
        return _factory.Server.CreateWebSocketClient().ConnectAsync(uri, CancellationToken.None);
    }

    private static async Task<string?> ReceiveAsync(WebSocket socket, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var buffer = new byte[4096];
        try
        {
            var result = await socket.ReceiveAsync(buffer, cts.Token);
            return result.MessageType == WebSocketMessageType.Close ? null : Encoding.UTF8.GetString(buffer, 0, result.Count);
        }
        catch (OperationCanceledException)
        {
            return null; // nothing arrived in time
        }
    }

    private static string TypeOf(string json) => JsonDocument.Parse(json).RootElement.GetProperty("type").GetString()!;

    [Fact]
    public async Task Company_ReceivesApplicationReceived_WhenStudentApplies()
    {
        var (company, companyAuth) = await _factory.RegisterCompanyAsync("ws-company@test.ee");
        var internship = await ApiFactory.CreateInternshipAsync(company, "Live Intern");
        var (student, _) = await _factory.RegisterStudentAsync("ws-student@test.ee");

        using var socket = await ConnectAsync(companyAuth.Token);
        Assert.Equal("Connected", TypeOf((await ReceiveAsync(socket, Wait))!));

        var applied = await student.PostAsJsonAsync($"/api/internships/{internship.Id}/applications",
            new CreateApplicationDto { CoverLetter = "Hello" }, ApiFactory.Json);
        applied.EnsureSuccessStatusCode();
        var application = (await applied.Content.ReadFromJsonAsync<ApplicationDto>(ApiFactory.Json))!;

        var message = JsonDocument.Parse((await ReceiveAsync(socket, Wait))!).RootElement;
        Assert.Equal("ApplicationReceived", message.GetProperty("type").GetString());
        Assert.Equal(application.Id, message.GetProperty("applicationId").GetGuid());
        Assert.Equal(internship.Id, message.GetProperty("internshipId").GetGuid());
        Assert.Equal("Live Intern", message.GetProperty("internshipTitle").GetString());
        Assert.Equal("Test Student", message.GetProperty("studentName").GetString());
        Assert.True(message.TryGetProperty("createdAt", out _));
    }

    [Fact]
    public async Task OtherCompany_DoesNotReceiveTheMessage()
    {
        var (owner, _) = await _factory.RegisterCompanyAsync("owner@test.ee");
        var (_, otherAuth) = await _factory.RegisterCompanyAsync("other@test.ee");
        var internship = await ApiFactory.CreateInternshipAsync(owner);
        var (student, _) = await _factory.RegisterStudentAsync("iso-student@test.ee");

        using var otherSocket = await ConnectAsync(otherAuth.Token);
        await ReceiveAsync(otherSocket, Wait); // "Connected"

        (await student.PostAsJsonAsync($"/api/internships/{internship.Id}/applications", new CreateApplicationDto(), ApiFactory.Json))
            .EnsureSuccessStatusCode();

        Assert.Null(await ReceiveAsync(otherSocket, TimeSpan.FromMilliseconds(600)));
    }

    [Fact]
    public async Task AllOpenConnectionsOfTheCompany_AreNotified()
    {
        var (company, auth) = await _factory.RegisterCompanyAsync("multi@test.ee");
        var internship = await ApiFactory.CreateInternshipAsync(company);
        var (student, _) = await _factory.RegisterStudentAsync("multi-student@test.ee");

        using var tab1 = await ConnectAsync(auth.Token);
        using var tab2 = await ConnectAsync(auth.Token);
        await ReceiveAsync(tab1, Wait);
        await ReceiveAsync(tab2, Wait);

        (await student.PostAsJsonAsync($"/api/internships/{internship.Id}/applications", new CreateApplicationDto(), ApiFactory.Json))
            .EnsureSuccessStatusCode();

        Assert.Equal("ApplicationReceived", TypeOf((await ReceiveAsync(tab1, Wait))!));
        Assert.Equal("ApplicationReceived", TypeOf((await ReceiveAsync(tab2, Wait))!));
    }

    [Fact]
    public async Task Connect_WithoutToken_IsRejected() =>
        await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync(null));

    [Fact]
    public async Task Connect_WithGarbageToken_IsRejected() =>
        await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync("not-a-jwt"));

    [Fact]
    public async Task Connect_AsStudent_IsRejected_CompaniesOnly()
    {
        var (_, studentAuth) = await _factory.RegisterStudentAsync("ws-reject@test.ee");

        await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync(studentAuth.Token));
    }

    [Fact]
    public async Task PlainHttpRequest_ToSocketEndpoint_IsBadRequest()
    {
        var (_, auth) = await _factory.RegisterCompanyAsync("plain-http@test.ee");

        var response = await _factory.CreateClient().GetAsync($"{ApplicationsSocketEndpoint.Path}?access_token={auth.Token}");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ping_GetsPong()
    {
        var (_, auth) = await _factory.RegisterCompanyAsync("ping@test.ee");
        using var socket = await ConnectAsync(auth.Token);
        await ReceiveAsync(socket, Wait); // Connected

        await socket.SendAsync(Encoding.UTF8.GetBytes("ping"), WebSocketMessageType.Text, true, CancellationToken.None);

        Assert.Equal("pong", TypeOf((await ReceiveAsync(socket, Wait))!));
    }

    [Fact]
    public async Task ClosedConnection_IsRemovedFromTheGroup()
    {
        var (_, auth) = await _factory.RegisterCompanyAsync("close@test.ee");
        var groups = _factory.Services.GetRequiredService<ConnectionGroups>();
        string group = ConnectionGroups.CompanyGroup(auth.User.ProfileId!.Value);

        var socket = await ConnectAsync(auth.Token);
        await ReceiveAsync(socket, Wait);
        Assert.Equal(1, groups.Count(group));

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        socket.Dispose();

        for (int i = 0; i < 50 && groups.Count(group) > 0; i++)
            await Task.Delay(50);
        Assert.Equal(0, groups.Count(group));
    }
}

/// <summary>The notifier must never fail the request that saved the application.</summary>
public class WebSocketNotifierTests
{
    [Fact]
    public async Task BrokenConnection_DoesNotThrow_AndOthersStillGetTheMessage()
    {
        var companyId = Guid.NewGuid();
        var groups = new ConnectionGroups();

        var broken = new Mock<WebSocket>();
        broken.SetupGet(s => s.State).Returns(WebSocketState.Open);
        broken.Setup(s => s.SendAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new WebSocketException("connection reset"));
        var healthy = new Mock<WebSocket>();
        healthy.SetupGet(s => s.State).Returns(WebSocketState.Open);
        healthy.Setup(s => s.SendAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
               .Returns(ValueTask.CompletedTask.AsTask());

        groups.Add(ConnectionGroups.CompanyGroup(companyId), new SocketConnection(broken.Object));
        groups.Add(ConnectionGroups.CompanyGroup(companyId), new SocketConnection(healthy.Object));
        var notifier = new WebSocketApplicationNotifier(groups, Microsoft.Extensions.Logging.Abstractions.NullLogger<WebSocketApplicationNotifier>.Instance);

        await notifier.ApplicationReceivedAsync(new ApplicationReceivedEvent(companyId, Guid.NewGuid(), Guid.NewGuid(), "Intern", "Mari Tamm", DateTime.UtcNow));

        healthy.Verify(s => s.SendAsync(It.IsAny<ArraySegment<byte>>(), WebSocketMessageType.Text, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NoConnections_IsANoOp()
    {
        var notifier = new WebSocketApplicationNotifier(new ConnectionGroups(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WebSocketApplicationNotifier>.Instance);

        await notifier.ApplicationReceivedAsync(new ApplicationReceivedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "x", "y", DateTime.UtcNow));
    }

    [Fact]
    public void Groups_AreSeparatedPerCompany()
    {
        var groups = new ConnectionGroups();
        var a = new SocketConnection(Mock.Of<WebSocket>(s => s.State == WebSocketState.Open));
        var b = new SocketConnection(Mock.Of<WebSocket>(s => s.State == WebSocketState.Open));
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        groups.Add(ConnectionGroups.CompanyGroup(companyA), a);
        groups.Add(ConnectionGroups.CompanyGroup(companyB), b);

        Assert.Equal([a], groups.GetOpen(ConnectionGroups.CompanyGroup(companyA)));
        groups.Remove(ConnectionGroups.CompanyGroup(companyA), a);
        Assert.Empty(groups.GetOpen(ConnectionGroups.CompanyGroup(companyA)));
        Assert.Single(groups.GetOpen(ConnectionGroups.CompanyGroup(companyB)));
    }
}
