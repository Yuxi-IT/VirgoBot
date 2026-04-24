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
    private readonly RouteRegistry _routes = new();

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

        RegisterRoutes();
    }

    /// <summary>Wraps a handler that only needs HttpListenerContext (ignores routeParams).</summary>
    private static Func<HttpListenerContext, Dictionary<string, string>, Task> R(Func<HttpListenerContext, Task> handler)
        => (ctx, _) => handler(ctx);

    private void RegisterRoutes()
    {
        // Chat
        _routes.Register("POST", "/chat", R(HandleChatRequest));

        // Status / Messages / Logs
        _routes.Register("GET", "/api/status", R(_statusApiHandler.HandleStatusRequest));
        _routes.Register("GET", "/api/token-stats", R(_statusApiHandler.HandleTokenStatsRequest));
        _routes.Register("GET", "/api/messages", R(_statusApiHandler.HandleGetMessagesRequest));
        _routes.Register("DELETE", "/api/messages/{id}", R(_statusApiHandler.HandleDeleteMessageRequest));
        _routes.Register("GET", "/api/logs", R(_statusApiHandler.HandleGetLogsRequest));
        _routes.Register("DELETE", "/api/logs", R(_statusApiHandler.HandleClearLogsRequest));

        // Contacts
        _routes.Register("GET", "/api/contacts", R(_contactApiHandler.HandleGetContactsRequest));
        _routes.Register("POST", "/api/contacts", R(_contactApiHandler.HandleAddContactRequest));
        _routes.Register("PUT", "/api/contacts/{id}", R(_contactApiHandler.HandleUpdateContactRequest));
        _routes.Register("DELETE", "/api/contacts/{id}", R(_contactApiHandler.HandleDeleteContactRequest));

        // Config
        _routes.Register("GET", "/api/config", R(_configApiHandler.HandleGetConfigRequest));
        _routes.Register("PUT", "/api/config", R(_configApiHandler.HandleUpdateConfigRequest));
        _routes.Register("GET", "/api/config/system-memory", R(_configApiHandler.HandleGetSystemMemoryRequest));
        _routes.Register("PUT", "/api/config/system-memory", R(_configApiHandler.HandleUpdateSystemMemoryRequest));
        _routes.Register("GET", "/api/config/soul", R(_configApiHandler.HandleGetSoulRequest));
        _routes.Register("PUT", "/api/config/soul", R(_configApiHandler.HandleUpdateSoulRequest));
        _routes.Register("GET", "/api/config/rule", R(_configApiHandler.HandleGetRuleRequest));
        _routes.Register("PUT", "/api/config/rule", R(_configApiHandler.HandleUpdateRuleRequest));
        _routes.Register("PUT", "/api/config/agent", R(_agentApiHandler.HandleSwitchAgentRequest));
        _routes.Register("GET", "/api/config/channels", R(_channelApiHandler.HandleGetChannelsConfigRequest));
        _routes.Register("PUT", "/api/config/channels", R(_channelApiHandler.HandleUpdateChannelsConfigRequest));

        // Skills (specific routes before parameterized)
        _routes.Register("GET", "/api/skills", R(_skillApiHandler.HandleGetSkillsRequest));
        _routes.Register("POST", "/api/skills", R(_skillApiHandler.HandleCreateSkillRequest));
        _routes.Register("POST", "/api/skills/import", R(_skillApiHandler.HandleImportSkillZipRequest));
        _routes.Register("POST", "/api/skills/import-url", R(_skillApiHandler.HandleImportSkillZipFromUrlRequest));
        _routes.Register("GET", "/api/skills/{name}", R(_skillApiHandler.HandleGetSkillRequest));
        _routes.Register("PUT", "/api/skills/{name}", R(_skillApiHandler.HandleUpdateSkillRequest));
        _routes.Register("DELETE", "/api/skills/{name}", R(_skillApiHandler.HandleDeleteSkillRequest));

        // Tasks (specific routes before parameterized)
        _routes.Register("GET", "/api/tasks", R(_taskApiHandler.HandleGetTasksRequest));
        _routes.Register("POST", "/api/tasks", R(_taskApiHandler.HandleCreateTaskRequest));
        _routes.Register("POST", "/api/tasks/{id}/toggle", R(_taskApiHandler.HandleToggleTaskRequest));
        _routes.Register("GET", "/api/tasks/{id}", R(_taskApiHandler.HandleGetTaskRequest));
        _routes.Register("PUT", "/api/tasks/{id}", R(_taskApiHandler.HandleUpdateTaskRequest));
        _routes.Register("DELETE", "/api/tasks/{id}", R(_taskApiHandler.HandleDeleteTaskRequest));

        // Gateway
        _routes.Register("POST", "/api/gateway/restart", R(_statusApiHandler.HandleGatewayRestartRequest));
        _routes.Register("GET", "/api/gateway/status", R(_statusApiHandler.HandleGatewayStatusRequest));

        // Agents (specific routes before parameterized)
        _routes.Register("GET", "/api/agents", R(_agentApiHandler.HandleGetAgentsRequest));
        _routes.Register("POST", "/api/agents", R(_agentApiHandler.HandleCreateAgentRequest));
        _routes.Register("POST", "/api/agents/generate", R(_agentApiHandler.HandleGenerateAgentRequest));
        _routes.Register("GET", "/api/agents/{name}", R(_agentApiHandler.HandleGetAgentRequest));
        _routes.Register("PUT", "/api/agents/{name}", R(_agentApiHandler.HandleUpdateAgentRequest));
        _routes.Register("DELETE", "/api/agents/{name}", R(_agentApiHandler.HandleDeleteAgentRequest));

        // iLink
        _routes.Register("POST", "/api/ilink/login/start", R(_channelApiHandler.HandleStartILinkLoginRequest));
        _routes.Register("GET", "/api/ilink/login/status", R(_channelApiHandler.HandleGetILinkLoginStatusRequest));

        // Soul
        _routes.Register("GET", "/api/soul", R(_agentApiHandler.HandleGetSoulEntriesRequest));
        _routes.Register("POST", "/api/soul", R(_agentApiHandler.HandleAddSoulEntryRequest));
        _routes.Register("PUT", "/api/soul/{id}", R(_agentApiHandler.HandleUpdateSoulEntryRequest));
        _routes.Register("DELETE", "/api/soul/{id}", R(_agentApiHandler.HandleDeleteSoulEntryRequest));

        // Sessions (specific routes before parameterized)
        _routes.Register("GET", "/api/sessions", R(_sessionApiHandler.HandleGetSessionsRequest));
        _routes.Register("POST", "/api/sessions", R(_sessionApiHandler.HandleCreateSessionRequest));
        _routes.Register("PUT", "/api/sessions/switch", R(_sessionApiHandler.HandleSwitchSessionRequest));
        _routes.Register("PUT", "/api/sessions/rename", R(_sessionApiHandler.HandleRenameSessionRequest));
        _routes.Register("POST", "/api/sessions/generate-name", R(_sessionApiHandler.HandleGenerateSessionNameRequest));
        _routes.Register("DELETE", "/api/sessions/{name}", R(_sessionApiHandler.HandleDeleteSessionRequest));

        // Voice
        _routes.Register("GET", "/api/voice/config", R(_voiceApiHandler.HandleGetConfigRequest));
        _routes.Register("PUT", "/api/voice/config", R(_voiceApiHandler.HandleUpdateConfigRequest));
        _routes.Register("POST", "/api/voice/asr", R(_voiceApiHandler.HandleAsrRequest));
        _routes.Register("POST", "/api/voice/tts", R(_voiceApiHandler.HandleTtsRequest));

        // Providers (specific routes before parameterized)
        _routes.Register("GET", "/api/providers", R(_providerApiHandler.HandleGetProvidersRequest));
        _routes.Register("POST", "/api/providers", R(_providerApiHandler.HandleCreateProviderRequest));
        _routes.Register("PUT", "/api/providers/current", R(_providerApiHandler.HandleSwitchCurrentProviderRequest));
        _routes.Register("GET", "/api/providers/{name}/models", R(_providerApiHandler.HandleGetModelsRequest));
        _routes.Register("PUT", "/api/providers/{name}", R(_providerApiHandler.HandleUpdateProviderRequest));
        _routes.Register("DELETE", "/api/providers/{name}", R(_providerApiHandler.HandleDeleteProviderRequest));

        // MCP (specific routes before parameterized)
        _routes.Register("GET", "/api/mcp/servers", R(_mcpApiHandler.HandleGetServersRequest));
        _routes.Register("POST", "/api/mcp/servers", R(_mcpApiHandler.HandleCreateServerRequest));
        _routes.Register("POST", "/api/mcp/servers/{name}/restart", R(_mcpApiHandler.HandleRestartServerRequest));
        _routes.Register("GET", "/api/mcp/servers/{name}/tools", R(_mcpApiHandler.HandleGetToolsRequest));
        _routes.Register("PUT", "/api/mcp/servers/{name}", R(_mcpApiHandler.HandleUpdateServerRequest));
        _routes.Register("DELETE", "/api/mcp/servers/{name}", R(_mcpApiHandler.HandleDeleteServerRequest));
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
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
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

        var path = ctx.Request.Url?.AbsolutePath ?? "/";
        var method = ctx.Request.HttpMethod;

        if (method == "OPTIONS") { ctx.Response.StatusCode = 200; return; }

        if (_routes.TryMatch(method, path, out var handler, out var routeParams))
        {
            await handler!(ctx, routeParams);
            return;
        }

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

