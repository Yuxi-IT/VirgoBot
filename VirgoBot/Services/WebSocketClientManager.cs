using System.Net.WebSockets;
using System.Text;

namespace VirgoBot.Services;

public class WebSocketClientManager
{
    private readonly List<WebSocket> _clients = new();
    private readonly Lock _lock = new();

    public void Add(WebSocket ws)
    {
        lock (_lock)
        {
            _clients.Add(ws);
        }
    }

    public void Remove(WebSocket ws)
    {
        lock (_lock)
        {
            _clients.Remove(ws);
        }
    }

    public List<WebSocket> GetSnapshot()
    {
        lock (_lock)
        {
            return new List<WebSocket>(_clients);
        }
    }

    public async Task BroadcastAsync(string message, CancellationToken cancellationToken = default)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(buffer);
        var snapshot = GetSnapshot();

        foreach (var ws in snapshot)
        {
            if (ws.State != WebSocketState.Open) continue;

            try
            {
                await ws.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken);
            }
            catch
            {
                Remove(ws);
            }
        }
    }

    public async Task BroadcastAsync(byte[] buffer, CancellationToken cancellationToken = default)
    {
        var segment = new ArraySegment<byte>(buffer);
        var snapshot = GetSnapshot();

        foreach (var ws in snapshot)
        {
            if (ws.State != WebSocketState.Open) continue;

            try
            {
                await ws.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken);
            }
            catch
            {
                Remove(ws);
            }
        }
    }
}
