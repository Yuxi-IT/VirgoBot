using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using VirgoBot.Configuration;
using VirgoBot.Helpers;

namespace VirgoBot.Integrations.ILink;

public sealed class ILinkBridgeService
{
    private readonly ILinkConfig _config;
    private readonly HttpClient _httpClient;
    private ClientWebSocket? _socket;

    public ILinkBridgeService(ILinkConfig config)
    {
        _config = config;
        _httpClient = new HttpClient();

        if (!string.IsNullOrWhiteSpace(_config.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _config.Token);
        }
    }

    public bool IsEnabled => _config.Enabled;

    public Task StartAsync(Func<ILinkIncomingMessage, Task> onMessage, CancellationToken cancellationToken)
    {
        var webSocketUrl = NormalizeWebSocketUrl(_config.WebSocketUrl);

        if (!_config.Enabled || string.IsNullOrWhiteSpace(webSocketUrl))
        {
            return Task.CompletedTask;
        }

        _ = Task.Run(() => RunWebSocketLoopAsync(webSocketUrl, onMessage, cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    public async Task SendMessageAsync(string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_config.SendUrl))
        {
            throw new InvalidOperationException("iLink send url is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _config.SendUrl);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { content }),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendLongMessageAsync(string text, CancellationToken cancellationToken = default)
    {
        var paragraphs = text.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);

        foreach (var paragraph in paragraphs)
        {
            var trimmed = paragraph.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            Console.WriteLine($"[ILINK] {trimmed}");
            await SendMessageAsync(trimmed, cancellationToken);
            await Task.Delay(300, cancellationToken);
        }
    }

    public bool TryParseIncoming(string json, out ILinkIncomingMessage? message)
    {
        message = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var type = GetString(root, "type");
            if (string.Equals(type, "send", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "pong", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var eventType = GetNestedString(root, "event", "type");
            if (string.Equals(type, "event", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(eventType, "message.text", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var content = GetString(root, "content")
                ?? GetString(root, "message")
                ?? GetString(root, "text")
                ?? GetNestedString(root, "event", "data", "content")
                ?? GetNestedString(root, "event", "data", "text");

            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            var userId = GetString(root, "userId")
                ?? GetString(root, "from")
                ?? GetString(root, "sender")
                ?? GetNestedString(root, "user", "id")
                ?? GetNestedString(root, "sender", "id")
                ?? GetNestedString(root, "event", "data", "sender", "id")
                ?? _config.DefaultUserId;

            var username = GetString(root, "username")
                ?? GetString(root, "nickname")
                ?? GetNestedString(root, "user", "name")
                ?? GetNestedString(root, "event", "data", "sender", "id")
                ?? userId;

            message = new ILinkIncomingMessage(userId, username, content, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task RunWebSocketLoopAsync(string webSocketUrl, Func<ILinkIncomingMessage, Task> onMessage, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ClientWebSocket? socket = null;

            try
            {
                socket = new ClientWebSocket();
                _socket = socket;
                await socket.ConnectAsync(new Uri(webSocketUrl), cancellationToken);
                ColorLog.Success("ILINK", "WebSocket connected");

                while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var json = await ReceiveTextAsync(socket, cancellationToken);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        continue;
                    }

                    if (TryParseIncoming(json, out var message) && message is not null)
                    {
                        await onMessage(message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ColorLog.Error("ILINK", $"WebSocket error: {ex.Message}");
            }
            finally
            {
                socket?.Dispose();

                if (ReferenceEquals(_socket, socket))
                {
                    _socket = null;
                }
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }
    }

    private static async Task<string> ReceiveTextAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new ArraySegment<byte>(new byte[4096]);
        using var ms = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return string.Empty;
            }

            ms.Write(buffer.Array!, buffer.Offset, result.Count);

            if (result.EndOfMessage)
            {
                break;
            }
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string NormalizeWebSocketUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var normalized = url.Trim();

        if (normalized.StartsWith("wss://http://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "ws://" + normalized["wss://http://".Length..];
        }
        else if (normalized.StartsWith("ws://https://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "wss://" + normalized["ws://https://".Length..];
        }
        else if (normalized.StartsWith("ws://http://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "ws://" + normalized["ws://http://".Length..];
        }
        else if (normalized.StartsWith("wss://https://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "wss://" + normalized["wss://https://".Length..];
        }

        return normalized;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static string? GetNestedString(JsonElement element, string propertyName, string nestedPropertyName)
    {
        if (!element.TryGetProperty(propertyName, out var nested) || nested.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetString(nested, nestedPropertyName);
    }

    private static string? GetNestedString(
        JsonElement element,
        string propertyName,
        string nestedPropertyName,
        string targetPropertyName)
    {
        if (!element.TryGetProperty(propertyName, out var nested) || nested.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!nested.TryGetProperty(nestedPropertyName, out var target) || target.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetString(target, targetPropertyName);
    }

    private static string? GetNestedString(
        JsonElement element,
        string propertyName,
        string nestedPropertyName,
        string targetPropertyName,
        string finalPropertyName)
    {
        if (!element.TryGetProperty(propertyName, out var nested) || nested.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!nested.TryGetProperty(nestedPropertyName, out var target) || target.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!target.TryGetProperty(targetPropertyName, out var final) || final.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetString(final, finalPropertyName);
    }
}

public sealed record ILinkIncomingMessage(string UserId, string Username, string Content, string RawJson);
