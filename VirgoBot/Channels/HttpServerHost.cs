using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VirgoBot.Channels.Handlers;
using VirgoBot.Configuration;
using VirgoBot.Contracts;
using VirgoBot.Services;
using VirgoBot.Utilities;

namespace VirgoBot.Channels;

public class HttpServerHost
{
    private readonly Gateway _gateway;
    private readonly WebSocketClientManager _wsManager;
    private readonly MemoryService _memoryService;
    private readonly LogService _logService;
    private readonly ILinkLoginService _iLinkLoginService;

    private readonly ContactApiHandler _contactApiHandler;
    private readonly ConfigApiHandler _configApiHandler;
    private readonly SkillApiHandler _skillApiHandler;
    private readonly AgentApiHandler _agentApiHandler;
    private readonly ChannelApiHandler _channelApiHandler;
    private readonly SessionApiHandler _sessionApiHandler;
    private readonly StatusApiHandler _statusApiHandler;
    private readonly ScheduledTaskApiHandler _taskApiHandler;
    private readonly ScheduledTaskService _taskService;
    private readonly VoiceApiHandler _voiceApiHandler;
    private readonly ProviderApiHandler _providerApiHandler;
    private readonly McpApiHandler _mcpApiHandler;

    // Public-facing port (TcpListener on 0.0.0.0) — no admin/URL ACL needed
    private const int PublicPort = 8765;
    // Internal HttpListener port (127.0.0.1 only)
    private const int InternalPort = 8766;

    public HttpServerHost(
        Gateway gateway,
        WebSocketClientManager wsManager,
        MemoryService memoryService,
        LogService logService)
    {
        _gateway = gateway;
        _wsManager = wsManager;
        _memoryService = memoryService;
        _logService = logService;
        _iLinkLoginService = new ILinkLoginService();
        _taskService = gateway.ScheduledTaskService;

        _contactApiHandler = new ContactApiHandler(gateway);
        _configApiHandler = new ConfigApiHandler(gateway, memoryService);
        _skillApiHandler = new SkillApiHandler(gateway);
        _agentApiHandler = new AgentApiHandler(gateway, memoryService);
        _channelApiHandler = new ChannelApiHandler(gateway, _iLinkLoginService);
        _sessionApiHandler = new SessionApiHandler(gateway, memoryService);
        _statusApiHandler = new StatusApiHandler(gateway, memoryService, logService, wsManager);
        _taskApiHandler = new ScheduledTaskApiHandler(_taskService);
        _voiceApiHandler = new VoiceApiHandler();
        _providerApiHandler = new ProviderApiHandler(gateway);
        _mcpApiHandler = new McpApiHandler(gateway);
    }

    public async Task StartAsync()
    {
        // Start internal HttpListener on localhost only (no URL ACL needed for 127.0.0.1)
        var internalListener = new HttpListener();
        internalListener.Prefixes.Add($"http://127.0.0.1:{InternalPort}/");
        internalListener.Start();

        // Accept and dispatch internal HTTP requests
        _ = Task.Run(async () =>
        {
            while (true)
            {
                var ctx = await internalListener.GetContextAsync();
                _ = Task.Run(async () =>
                {
                    try { await DispatchRequest(ctx); }
                    catch (Exception ex)
                    {
                        ColorLog.Error("HTTP", $"请求处理失败: {ex.Message}");
                        try { await HttpResponseHelper.SendErrorResponse(ctx, 500, ex.Message); } catch { ctx.Response.StatusCode = 500; }
                    }
                    finally { ctx.Response.Close(); }
                });
            }
        });

        // Public TcpListener on 0.0.0.0 — no admin privileges required
        var tcpListener = new TcpListener(IPAddress.Any, PublicPort);
        tcpListener.Start();
        ColorLog.Success("HTTP", $"HTTP 服务已启动 — 端口 {PublicPort} (公网可访问)");

        while (true)
        {
            var client = await tcpListener.AcceptTcpClientAsync();
            _ = Task.Run(() => HandleTcpClient(client));
        }
    }

    private async Task HandleTcpClient(TcpClient client)
    {
        try
        {
            client.NoDelay = true;
            using var stream = client.GetStream();

            // Read the HTTP request headers into a buffer
            var headerBytes = await ReadHttpHeadersAsync(stream);
            if (headerBytes == null) return;

            var headerText = Encoding.UTF8.GetString(headerBytes);

            // Check if this is a WebSocket upgrade request
            if (headerText.Contains("Upgrade: websocket", StringComparison.OrdinalIgnoreCase) ||
                headerText.Contains("Upgrade:websocket", StringComparison.OrdinalIgnoreCase))
            {
                await HandleRawWebSocket(client, stream, headerText, headerBytes);
                return;
            }

            // Regular HTTP — proxy to internal HttpListener
            await ProxyToInternalListener(client, stream, headerBytes);
        }
        catch (Exception ex)
        {
            ColorLog.Error("TCP", $"客户端处理失败: {ex.Message}");
        }
        finally
        {
            client.Close();
        }
    }

    /// <summary>
    /// Reads HTTP headers until \r\n\r\n, returns the full header bytes (including the blank line).
    /// </summary>
    private static async Task<byte[]?> ReadHttpHeadersAsync(NetworkStream stream)
    {
        var buf = new List<byte>(4096);
        var tmp = new byte[1];
        while (true)
        {
            int n;
            try { n = await stream.ReadAsync(tmp, 0, 1); }
            catch { return null; }
            if (n == 0) return null;
            buf.Add(tmp[0]);
            if (buf.Count >= 4 &&
                buf[^4] == '\r' && buf[^3] == '\n' &&
                buf[^2] == '\r' && buf[^1] == '\n')
                return buf.ToArray();
            if (buf.Count > 65536) return null; // safety limit
        }
    }

    private async Task ProxyToInternalListener(TcpClient client, NetworkStream clientStream, byte[] headerBytes)
    {
        try
        {
            // Parse Content-Length from headers to know if there's a body
            var headerText = Encoding.UTF8.GetString(headerBytes);
            var contentLength = 0;
            var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var len))
                        contentLength = len;
                }
            }

            // Read body if present
            byte[]? bodyBytes = null;
            if (contentLength > 0)
            {
                bodyBytes = new byte[contentLength];
                var totalRead = 0;
                while (totalRead < contentLength)
                {
                    var n = await clientStream.ReadAsync(bodyBytes, totalRead, contentLength - totalRead);
                    if (n == 0) break;
                    totalRead += n;
                }
            }

            // Forward to internal HttpListener
            using var httpClient = new HttpClient();
            var requestLine = lines[0].Split(' ');
            if (requestLine.Length < 3) return;

            var method = requestLine[0];
            var path = requestLine[1];
            var url = $"http://127.0.0.1:{InternalPort}{path}";

            HttpResponseMessage response;
            if (method == "GET" || method == "DELETE")
            {
                var request = new HttpRequestMessage(new HttpMethod(method), url);
                response = await httpClient.SendAsync(request);
            }
            else if (method == "POST" || method == "PUT")
            {
                var content = bodyBytes != null ? new ByteArrayContent(bodyBytes) : new ByteArrayContent(Array.Empty<byte>());
                // Copy Content-Type header if present
                foreach (var line in lines)
                {
                    if (line.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(new[] { ':' }, 2);
                        if (parts.Length == 2)
                            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(parts[1].Trim());
                    }
                }
                response = method == "POST"
                    ? await httpClient.PostAsync(url, content)
                    : await httpClient.PutAsync(url, content);
            }
            else if (method == "OPTIONS")
            {
                var request = new HttpRequestMessage(HttpMethod.Options, url);
                response = await httpClient.SendAsync(request);
            }
            else
            {
                return; // Unsupported method
            }

            // Send response back to client
            var statusLine = $"HTTP/1.1 {(int)response.StatusCode} {response.ReasonPhrase}\r\n";
            await clientStream.WriteAsync(Encoding.UTF8.GetBytes(statusLine));

            // Write response headers
            foreach (var header in response.Headers)
            {
                foreach (var value in header.Value)
                    await clientStream.WriteAsync(Encoding.UTF8.GetBytes($"{header.Key}: {value}\r\n"));
            }
            foreach (var header in response.Content.Headers)
            {
                foreach (var value in header.Value)
                    await clientStream.WriteAsync(Encoding.UTF8.GetBytes($"{header.Key}: {value}\r\n"));
            }
            await clientStream.WriteAsync(Encoding.UTF8.GetBytes("\r\n"));

            // Write response body
            var responseBody = await response.Content.ReadAsByteArrayAsync();
            if (responseBody.Length > 0)
                await clientStream.WriteAsync(responseBody);

            await clientStream.FlushAsync();
        }
        catch (Exception ex)
        {
            ColorLog.Error("PROXY", $"代理失败: {ex.Message}");
        }
    }

    private async Task HandleRawWebSocket(TcpClient client, NetworkStream stream, string headerText, byte[] headerBytes)
    {
        // Perform WebSocket handshake manually
        var key = "";
        foreach (var line in headerText.Split(new[] { "\r\n" }, StringSplitOptions.None))
        {
            if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
            {
                key = line.Split(new[] { ':' }, 2)[1].Trim();
                break;
            }
        }

        if (string.IsNullOrEmpty(key))
        {
            ColorLog.Error("WS", "WebSocket 握手失败: 缺少 Sec-WebSocket-Key");
            return;
        }

        var acceptKey = Convert.ToBase64String(
            SHA1.HashData(Encoding.UTF8.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));

        var handshake = "HTTP/1.1 101 Switching Protocols\r\n" +
                        "Upgrade: websocket\r\n" +
                        "Connection: Upgrade\r\n" +
                        $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n";

        await stream.WriteAsync(Encoding.UTF8.GetBytes(handshake));
        await stream.FlushAsync();

        // Wrap in a managed WebSocket
        var ws = WebSocket.CreateFromStream(stream, new WebSocketCreationOptions
        {
            IsServer = true,
            KeepAliveInterval = TimeSpan.FromSeconds(30)
        });

        _wsManager.Add(ws);
        //ColorLog.Success("WS", "客户端已连接");

        var buffer = new byte[AppConstants.WebSocketBufferSize];
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    break;
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessWebSocketMessage(ws, msg);
                }
            }
        }
        catch (Exception ex)
        {
            ColorLog.Error("WS", $"WebSocket 错误: {ex.Message}");
        }
        finally
        {
            _wsManager.Remove(ws);
            //ColorLog.Info("WS", "客户端已断开");
        }
    }

    private async Task ProcessWebSocketMessage(WebSocket ws, string msg)
    {
        JsonElement req;
        try
        {
            req = JsonSerializer.Deserialize<JsonElement>(msg);
        }
        catch (JsonException ex)
        {
            ColorLog.Error("WS", $"收到无效 JSON: {ex.Message}");
            return;
        }

        if (req.TryGetProperty("type", out var typeEl))
        {
            var type = typeEl.GetString();

            if (type == "message")
            {
                var message = req.GetProperty("message").GetString();
                ColorLog.Info("→AI", $"{message}");

                _gateway.ActivityMonitor?.UpdateActivity();
                var reply = await _gateway.LlmService.AskAsync(message ?? "");
                ColorLog.Success("AI→", reply);

                var response = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "sendMessage", content = reply }));
                await ws.SendAsync(new ArraySegment<byte>(response), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else if (type == "aiSwitchChat")
            {
                var username = req.GetProperty("username").GetString();
                var response = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "switchChat", username }));
                await ws.SendAsync(new ArraySegment<byte>(response), WebSocketMessageType.Text, true, CancellationToken.None);
                ColorLog.Success("SWITCH", $"切换到 {username}");
            }
        }
        else if (req.TryGetProperty("message", out var msgEl))
        {
            var chatReq = JsonSerializer.Deserialize<ChatRequest>(msg);
            ColorLog.Info("MSG-WS", $"'{chatReq?.message ?? ""}'");

            _gateway.ActivityMonitor?.UpdateActivity();
            var reply = await _gateway.LlmService.AskAsync(chatReq?.message ?? "");

            var response = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "reply", content = reply }));
            await ws.SendAsync(new ArraySegment<byte>(response), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    private async Task DispatchRequest(HttpListenerContext ctx)
    {
        ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        ctx.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
        ctx.Response.Headers.Add("Access-Control-Allow-Headers", "*");

        var path = ctx.Request.Url?.AbsolutePath;
        var method = ctx.Request.HttpMethod;

        if (method == "OPTIONS") { ctx.Response.StatusCode = 200; return; }

        if (path == "/chat" && method == "POST") { await HandleChatRequest(ctx); return; }
        if (path == "/api/status" && method == "GET") { await _statusApiHandler.HandleStatusRequest(ctx); return; }
        if (path == "/api/token-stats" && method == "GET") { await _statusApiHandler.HandleTokenStatsRequest(ctx); return; }
        if (path == "/api/messages" && method == "GET") { await _statusApiHandler.HandleGetMessagesRequest(ctx); return; }
        if (path == "/api/contacts" && method == "GET") { await _contactApiHandler.HandleGetContactsRequest(ctx); return; }
        if (path == "/api/contacts" && method == "POST") { await _contactApiHandler.HandleAddContactRequest(ctx); return; }
        if (path?.StartsWith("/api/contacts/") == true && method == "PUT") { await _contactApiHandler.HandleUpdateContactRequest(ctx); return; }
        if (path?.StartsWith("/api/contacts/") == true && method == "DELETE") { await _contactApiHandler.HandleDeleteContactRequest(ctx); return; }
        if (path == "/api/config" && method == "GET") { await _configApiHandler.HandleGetConfigRequest(ctx); return; }
        if (path == "/api/config" && method == "PUT") { await _configApiHandler.HandleUpdateConfigRequest(ctx); return; }
        if (path == "/api/config/system-memory" && method == "GET") { await _configApiHandler.HandleGetSystemMemoryRequest(ctx); return; }
        if (path == "/api/config/system-memory" && method == "PUT") { await _configApiHandler.HandleUpdateSystemMemoryRequest(ctx); return; }
        if (path == "/api/config/soul" && method == "GET") { await _configApiHandler.HandleGetSoulRequest(ctx); return; }
        if (path == "/api/config/soul" && method == "PUT") { await _configApiHandler.HandleUpdateSoulRequest(ctx); return; }
        if (path == "/api/config/rule" && method == "GET") { await _configApiHandler.HandleGetRuleRequest(ctx); return; }
        if (path == "/api/config/rule" && method == "PUT") { await _configApiHandler.HandleUpdateRuleRequest(ctx); return; }
        if (path == "/api/logs" && method == "GET") { await _statusApiHandler.HandleGetLogsRequest(ctx); return; }
        if (path == "/api/logs" && method == "DELETE") { await _statusApiHandler.HandleClearLogsRequest(ctx); return; }
        if (path == "/api/skills" && method == "GET") { await _skillApiHandler.HandleGetSkillsRequest(ctx); return; }
        if (path == "/api/skills" && method == "POST") { await _skillApiHandler.HandleCreateSkillRequest(ctx); return; }
        if (path == "/api/skills/import" && method == "POST") { await _skillApiHandler.HandleImportSkillZipRequest(ctx); return; }
        if (path == "/api/skills/import-url" && method == "POST") { await _skillApiHandler.HandleImportSkillZipFromUrlRequest(ctx); return; }
        if (path?.StartsWith("/api/skills/") == true && method == "GET") { await _skillApiHandler.HandleGetSkillRequest(ctx); return; }
        if (path?.StartsWith("/api/skills/") == true && method == "PUT") { await _skillApiHandler.HandleUpdateSkillRequest(ctx); return; }
        if (path?.StartsWith("/api/skills/") == true && method == "DELETE") { await _skillApiHandler.HandleDeleteSkillRequest(ctx); return; }
        if (path == "/api/tasks" && method == "GET") { await _taskApiHandler.HandleGetTasksRequest(ctx); return; }
        if (path == "/api/tasks" && method == "POST") { await _taskApiHandler.HandleCreateTaskRequest(ctx); return; }
        if (path?.StartsWith("/api/tasks/") == true && path.EndsWith("/toggle") && method == "POST") { await _taskApiHandler.HandleToggleTaskRequest(ctx); return; }
        if (path?.StartsWith("/api/tasks/") == true && method == "GET") { await _taskApiHandler.HandleGetTaskRequest(ctx); return; }
        if (path?.StartsWith("/api/tasks/") == true && method == "PUT") { await _taskApiHandler.HandleUpdateTaskRequest(ctx); return; }
        if (path?.StartsWith("/api/tasks/") == true && method == "DELETE") { await _taskApiHandler.HandleDeleteTaskRequest(ctx); return; }
        if (path == "/api/gateway/restart" && method == "POST") { await _statusApiHandler.HandleGatewayRestartRequest(ctx); return; }
        if (path == "/api/gateway/status" && method == "GET") { await _statusApiHandler.HandleGatewayStatusRequest(ctx); return; }
        if (path == "/api/agents" && method == "GET") { await _agentApiHandler.HandleGetAgentsRequest(ctx); return; }
        if (path == "/api/agents" && method == "POST") { await _agentApiHandler.HandleCreateAgentRequest(ctx); return; }
        if (path == "/api/agents/generate" && method == "POST") { await _agentApiHandler.HandleGenerateAgentRequest(ctx); return; }
        if (path?.StartsWith("/api/agents/") == true && method == "GET") { await _agentApiHandler.HandleGetAgentRequest(ctx); return; }
        if (path?.StartsWith("/api/agents/") == true && method == "PUT") { await _agentApiHandler.HandleUpdateAgentRequest(ctx); return; }
        if (path?.StartsWith("/api/agents/") == true && method == "DELETE") { await _agentApiHandler.HandleDeleteAgentRequest(ctx); return; }
        if (path == "/api/config/agent" && method == "PUT") { await _agentApiHandler.HandleSwitchAgentRequest(ctx); return; }
        if (path == "/api/ilink/login/start" && method == "POST") { await _channelApiHandler.HandleStartILinkLoginRequest(ctx); return; }
        if (path == "/api/ilink/login/status" && method == "GET") { await _channelApiHandler.HandleGetILinkLoginStatusRequest(ctx); return; }
        if (path == "/api/soul" && method == "GET") { await _agentApiHandler.HandleGetSoulEntriesRequest(ctx); return; }
        if (path == "/api/soul" && method == "POST") { await _agentApiHandler.HandleAddSoulEntryRequest(ctx); return; }
        if (path?.StartsWith("/api/soul/") == true && method == "PUT") { await _agentApiHandler.HandleUpdateSoulEntryRequest(ctx); return; }
        if (path?.StartsWith("/api/soul/") == true && method == "DELETE") { await _agentApiHandler.HandleDeleteSoulEntryRequest(ctx); return; }
        if (path == "/api/config/channels" && method == "GET") { await _channelApiHandler.HandleGetChannelsConfigRequest(ctx); return; }
        if (path == "/api/config/channels" && method == "PUT") { await _channelApiHandler.HandleUpdateChannelsConfigRequest(ctx); return; }
        if (path == "/api/sessions" && method == "GET") { await _sessionApiHandler.HandleGetSessionsRequest(ctx); return; }
        if (path == "/api/sessions" && method == "POST") { await _sessionApiHandler.HandleCreateSessionRequest(ctx); return; }
        if (path == "/api/sessions/switch" && method == "PUT") { await _sessionApiHandler.HandleSwitchSessionRequest(ctx); return; }
        if (path?.StartsWith("/api/sessions/") == true && method == "DELETE") { await _sessionApiHandler.HandleDeleteSessionRequest(ctx); return; }
        if (path == "/api/sessions/rename" && method == "PUT") { await _sessionApiHandler.HandleRenameSessionRequest(ctx); return; }
        if (path == "/api/sessions/generate-name" && method == "POST") { await _sessionApiHandler.HandleGenerateSessionNameRequest(ctx); return; }
        if (path?.StartsWith("/api/messages/") == true && method == "DELETE") { await _statusApiHandler.HandleDeleteMessageRequest(ctx); return; }
        if (path == "/api/voice/config" && method == "GET") { await _voiceApiHandler.HandleGetConfigRequest(ctx); return; }
        if (path == "/api/voice/config" && method == "PUT") { await _voiceApiHandler.HandleUpdateConfigRequest(ctx); return; }
        if (path == "/api/voice/asr" && method == "POST") { await _voiceApiHandler.HandleAsrRequest(ctx); return; }
        if (path == "/api/voice/tts" && method == "POST") { await _voiceApiHandler.HandleTtsRequest(ctx); return; }
        if (path == "/api/providers" && method == "GET") { await _providerApiHandler.HandleGetProvidersRequest(ctx); return; }
        if (path == "/api/providers" && method == "POST") { await _providerApiHandler.HandleCreateProviderRequest(ctx); return; }
        if (path == "/api/providers/current" && method == "PUT") { await _providerApiHandler.HandleSwitchCurrentProviderRequest(ctx); return; }
        if (path?.StartsWith("/api/providers/") == true && path.EndsWith("/models") && method == "GET") { await _providerApiHandler.HandleGetModelsRequest(ctx); return; }
        if (path?.StartsWith("/api/providers/") == true && method == "PUT") { await _providerApiHandler.HandleUpdateProviderRequest(ctx); return; }
        if (path?.StartsWith("/api/providers/") == true && method == "DELETE") { await _providerApiHandler.HandleDeleteProviderRequest(ctx); return; }

        // MCP routes
        if (path == "/api/mcp/servers" && method == "GET") { await _mcpApiHandler.HandleGetServersRequest(ctx); return; }
        if (path == "/api/mcp/servers" && method == "POST") { await _mcpApiHandler.HandleCreateServerRequest(ctx); return; }
        if (path?.StartsWith("/api/mcp/servers/") == true && path.EndsWith("/restart") && method == "POST") { await _mcpApiHandler.HandleRestartServerRequest(ctx); return; }
        if (path?.StartsWith("/api/mcp/servers/") == true && path.EndsWith("/tools") && method == "GET") { await _mcpApiHandler.HandleGetToolsRequest(ctx); return; }
        if (path?.StartsWith("/api/mcp/servers/") == true && method == "PUT") { await _mcpApiHandler.HandleUpdateServerRequest(ctx); return; }
        if (path?.StartsWith("/api/mcp/servers/") == true && method == "DELETE") { await _mcpApiHandler.HandleDeleteServerRequest(ctx); return; }

        // Static file serving — webapp directory next to the executable
        if (method == "GET" && await TryServeStaticFile(ctx, path))
            return;

        ctx.Response.StatusCode = 404;
    }

    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "application/javascript; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".svg"] = "image/svg+xml",
        [".ico"] = "image/x-icon",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".ttf"] = "font/ttf",
        [".webp"] = "image/webp",
    };

    private static async Task<bool> TryServeStaticFile(HttpListenerContext ctx, string? path)
    {
        var webRoot = Path.Combine(AppContext.BaseDirectory, "webapp");
        if (!Directory.Exists(webRoot)) return false;

        // Default to index.html for root
        var relativePath = (path ?? "/").TrimStart('/');
        if (string.IsNullOrEmpty(relativePath)) relativePath = "index.html";

        var filePath = Path.GetFullPath(Path.Combine(webRoot, relativePath));

        // Prevent directory traversal
        if (!filePath.StartsWith(Path.GetFullPath(webRoot))) return false;

        // If path points to a directory or has no extension, try index.html
        if (Directory.Exists(filePath) || !Path.HasExtension(filePath))
        {
            filePath = Path.Combine(filePath, "index.html");
        }

        if (!File.Exists(filePath)) return false;

        var ext = Path.GetExtension(filePath);
        ctx.Response.ContentType = MimeTypes.GetValueOrDefault(ext, "application/octet-stream");
        var bytes = await File.ReadAllBytesAsync(filePath);
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        return true;
    }

    private async Task HandleChatRequest(HttpListenerContext ctx)
    {
        using var reader = new StreamReader(ctx.Request.InputStream);
        var body = await reader.ReadToEndAsync();
        var req = JsonSerializer.Deserialize<ChatRequest>(body);

        var reply = await _gateway.LlmService.AskAsync(req?.message ?? "");
        var response = Encoding.UTF8.GetBytes(reply);

        ColorLog.Info("MSG-HTTP", $"'{req?.message ?? ""}'");

        ctx.Response.ContentType = "text/plain; charset=utf-8";
        ctx.Response.ContentLength64 = response.Length;
        await ctx.Response.OutputStream.WriteAsync(response);
    }

}

