using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using VirgoBot.Configuration;
using VirgoBot.Contracts;
using VirgoBot.Functions;
using VirgoBot.Integrations.ILink;
using VirgoBot.Services;
using VirgoBot.Utilities;

namespace VirgoBot.Channels;

public class HttpServerHost
{
    private readonly Config _config;
    private readonly LLMService _llmService;
    private readonly WebSocketClientManager _wsManager;
    private readonly ActivityMonitor _activityMonitor;
    private readonly ILinkBridgeService _iLinkBridge;
    private readonly Func<ILinkIncomingMessage, Task> _handleILinkMessage;
    private readonly MemoryService _memoryService;
    private readonly ContactService _contactService;
    private readonly LogService _logService;

    private static readonly DateTime StartTime = DateTime.UtcNow;

    public HttpServerHost(
        Config config,
        LLMService llmService,
        WebSocketClientManager wsManager,
        ActivityMonitor activityMonitor,
        ILinkBridgeService iLinkBridge,
        Func<ILinkIncomingMessage, Task> handleILinkMessage,
        MemoryService memoryService,
        ContactService contactService,
        LogService logService)
    {
        _config = config;
        _llmService = llmService;
        _wsManager = wsManager;
        _activityMonitor = activityMonitor;
        _iLinkBridge = iLinkBridge;
        _handleILinkMessage = handleILinkMessage;
        _memoryService = memoryService;
        _contactService = contactService;
        _logService = logService;
    }

    public async Task StartAsync()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add(_config.Server.ListenUrl);
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
                    else if (_config.ILink.Enabled &&
                             ctx.Request.Url?.AbsolutePath == _config.ILink.WebhookPath &&
                             ctx.Request.HttpMethod == "POST")
                    {
                        await HandleILinkWebhook(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/sticker/") == true && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleStickerRequest(ctx);
                    }
                    // ===== Management API =====
                    else if (ctx.Request.Url?.AbsolutePath == "/api/status" && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleStatusRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/messages/users" && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleGetUsersRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/messages" && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleGetMessagesRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/contacts" && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleGetContactsRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/contacts" && ctx.Request.HttpMethod == "POST")
                    {
                        await HandleAddContactRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/contacts/") == true && ctx.Request.HttpMethod == "PUT")
                    {
                        await HandleUpdateContactRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/contacts/") == true && ctx.Request.HttpMethod == "DELETE")
                    {
                        await HandleDeleteContactRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config" && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleGetConfigRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config/system-memory" && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleGetSystemMemoryRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config/system-memory" && ctx.Request.HttpMethod == "PUT")
                    {
                        await HandleUpdateSystemMemoryRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config/soul" && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleGetSoulRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config/soul" && ctx.Request.HttpMethod == "PUT")
                    {
                        await HandleUpdateSoulRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config/rule" && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleGetRuleRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config/rule" && ctx.Request.HttpMethod == "PUT")
                    {
                        await HandleUpdateRuleRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/logs" && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleGetLogsRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/logs" && ctx.Request.HttpMethod == "DELETE")
                    {
                        await HandleClearLogsRequest(ctx);
                    }
                    else
                    {
                        ctx.Response.StatusCode = 404;
                    }
                }
                catch (Exception ex)
                {
                    ColorLog.Error("HTTP", $"请求处理失败: {ex.Message}");
                    try { await SendErrorResponse(ctx, 500, ex.Message); } catch { ctx.Response.StatusCode = 500; }
                }
                finally { ctx.Response.Close(); }
            });
        }
    }

    // ===== Helper Methods =====

    private static async Task SendJsonResponse(HttpListenerContext ctx, object data, int statusCode = 200)
    {
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
    }

    private static async Task SendErrorResponse(HttpListenerContext ctx, int statusCode, string message)
    {
        await SendJsonResponse(ctx, new { success = false, error = message }, statusCode);
    }

    private static async Task<T?> ReadRequestBody<T>(HttpListenerContext ctx)
    {
        using var reader = new StreamReader(ctx.Request.InputStream);
        var body = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static string GetQueryParam(HttpListenerContext ctx, string key, string defaultValue = "")
    {
        return ctx.Request.QueryString[key] ?? defaultValue;
    }

    // ===== Status API =====

    private async Task HandleStatusRequest(HttpListenerContext ctx)
    {
        var uptime = DateTime.UtcNow - StartTime;
        var uptimeStr = uptime.TotalHours >= 1
            ? $"{(int)uptime.TotalHours}h {uptime.Minutes}m"
            : $"{uptime.Minutes}m {uptime.Seconds}s";

        var clients = _wsManager.GetSnapshot();

        var data = new
        {
            success = true,
            data = new
            {
                botName = "VirgoBot",
                model = _config.Model,
                uptime = uptimeStr,
                startTime = StartTime.ToString("o"),
                connectedClients = clients.Count,
                channels = new Dictionary<string, object>
                {
                    ["telegram"] = new { enabled = true, status = "running" },
                    ["http"] = new { enabled = true, status = "running" },
                    ["webSocket"] = new { enabled = true, status = "running", clients = clients.Count },
                    ["email"] = new { enabled = true, status = "monitoring" },
                    ["iLink"] = new { enabled = _config.ILink.Enabled, status = _config.ILink.Enabled ? "running" : "disabled" }
                },
                server = new
                {
                    listenUrl = _config.Server.ListenUrl,
                    maxTokens = _config.Server.MaxTokens,
                    messageLimit = _config.Server.MessageLimit
                }
            }
        };

        await SendJsonResponse(ctx, data);
    }

    // ===== Messages API =====

    private async Task HandleGetUsersRequest(HttpListenerContext ctx)
    {
        var userIds = _memoryService.GetAllUserIds();
        var users = userIds.Select(uid => new
        {
            userId = uid.ToString(),
            messageCount = _memoryService.GetMessageCount(uid),
            lastActive = _memoryService.GetLastActiveTime(uid)?.ToString("o") ?? ""
        }).ToList();

        await SendJsonResponse(ctx, new { success = true, data = users });
    }

    private async Task HandleGetMessagesRequest(HttpListenerContext ctx)
    {
        var userIdStr = GetQueryParam(ctx, "userId");
        var limitStr = GetQueryParam(ctx, "limit", "50");
        var offsetStr = GetQueryParam(ctx, "offset", "0");

        if (!long.TryParse(userIdStr, out var userId))
        {
            await SendErrorResponse(ctx, 400, "Invalid userId");
            return;
        }

        int.TryParse(limitStr, out var limit);
        int.TryParse(offsetStr, out var offset);
        if (limit <= 0) limit = 50;
        if (offset < 0) offset = 0;

        var (messages, total) = _memoryService.LoadMessagesWithPagination(userId, limit, offset);

        var data = new
        {
            messages = messages.Select(m => new
            {
                id = m.Id,
                role = m.Role,
                content = m.Content,
                createdAt = m.CreatedAt.ToString("o")
            }),
            total,
            userId = userIdStr
        };

        await SendJsonResponse(ctx, new { success = true, data });
    }

    // ===== Contacts API =====

    private async Task HandleGetContactsRequest(HttpListenerContext ctx)
    {
        var contacts = _contactService.GetAllContacts();
        await SendJsonResponse(ctx, new { success = true, data = contacts });
    }

    private async Task HandleAddContactRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<ContactRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Name))
        {
            await SendErrorResponse(ctx, 400, "Name is required");
            return;
        }

        _contactService.AddContact(body.Name, body.Email, body.Phone, body.Notes);
        await SendJsonResponse(ctx, new { success = true, message = "Contact added" });
    }

    private async Task HandleUpdateContactRequest(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath;
        var idStr = path.Replace("/api/contacts/", "");

        if (!int.TryParse(idStr, out var id))
        {
            await SendErrorResponse(ctx, 400, "Invalid contact ID");
            return;
        }

        var body = await ReadRequestBody<ContactRequest>(ctx);
        if (body == null)
        {
            await SendErrorResponse(ctx, 400, "Invalid request body");
            return;
        }

        _contactService.UpdateContact(id, body.Name, body.Email, body.Phone, body.Notes);
        await SendJsonResponse(ctx, new { success = true, message = "Contact updated" });
    }

    private async Task HandleDeleteContactRequest(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath;
        var idStr = path.Replace("/api/contacts/", "");

        if (!int.TryParse(idStr, out var id))
        {
            await SendErrorResponse(ctx, 400, "Invalid contact ID");
            return;
        }

        _contactService.DeleteContact(id);
        await SendJsonResponse(ctx, new { success = true, message = "Contact deleted" });
    }

    // ===== Config API =====

    private async Task HandleGetConfigRequest(HttpListenerContext ctx)
    {
        var data = new
        {
            model = _config.Model,
            baseUrl = _config.BaseUrl,
            server = new
            {
                listenUrl = _config.Server.ListenUrl,
                maxTokens = _config.Server.MaxTokens,
                messageLimit = _config.Server.MessageLimit
            },
            email = new
            {
                imapHost = _config.Email.ImapHost,
                address = _config.Email.Address,
                enabled = !string.IsNullOrEmpty(_config.Email.Address) && _config.Email.Address != "your@email.com"
            },
            iLink = new
            {
                enabled = _config.ILink.Enabled
            },
            allowedUsers = _config.AllowedUsers
        };

        await SendJsonResponse(ctx, new { success = true, data });
    }

    private async Task HandleGetSystemMemoryRequest(HttpListenerContext ctx)
    {
        var memoryPath = Path.Combine(AppConstants.ConfigDirectory, _config.MemoryFile);
        var content = File.Exists(memoryPath) ? await File.ReadAllTextAsync(memoryPath) : "";
        await SendJsonResponse(ctx, new { success = true, data = new { content } });
    }

    private async Task HandleUpdateSystemMemoryRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<ContentRequest>(ctx);
        if (body == null)
        {
            await SendErrorResponse(ctx, 400, "Invalid request body");
            return;
        }

        var memoryPath = Path.Combine(AppConstants.ConfigDirectory, _config.MemoryFile);
        await File.WriteAllTextAsync(memoryPath, body.Content ?? "");
        ColorLog.Success("CONFIG", "系统记忆已更新");
        await SendJsonResponse(ctx, new { success = true, message = "System memory updated" });
    }

    private async Task HandleGetSoulRequest(HttpListenerContext ctx)
    {
        var soulPath = Path.Combine(AppConstants.ConfigDirectory, _config.SoulFile);
        var content = File.Exists(soulPath) ? await File.ReadAllTextAsync(soulPath) : "";
        await SendJsonResponse(ctx, new { success = true, data = new { content } });
    }

    private async Task HandleUpdateSoulRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<ContentRequest>(ctx);
        if (body == null)
        {
            await SendErrorResponse(ctx, 400, "Invalid request body");
            return;
        }

        var soulPath = Path.Combine(AppConstants.ConfigDirectory, _config.SoulFile);
        await File.WriteAllTextAsync(soulPath, body.Content ?? "");
        ColorLog.Success("CONFIG", "Soul 文件已更新");
        await SendJsonResponse(ctx, new { success = true, message = "Soul updated" });
    }

    private async Task HandleGetRuleRequest(HttpListenerContext ctx)
    {
        var rulePath = Path.Combine(AppConstants.ConfigDirectory, _config.RuleFile);
        var content = File.Exists(rulePath) ? await File.ReadAllTextAsync(rulePath) : "";
        await SendJsonResponse(ctx, new { success = true, data = new { content } });
    }

    private async Task HandleUpdateRuleRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<ContentRequest>(ctx);
        if (body == null)
        {
            await SendErrorResponse(ctx, 400, "Invalid request body");
            return;
        }

        var rulePath = Path.Combine(AppConstants.ConfigDirectory, _config.RuleFile);
        await File.WriteAllTextAsync(rulePath, body.Content ?? "");
        ColorLog.Success("CONFIG", "Rule 文件已更新");
        await SendJsonResponse(ctx, new { success = true, message = "Rule updated" });
    }

    // ===== Logs API =====

    private async Task HandleGetLogsRequest(HttpListenerContext ctx)
    {
        var level = GetQueryParam(ctx, "level");
        var limitStr = GetQueryParam(ctx, "limit", "100");
        var offsetStr = GetQueryParam(ctx, "offset", "0");

        int.TryParse(limitStr, out var limit);
        int.TryParse(offsetStr, out var offset);
        if (limit <= 0) limit = 100;
        if (offset < 0) offset = 0;

        var (logs, total) = _logService.GetLogs(
            string.IsNullOrEmpty(level) ? null : level,
            limit, offset);

        var data = new
        {
            logs = logs.Select(l => new
            {
                id = l.Id,
                level = l.Level,
                component = l.Component,
                message = l.Message,
                timestamp = l.Timestamp.ToString("o")
            }),
            total
        };

        await SendJsonResponse(ctx, new { success = true, data });
    }

    private async Task HandleClearLogsRequest(HttpListenerContext ctx)
    {
        _logService.Clear();
        ColorLog.Info("LOGS", "日志已清空");
        await SendJsonResponse(ctx, new { success = true, message = "Logs cleared" });
    }

    // ===== Original Handlers =====

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

                    if (type == "newMessage")
                    {
                        var username = req.GetProperty("username").GetString();
                        var effectiveUsername = username ?? "unknown";
                        var queueLength = req.GetProperty("queueLength").GetInt32();
                        ColorLog.Info("NEW-MSG", $"来自 {username}, 队列: {queueLength}");

                        var prompt = $"系统提示：用户 {username} 发来了新消息，请使用 switch_douyin_chat 函数回复";
                        ColorLog.Info("→AI", prompt);
                        var reply = await _llmService.AskAsync(GetStableHashCode(effectiveUsername), prompt, null, null, async (targetUser) =>
                        {
                            var response = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "switchChat", username = targetUser }));
                            await ws.SendAsync(new ArraySegment<byte>(response), WebSocketMessageType.Text, true, CancellationToken.None);
                            ColorLog.Success("SWITCH", $"切换到 {targetUser}");
                        });
                        ColorLog.Success("AI→", reply);
                    }
                    else if (type == "message")
                    {
                        var message = req.GetProperty("message").GetString();
                        var userId = req.GetProperty("userId").GetString();
                        var effectiveUserId = userId ?? "unknown";
                        ColorLog.Info("→AI", $"[@{userId}] {message}");

                        _activityMonitor.UpdateActivity();
                        var reply = await _llmService.AskAsync(GetStableHashCode(effectiveUserId), message ?? "");
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

                    _activityMonitor.UpdateActivity();
                    var reply = await _llmService.AskAsync(_config.AllowedUsers[0], chatReq?.message ?? "");

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

        var reply = await _llmService.AskAsync(_config.AllowedUsers[0], req?.message ?? "");
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

        if (_iLinkBridge.TryParseIncoming(body, out var incoming) && incoming is not null)
        {
            await _handleILinkMessage(incoming);
        }

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

// Request DTOs for management API
public record ContactRequest
{
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Notes { get; init; }
}

public record ContentRequest
{
    public string? Content { get; init; }
}
