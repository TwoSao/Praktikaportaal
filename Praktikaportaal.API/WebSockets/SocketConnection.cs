using System.Net.WebSockets;
using System.Text;

namespace Praktikaportaal.API.WebSockets;

/// <summary>
/// One open WebSocket. A WebSocket allows only one send at a time, so sends are serialized.
/// </summary>
public sealed class SocketConnection
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public SocketConnection(WebSocket socket)
    {
        Socket = socket;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public WebSocket Socket { get; }

    public bool IsOpen => Socket.State == WebSocketState.Open;

    public async Task SendTextAsync(string text, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct);
        try
        {
            if (IsOpen)
                await Socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, endOfMessage: true, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }
}
