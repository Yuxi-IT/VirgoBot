using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using VirgoBot.Channels.Handlers;
using VirgoBot.Configuration;
using VirgoBot.Contracts;
using VirgoBot.Functions;
using VirgoBot.Integrations.ILink;
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

    private static readonly DateTime StartTime = DateTime.UtcNow;

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
    }

    public async Task StartAsync()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add(_gateway.Config.Server.ListenUrl);
        listener.Start();

        while (true)
        {
            var ctx = await listener.GetContextAsync();
            _ = Task.Run(async () =>
            {
                try
                {
                    if (ctx.Request.IsWebSocketRequest)
                    {
                        await HandleWebSocketConnection(ctx);
                        return;
                    }

                    ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    ctx.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                    ctx.Response.Headers.Add("Access-Control-Allow-Headers", "*");

                    if (ctx.Request.HttpMethod == "OPTIONS")
                    {
                        ctx.Response.StatusCode = 200;
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/chat" && ctx.Request.HttpMethod == "POST")
                    {
                        await HandleChatRequest(ctx);
                    }
                    else if (_gateway.Config.Channel.ILink.Enabled &&
                             ctx.Request.Url?.AbsolutePath == _gateway.Config.Channel.ILink.WebhookPath &&
                             ctx.Request.HttpMethod == "POST")
                    {
                        await HandleILinkWebhook(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/sticker/") == true && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleStickerRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/status" && ctx.Request.HttpMethod == "GET")
                    {
                        await _statusApiHandler.HandleStatusRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/messages/users" && ctx.Request.HttpMethod == "GET")
                    {
                        await _statusApiHandler.HandleGetUsersRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/messages" && ctx.Request.HttpMethod == "GET")
                    {
                        await _statusApiHandler.HandleGetMessagesRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/contacts" && ctx.Request.HttpMethod == "GET")
                    {
                        await _contactApiHandler.HandleGetContactsRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/contacts" && ctx.Request.HttpMethod == "POST")
                    {
                        await _contactApiHandler.HandleAddContactRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/contacts/") == true && ctx.Request.HttpMethod == "PUT")
                    {
                        await _contactApiHandler.HandleUpdateContactRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/contacts/") == true && ctx.Request.HttpMethod == "DELETE")
                    {
                        await _contactApiHandler.HandleDeleteContactRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config" && ctx.Request.HttpMethod == "GET")
                    {
                        await _configApiHandler.HandleGetConfigRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config" && ctx.Request.HttpMethod == "PUT")
                    {
                        await _configApiHandler.HandleUpdateConfigRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config/system-memory" && ctx.Request.HttpMethod == "GET")
                    {
                        await _configApiHandler.HandleGetSystemMemoryRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config/system-memory" && ctx.Request.HttpMethod == "PUT")
                    {
                        await _configApiHandler.HandleUpdateSystemMemoryRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config/soul" && ctx.Request.HttpMethod == "GET")
                    {
                        await _configApiHandler.HandleGetSoulRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config/soul" && ctx.Request.HttpMethod == "PUT")
                    {
                        await _configApiHandler.HandleUpdateSoulRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config/rule" && ctx.Request.HttpMethod == "GET")
                    {
                        await _configApiHandler.HandleGetRuleRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config/rule" && ctx.Request.HttpMethod == "PUT")
                    {
                        await _configApiHandler.HandleUpdateRuleRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/logs" && ctx.Request.HttpMethod == "GET")
                    {
                        await _statusApiHandler.HandleGetLogsRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/logs" && ctx.Request.HttpMethod == "DELETE")
                    {
                        await _statusApiHandler.HandleClearLogsRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/skills" && ctx.Request.HttpMethod == "GET")
                    {
                        await _skillApiHandler.HandleGetSkillsRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/skills" && ctx.Request.HttpMethod == "POST")
                    {
                        await _skillApiHandler.HandleCreateSkillRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/skills/import" && ctx.Request.HttpMethod == "POST")
                    {
                        await _skillApiHandler.HandleImportSkillZipRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/skills/import-url" && ctx.Request.HttpMethod == "POST")
                    {
                        await _skillApiHandler.HandleImportSkillZipFromUrlRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/skills/") == true && ctx.Request.HttpMethod == "GET")
                    {
                        await _skillApiHandler.HandleGetSkillRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/skills/") == true && ctx.Request.HttpMethod == "PUT")
                    {
                        await _skillApiHandler.HandleUpdateSkillRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/skills/") == true && ctx.Request.HttpMethod == "DELETE")
                    {
                        await _skillApiHandler.HandleDeleteSkillRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/tasks" && ctx.Request.HttpMethod == "GET")
                    {
                        await _taskApiHandler.HandleGetTasksRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/tasks" && ctx.Request.HttpMethod == "POST")
                    {
                        await _taskApiHandler.HandleCreateTaskRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/tasks/") == true && ctx.Request.Url?.AbsolutePath.EndsWith("/toggle") == true && ctx.Request.HttpMethod == "POST")
                    {
                        await _taskApiHandler.HandleToggleTaskRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/tasks/") == true && ctx.Request.HttpMethod == "GET")
                    {
                        await _taskApiHandler.HandleGetTaskRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/tasks/") == true && ctx.Request.HttpMethod == "PUT")
                    {
                        await _taskApiHandler.HandleUpdateTaskRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/tasks/") == true && ctx.Request.HttpMethod == "DELETE")
                    {
                        await _taskApiHandler.HandleDeleteTaskRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/gateway/restart" && ctx.Request.HttpMethod == "POST")
                    {
                        await _statusApiHandler.HandleGatewayRestartRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/gateway/status" && ctx.Request.HttpMethod == "GET")
                    {
                        await _statusApiHandler.HandleGatewayStatusRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/agents" && ctx.Request.HttpMethod == "GET")
                    {
                        await _agentApiHandler.HandleGetAgentsRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/agents" && ctx.Request.HttpMethod == "POST")
                    {
                        await _agentApiHandler.HandleCreateAgentRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/agents/") == true && ctx.Request.HttpMethod == "GET")
                    {
                        await _agentApiHandler.HandleGetAgentRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/agents/") == true && ctx.Request.HttpMethod == "PUT")
                    {
                        await _agentApiHandler.HandleUpdateAgentRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/agents/") == true && ctx.Request.HttpMethod == "DELETE")
                    {
                        await _agentApiHandler.HandleDeleteAgentRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config/agent" && ctx.Request.HttpMethod == "PUT")
                    {
                        await _agentApiHandler.HandleSwitchAgentRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/ilink/login/qrcode" && ctx.Request.HttpMethod == "POST")
                    {
                        await _channelApiHandler.HandleCreateILinkQrCodeRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/ilink/login/status" && ctx.Request.HttpMethod == "GET")
                    {
                        await _channelApiHandler.HandleQueryILinkLoginStatusRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/ilink/login/save" && ctx.Request.HttpMethod == "POST")
                    {
                        await _channelApiHandler.HandleSaveILinkCredentialsRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/soul" && ctx.Request.HttpMethod == "GET")
                    {
                        await _agentApiHandler.HandleGetSoulEntriesRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/soul" && ctx.Request.HttpMethod == "POST")
                    {
                        await _agentApiHandler.HandleAddSoulEntryRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/soul/") == true && ctx.Request.HttpMethod == "PUT")
                    {
                        await _agentApiHandler.HandleUpdateSoulEntryRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/soul/") == true && ctx.Request.HttpMethod == "DELETE")
                    {
                        await _agentApiHandler.HandleDeleteSoulEntryRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config/channels" && ctx.Request.HttpMethod == "GET")
                    {
                        await _channelApiHandler.HandleGetChannelsConfigRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config/channels" && ctx.Request.HttpMethod == "PUT")
                    {
                        await _channelApiHandler.HandleUpdateChannelsConfigRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/sessions" && ctx.Request.HttpMethod == "GET")
                    {
                        await _sessionApiHandler.HandleGetSessionsRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/sessions" && ctx.Request.HttpMethod == "POST")
                    {
                        await _sessionApiHandler.HandleCreateSessionRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/sessions/switch" && ctx.Request.HttpMethod == "PUT")
                    {
                        await _sessionApiHandler.HandleSwitchSessionRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/sessions/") == true && ctx.Request.HttpMethod == "DELETE")
                    {
                        await _sessionApiHandler.HandleDeleteSessionRequest(ctx);
                    }
                    else
                    {
                        ctx.Response.StatusCode = 404;
                    }
                }
                catch (Exception ex)
                {
                    ColorLog.Error("HTTP", $"请求处理失败: {ex.Message}");
                    try { await HttpResponseHelper.SendErrorResponse(ctx, 500, ex.Message); } catch { ctx.Response.StatusCode = 500; }
                }
                finally { ctx.Response.Close(); }
            });
        }
    }

    private async Task HandleWebSocketConnection(HttpListenerContext ctx)
    {
        var wsCtx = await ctx.AcceptWebSocketAsync(null);
        var ws = wsCtx.WebSocket;
        _wsManager.Add(ws);
        ColorLog.Success("WS", "客户端已连接");

        var buffer = new byte[AppConstants.WebSocketBufferSize];
        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                _wsManager.Remove(ws);
                ColorLog.Info("WS", "客户端已断开");
            }
            else if (result.MessageType == WebSocketMessageType.Text)
            {
                var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var req = JsonSerializer.Deserialize<JsonElement>(msg);

                if (req.TryGetProperty("type", out var typeEl))
                {
                    var type = typeEl.GetString();

                    if (type == "message")
                    {
                        var message = req.GetProperty("message").GetString();
                        var userId = req.GetProperty("userId").GetString();
                        var effectiveUserId = userId ?? "unknown";
                        ColorLog.Info("→AI", $"[@{userId}] {message}");

                        _gateway.ActivityMonitor?.UpdateActivity();
                        var reply = await _gateway.LlmService.AskAsync(GetStableHashCode(effectiveUserId), message ?? "");
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
                    ColorLog.Info("MSG-WS", $"[@{chatReq?.userId ?? ""}] '{chatReq?.message ?? ""}'");

                    _gateway.ActivityMonitor?.UpdateActivity();
                    var reply = await _gateway.LlmService.AskAsync(_gateway.Config.Channel.Telegram.AllowedUsers[0], chatReq?.message ?? "");

                    var response = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "reply", content = reply }));
                    await ws.SendAsync(new ArraySegment<byte>(response), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }
    }

    private async Task HandleChatRequest(HttpListenerContext ctx)
    {
        using var reader = new StreamReader(ctx.Request.InputStream);
        var body = await reader.ReadToEndAsync();
        var req = JsonSerializer.Deserialize<ChatRequest>(body);

        var reply = await _gateway.LlmService.AskAsync(_gateway.Config.Channel.Telegram.AllowedUsers[0], req?.message ?? "");
        var response = Encoding.UTF8.GetBytes(reply);

        ColorLog.Info("MSG-HTTP", $"[@{req?.userId ?? ""}] '{req?.message ?? ""}'");

        ctx.Response.ContentType = "text/plain; charset=utf-8";
        ctx.Response.ContentLength64 = response.Length;
        await ctx.Response.OutputStream.WriteAsync(response);
    }

    private async Task HandleILinkWebhook(HttpListenerContext ctx)
    {
        using var reader = new StreamReader(ctx.Request.InputStream);
        var body = await reader.ReadToEndAsync();

        ColorLog.Info("ILINK", $"Webhook 收到数据: {body}");

        ColorLog.Warning("ILINK", "Webhook 模式暂不支持，请使用轮询模式");

        ctx.Response.StatusCode = 200;
        var ok = Encoding.UTF8.GetBytes("ok");
        await ctx.Response.OutputStream.WriteAsync(ok);
    }

    private async Task HandleStickerRequest(HttpListenerContext ctx)
    {
        var filename = ctx.Request.Url!.AbsolutePath.Replace("/sticker/", "");
        var path = Path.Combine("stickers", filename);

        if (File.Exists(path))
        {
            var ext = Path.GetExtension(filename).ToLower();
            ctx.Response.ContentType = ext == ".gif" ? "image/gif" : "image/png";
            var data = await File.ReadAllBytesAsync(path);
            await ctx.Response.OutputStream.WriteAsync(data);
        }
        else ctx.Response.StatusCode = 404;
    }

    private static long GetStableHashCode(string str)
    {
        unchecked
        {
            long hash1 = 5381;
            long hash2 = hash1;

            for (var i = 0; i < str.Length && str[i] != '\0'; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ str[i];
                if (i == str.Length - 1) break;
                hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
            }

            return hash1 + hash2 * 1566083941;
        }
    }
}
