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
    private readonly Gateway _gateway;
    private readonly WebSocketClientManager _wsManager;
    private readonly MemoryService _memoryService;
    private readonly LogService _logService;
    private readonly ILinkLoginService _iLinkLoginService;

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
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config" && ctx.Request.HttpMethod == "PUT")
                    {
                        await HandleUpdateConfigRequest(ctx);
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
                    // ===== Skills API =====
                    else if (ctx.Request.Url?.AbsolutePath == "/api/skills" && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleGetSkillsRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/skills" && ctx.Request.HttpMethod == "POST")
                    {
                        await HandleCreateSkillRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/skills/") == true && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleGetSkillRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/skills/") == true && ctx.Request.HttpMethod == "PUT")
                    {
                        await HandleUpdateSkillRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/skills/") == true && ctx.Request.HttpMethod == "DELETE")
                    {
                        await HandleDeleteSkillRequest(ctx);
                    }
                    // ===== Gateway API =====
                    else if (ctx.Request.Url?.AbsolutePath == "/api/gateway/restart" && ctx.Request.HttpMethod == "POST")
                    {
                        await HandleGatewayRestartRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/gateway/status" && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleGatewayStatusRequest(ctx);
                    }
                    // ===== Agent API =====
                    else if (ctx.Request.Url?.AbsolutePath == "/api/agents" && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleGetAgentsRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/agents" && ctx.Request.HttpMethod == "POST")
                    {
                        await HandleCreateAgentRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/agents/") == true && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleGetAgentRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/agents/") == true && ctx.Request.HttpMethod == "PUT")
                    {
                        await HandleUpdateAgentRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/agents/") == true && ctx.Request.HttpMethod == "DELETE")
                    {
                        await HandleDeleteAgentRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config/agent" && ctx.Request.HttpMethod == "PUT")
                    {
                        await HandleSwitchAgentRequest(ctx);
                    }
                    // ===== iLink Login API =====
                    else if (ctx.Request.Url?.AbsolutePath == "/api/ilink/login/qrcode" && ctx.Request.HttpMethod == "POST")
                    {
                        await HandleCreateILinkQrCodeRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/ilink/login/status" && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleQueryILinkLoginStatusRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/ilink/login/save" && ctx.Request.HttpMethod == "POST")
                    {
                        await HandleSaveILinkCredentialsRequest(ctx);
                    }
                    // ===== Soul CRUD API =====
                    else if (ctx.Request.Url?.AbsolutePath == "/api/soul" && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleGetSoulEntriesRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/soul" && ctx.Request.HttpMethod == "POST")
                    {
                        await HandleAddSoulEntryRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/soul/") == true && ctx.Request.HttpMethod == "PUT")
                    {
                        await HandleUpdateSoulEntryRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/soul/") == true && ctx.Request.HttpMethod == "DELETE")
                    {
                        await HandleDeleteSoulEntryRequest(ctx);
                    }
                    // ===== Channel Config API =====
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config/channels" && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleGetChannelsConfigRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/config/channels" && ctx.Request.HttpMethod == "PUT")
                    {
                        await HandleUpdateChannelsConfigRequest(ctx);
                    }
                    // ===== Session API =====
                    else if (ctx.Request.Url?.AbsolutePath == "/api/sessions" && ctx.Request.HttpMethod == "GET")
                    {
                        await HandleGetSessionsRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/sessions" && ctx.Request.HttpMethod == "POST")
                    {
                        await HandleCreateSessionRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath == "/api/sessions/switch" && ctx.Request.HttpMethod == "PUT")
                    {
                        await HandleSwitchSessionRequest(ctx);
                    }
                    else if (ctx.Request.Url?.AbsolutePath.StartsWith("/api/sessions/") == true && ctx.Request.HttpMethod == "DELETE")
                    {
                        await HandleDeleteSessionRequest(ctx);
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

    // ===== iLink Login Handlers =====

    private async Task HandleCreateILinkQrCodeRequest(HttpListenerContext ctx)
    {
        try
        {
            var result = await _iLinkLoginService.CreateQrCodeAsync();
            await SendJsonResponse(ctx, new { success = true, data = result });
        }
        catch (Exception ex)
        {
            ColorLog.Error("ILINK-LOGIN", $"创建二维码失败: {ex.Message}");
            await SendJsonResponse(ctx, new { success = false, error = ex.Message }, 500);
        }
    }

    private async Task HandleQueryILinkLoginStatusRequest(HttpListenerContext ctx)
    {
        try
        {
            var qrCode = ctx.Request.QueryString["qrcode"];
            if (string.IsNullOrEmpty(qrCode))
            {
                await SendJsonResponse(ctx, new { success = false, error = "缺少 qrcode 参数" }, 400);
                return;
            }

            var result = await _iLinkLoginService.QueryStatusAsync(qrCode);
            await SendJsonResponse(ctx, new { success = true, data = result });
        }
        catch (Exception ex)
        {
            ColorLog.Error("ILINK-LOGIN", $"查询登录状态失败: {ex.Message}");
            await SendJsonResponse(ctx, new { success = false, error = ex.Message }, 500);
        }
    }

    private async Task HandleSaveILinkCredentialsRequest(HttpListenerContext ctx)
    {
        try
        {
            var req = await ReadRequestBody<SaveILinkCredentialsRequest>(ctx);

            if (req == null || string.IsNullOrEmpty(req.BotToken) || string.IsNullOrEmpty(req.ApiBaseUri))
            {
                ColorLog.Error("ILINK-LOGIN", "保存凭证失败: 缺少必要参数");
                await SendJsonResponse(ctx, new { success = false, error = "缺少必要参数" }, 400);
                return;
            }

            ColorLog.Info("ILINK-LOGIN", $"正在保存 iLink 凭证 (BotId: {req.ILinkBotId})");

            _gateway.Config.Channel.ILink.Token = req.BotToken;
            _gateway.Config.Channel.ILink.WebSocketUrl = $"{req.ApiBaseUri}/bot/v1/ws?token={req.BotToken}";
            _gateway.Config.Channel.ILink.SendUrl = $"{req.ApiBaseUri}/bot/v1/message/send";
            _gateway.Config.Channel.ILink.Enabled = true;

            ConfigLoader.Save(_gateway.Config);

            ColorLog.Success("ILINK-LOGIN", "iLink 凭证已保存并启用");
            await SendJsonResponse(ctx, new { success = true });
        }
        catch (Exception ex)
        {
            ColorLog.Error("ILINK-LOGIN", $"保存凭证失败: {ex.Message}");
            await SendJsonResponse(ctx, new { success = false, error = ex.Message }, 500);
        }
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

    // ===== Gateway API =====

    private async Task HandleGatewayRestartRequest(HttpListenerContext ctx)
    {
        try
        {
            await _gateway.RestartAsync();
            await SendJsonResponse(ctx, new
            {
                success = true,
                message = "Gateway restarted",
                data = new
                {
                    isRunning = _gateway.IsRunning,
                    channels = _gateway.ChannelStatuses.ToDictionary(
                        kv => kv.Key,
                        kv => new { kv.Value.Name, kv.Value.Enabled, kv.Value.Status })
                }
            });
        }
        catch (Exception ex)
        {
            await SendErrorResponse(ctx, 500, $"Restart failed: {ex.Message}");
        }
    }

    private async Task HandleGatewayStatusRequest(HttpListenerContext ctx)
    {
        var data = new
        {
            isRunning = _gateway.IsRunning,
            channels = _gateway.ChannelStatuses.ToDictionary(
                kv => kv.Key,
                kv => new { kv.Value.Name, kv.Value.Enabled, kv.Value.Status }),
            config = new
            {
                model = _gateway.Config.Model,
                baseUrl = _gateway.Config.BaseUrl,
                maxTokens = _gateway.Config.Server.MaxTokens,
                messageLimit = _gateway.Config.Server.MessageLimit
            }
        };

        await SendJsonResponse(ctx, new { success = true, data });
    }

    private async Task HandleUpdateConfigRequest(HttpListenerContext ctx)
    {
        try
        {
            var body = await ReadRequestBody<ConfigUpdateRequest>(ctx);
            if (body == null)
            {
                await SendErrorResponse(ctx, 400, "Invalid request body");
                return;
            }

            var config = _gateway.Config;

            // Merge partial updates
            if (!string.IsNullOrWhiteSpace(body.Model)) config.Model = body.Model;
            if (!string.IsNullOrWhiteSpace(body.BaseUrl)) config.BaseUrl = body.BaseUrl;
            if (body.MaxTokens.HasValue) config.Server.MaxTokens = body.MaxTokens.Value;
            if (body.MessageLimit.HasValue) config.Server.MessageLimit = body.MessageLimit.Value;
            if (!string.IsNullOrWhiteSpace(body.MessageSplitDelimiters)) config.Server.MessageSplitDelimiters = body.MessageSplitDelimiters;
            if (!string.IsNullOrWhiteSpace(body.ImapHost)) config.Channel.Email.ImapHost = body.ImapHost;
            if (!string.IsNullOrWhiteSpace(body.EmailAddress)) config.Channel.Email.Address = body.EmailAddress;
            if (!string.IsNullOrWhiteSpace(body.MemoryFile)) config.MemoryFile = body.MemoryFile;

            ConfigLoader.Save(config);
            await SendJsonResponse(ctx, new { success = true, message = "Config saved" });
        }
        catch (Exception ex)
        {
            await SendErrorResponse(ctx, 500, $"Failed to save config: {ex.Message}");
        }
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
                model = _gateway.Config.Model,
                uptime = uptimeStr,
                startTime = StartTime.ToString("o"),
                connectedClients = clients.Count,
                channels = new Dictionary<string, object>
                {
                    ["telegram"] = new { enabled = _gateway.Config.Channel.Telegram.Enabled, status = _gateway.ChannelStatuses["telegram"].Status },
                    ["http"] = new { enabled = true, status = "running" },
                    ["webSocket"] = new { enabled = true, status = "running", clients = clients.Count },
                    ["email"] = new { enabled = _gateway.Config.Channel.Email.Enabled, status = _gateway.ChannelStatuses["email"].Status },
                    ["iLink"] = new { enabled = _gateway.Config.Channel.ILink.Enabled, status = _gateway.ChannelStatuses["iLink"].Status }
                },
                server = new
                {
                    listenUrl = _gateway.Config.Server.ListenUrl,
                    maxTokens = _gateway.Config.Server.MaxTokens,
                    messageLimit = _gateway.Config.Server.MessageLimit
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
        var contacts = _gateway.ContactService.GetAllContacts();
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

        _gateway.ContactService.AddContact(body.Name, body.Email, body.Phone, body.Notes);
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

        _gateway.ContactService.UpdateContact(id, body.Name, body.Email, body.Phone, body.Notes);
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

        _gateway.ContactService.DeleteContact(id);
        await SendJsonResponse(ctx, new { success = true, message = "Contact deleted" });
    }

    // ===== Config API =====

    private async Task HandleGetConfigRequest(HttpListenerContext ctx)
    {
        var config = _gateway.Config;
        var data = new
        {
            model = config.Model,
            baseUrl = config.BaseUrl,
            apiKey = MaskSecret(config.ApiKey),
            memoryFile = config.MemoryFile,
            server = new
            {
                listenUrl = config.Server.ListenUrl,
                maxTokens = config.Server.MaxTokens,
                messageLimit = config.Server.MessageLimit,
                messageSplitDelimiters = config.Server.MessageSplitDelimiters
            },
            channel = new
            {
                telegram = new
                {
                    enabled = config.Channel.Telegram.Enabled,
                    botToken = MaskSecret(config.Channel.Telegram.BotToken),
                    allowedUsers = config.Channel.Telegram.AllowedUsers
                },
                email = new
                {
                    enabled = config.Channel.Email.Enabled,
                    imapHost = config.Channel.Email.ImapHost,
                    imapPort = config.Channel.Email.ImapPort,
                    smtpHost = config.Channel.Email.SmtpHost,
                    smtpPort = config.Channel.Email.SmtpPort,
                    address = config.Channel.Email.Address,
                    password = MaskSecret(config.Channel.Email.Password),
                    notification = new
                    {
                        notifyToTelegram = config.Channel.Email.Notification.NotifyToTelegram,
                        notifyToILink = config.Channel.Email.Notification.NotifyToILink,
                        notifyToWebSocket = config.Channel.Email.Notification.NotifyToWebSocket
                    }
                },
                iLink = new
                {
                    enabled = config.Channel.ILink.Enabled,
                    token = MaskSecret(config.Channel.ILink.Token),
                    webSocketUrl = config.Channel.ILink.WebSocketUrl,
                    sendUrl = config.Channel.ILink.SendUrl,
                    webhookPath = config.Channel.ILink.WebhookPath,
                    defaultUserId = config.Channel.ILink.DefaultUserId
                }
            }
        };

        await SendJsonResponse(ctx, new { success = true, data });
    }

    private static string MaskSecret(string secret)
    {
        if (string.IsNullOrEmpty(secret) || secret.Length <= 8)
            return "****";
        return secret[..4] + "****" + secret[^4..];
    }

    private async Task HandleGetSystemMemoryRequest(HttpListenerContext ctx)
    {
        var memoryPath = Path.Combine(AppConstants.ConfigDirectory, _gateway.Config.MemoryFile);
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

        var memoryPath = Path.Combine(AppConstants.ConfigDirectory, _gateway.Config.MemoryFile);
        await File.WriteAllTextAsync(memoryPath, body.Content ?? "");
        ColorLog.Success("CONFIG", "系统记忆已更新");
        await SendJsonResponse(ctx, new { success = true, message = "System memory updated" });
    }

    private async Task HandleGetSoulRequest(HttpListenerContext ctx)
    {
        var soulPath = Path.Combine(AppConstants.ConfigDirectory, _gateway.Config.SoulFile);
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

        var soulPath = Path.Combine(AppConstants.ConfigDirectory, _gateway.Config.SoulFile);
        await File.WriteAllTextAsync(soulPath, body.Content ?? "");
        ColorLog.Success("CONFIG", "Soul 文件已更新");
        await SendJsonResponse(ctx, new { success = true, message = "Soul updated" });
    }

    private async Task HandleGetRuleRequest(HttpListenerContext ctx)
    {
        var rulePath = Path.Combine(AppConstants.ConfigDirectory, _gateway.Config.RuleFile);
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

        var rulePath = Path.Combine(AppConstants.ConfigDirectory, _gateway.Config.RuleFile);
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

    // ===== Skills API =====

    private async Task HandleGetSkillsRequest(HttpListenerContext ctx)
    {
        var dir = AppConstants.SkillsDirectory;
        Directory.CreateDirectory(dir);

        var skills = new List<object>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.StartsWith("_")) continue;

            try
            {
                var json = await File.ReadAllTextAsync(file);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var mode = root.TryGetProperty("mode", out var modeEl) ? modeEl.GetString() ?? "command" : "command";

                string command;
                if (mode == "http" && root.TryGetProperty("http", out var httpEl))
                {
                    var method = httpEl.TryGetProperty("method", out var m) ? m.GetString() ?? "GET" : "GET";
                    var url = httpEl.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                    command = $"{method} {url}";
                }
                else
                {
                    command = root.TryGetProperty("command", out var cmdEl) ? cmdEl.GetString() ?? "" : "";
                }

                skills.Add(new
                {
                    fileName,
                    name = root.GetProperty("name").GetString() ?? "",
                    description = root.GetProperty("description").GetString() ?? "",
                    command,
                    mode,
                    parameterCount = root.TryGetProperty("parameters", out var p) ? p.GetArrayLength() : 0
                });
            }
            catch
            {
                skills.Add(new { fileName, name = fileName, description = "解析失败", command = "", mode = "command", parameterCount = 0 });
            }
        }

        await SendJsonResponse(ctx, new { success = true, data = skills });
    }

    private async Task HandleGetSkillRequest(HttpListenerContext ctx)
    {
        var name = ctx.Request.Url!.AbsolutePath.Replace("/api/skills/", "");
        var filePath = Path.Combine(AppConstants.SkillsDirectory, $"{name}.json");

        if (!File.Exists(filePath))
        {
            await SendErrorResponse(ctx, 404, "Skill not found");
            return;
        }

        var content = await File.ReadAllTextAsync(filePath);
        await SendJsonResponse(ctx, new { success = true, data = new { fileName = $"{name}.json", content } });
    }

    private async Task HandleCreateSkillRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<SkillRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Name))
        {
            await SendErrorResponse(ctx, 400, "Name is required");
            return;
        }

        var dir = AppConstants.SkillsDirectory;
        Directory.CreateDirectory(dir);

        var fileName = $"{body.Name}.json";
        var filePath = Path.Combine(dir, fileName);

        if (File.Exists(filePath))
        {
            await SendErrorResponse(ctx, 409, "Skill already exists");
            return;
        }

        await File.WriteAllTextAsync(filePath, body.Content ?? "{}");
        ColorLog.Success("SKILL", $"Skill 已创建: {fileName}");
        await SendJsonResponse(ctx, new { success = true, message = "Skill created" });
    }

    private async Task HandleUpdateSkillRequest(HttpListenerContext ctx)
    {
        var name = ctx.Request.Url!.AbsolutePath.Replace("/api/skills/", "");
        var filePath = Path.Combine(AppConstants.SkillsDirectory, $"{name}.json");

        if (!File.Exists(filePath))
        {
            await SendErrorResponse(ctx, 404, "Skill not found");
            return;
        }

        var body = await ReadRequestBody<SkillRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Content))
        {
            await SendErrorResponse(ctx, 400, "Content is required");
            return;
        }

        // 如果名称变了，需要重命名文件
        if (!string.IsNullOrWhiteSpace(body.Name) && body.Name != name)
        {
            var newFilePath = Path.Combine(AppConstants.SkillsDirectory, $"{body.Name}.json");
            if (File.Exists(newFilePath))
            {
                await SendErrorResponse(ctx, 409, "Target skill name already exists");
                return;
            }
            File.Delete(filePath);
            filePath = newFilePath;
        }

        await File.WriteAllTextAsync(filePath, body.Content);
        ColorLog.Success("SKILL", $"Skill 已更新: {Path.GetFileName(filePath)}");
        await SendJsonResponse(ctx, new { success = true, message = "Skill updated" });
    }

    private async Task HandleDeleteSkillRequest(HttpListenerContext ctx)
    {
        var name = ctx.Request.Url!.AbsolutePath.Replace("/api/skills/", "");
        var filePath = Path.Combine(AppConstants.SkillsDirectory, $"{name}.json");

        if (!File.Exists(filePath))
        {
            await SendErrorResponse(ctx, 404, "Skill not found");
            return;
        }

        File.Delete(filePath);
        ColorLog.Success("SKILL", $"Skill 已删除: {name}.json");
        await SendJsonResponse(ctx, new { success = true, message = "Skill deleted" });
    }

    // ===== Agent API =====

    private async Task HandleGetAgentsRequest(HttpListenerContext ctx)
    {
        var agentDir = Path.Combine(AppConstants.ConfigDirectory, "agent");
        Directory.CreateDirectory(agentDir);

        var agents = new List<object>();
        foreach (var file in Directory.GetFiles(agentDir, "*.md"))
        {
            var fileName = Path.GetFileName(file);
            var content = await File.ReadAllTextAsync(file);
            var preview = content.Length > 200 ? content[..200] + "..." : content;
            agents.Add(new
            {
                name = Path.GetFileNameWithoutExtension(file),
                fileName,
                memoryPath = $"agent/{fileName}",
                preview,
                size = content.Length
            });
        }

        var currentAgent = _gateway.Config.MemoryFile;
        await SendJsonResponse(ctx, new { success = true, data = new { agents, currentAgent } });
    }

    private async Task HandleGetAgentRequest(HttpListenerContext ctx)
    {
        var name = Uri.UnescapeDataString(ctx.Request.Url!.AbsolutePath.Replace("/api/agents/", ""));
        var filePath = Path.Combine(AppConstants.ConfigDirectory, "agent", $"{name}.md");

        if (!File.Exists(filePath))
        {
            await SendErrorResponse(ctx, 404, "Agent not found");
            return;
        }

        var content = await File.ReadAllTextAsync(filePath);
        await SendJsonResponse(ctx, new { success = true, data = new { name, content } });
    }

    private async Task HandleSwitchAgentRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<AgentSwitchRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.MemoryFile))
        {
            await SendErrorResponse(ctx, 400, "MemoryFile is required");
            return;
        }

        var config = _gateway.Config;
        config.MemoryFile = body.MemoryFile;
        ConfigLoader.Save(config);
        await SendJsonResponse(ctx, new { success = true, message = "Agent switched" });
    }

    private async Task HandleCreateAgentRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<AgentCreateUpdateRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Name) || body.Content == null)
        {
            await SendErrorResponse(ctx, 400, "Name and content are required");
            return;
        }

        // Sanitize name: only allow alphanumeric, Chinese chars, underscore, dash
        var safeName = body.Name.Trim();
        if (string.IsNullOrWhiteSpace(safeName))
        {
            await SendErrorResponse(ctx, 400, "Invalid agent name");
            return;
        }

        var agentDir = Path.Combine(AppConstants.ConfigDirectory, "agent");
        Directory.CreateDirectory(agentDir);
        var filePath = Path.Combine(agentDir, $"{safeName}.md");

        if (File.Exists(filePath))
        {
            await SendErrorResponse(ctx, 409, "Agent already exists");
            return;
        }

        await File.WriteAllTextAsync(filePath, body.Content);
        ColorLog.Success("AGENT", $"智能体 '{safeName}' 已创建");
        await SendJsonResponse(ctx, new { success = true, message = "Agent created" });
    }

    private async Task HandleUpdateAgentRequest(HttpListenerContext ctx)
    {
        var name = Uri.UnescapeDataString(ctx.Request.Url!.AbsolutePath.Replace("/api/agents/", ""));
        var filePath = Path.Combine(AppConstants.ConfigDirectory, "agent", $"{name}.md");

        if (!File.Exists(filePath))
        {
            await SendErrorResponse(ctx, 404, "Agent not found");
            return;
        }

        var body = await ReadRequestBody<AgentCreateUpdateRequest>(ctx);
        if (body == null || body.Content == null)
        {
            await SendErrorResponse(ctx, 400, "Content is required");
            return;
        }

        await File.WriteAllTextAsync(filePath, body.Content);
        ColorLog.Success("AGENT", $"智能体 '{name}' 已更新");
        await SendJsonResponse(ctx, new { success = true, message = "Agent updated" });
    }

    private async Task HandleDeleteAgentRequest(HttpListenerContext ctx)
    {
        var name = Uri.UnescapeDataString(ctx.Request.Url!.AbsolutePath.Replace("/api/agents/", ""));
        var filePath = Path.Combine(AppConstants.ConfigDirectory, "agent", $"{name}.md");

        if (!File.Exists(filePath))
        {
            await SendErrorResponse(ctx, 404, "Agent not found");
            return;
        }

        // Don't allow deleting the currently active agent
        var currentAgent = _gateway.Config.MemoryFile;
        if (currentAgent == $"agent/{name}.md")
        {
            await SendErrorResponse(ctx, 400, "Cannot delete the currently active agent");
            return;
        }

        File.Delete(filePath);
        ColorLog.Success("AGENT", $"智能体 '{name}' 已删除");
        await SendJsonResponse(ctx, new { success = true, message = "Agent deleted" });
    }

    // ===== Soul CRUD API =====

    private async Task HandleGetSoulEntriesRequest(HttpListenerContext ctx)
    {
        var entries = _memoryService.GetAllSoulEntries();
        var data = entries.Select(e => new
        {
            id = e.Id,
            content = e.Content,
            createdAt = e.CreatedAt.ToString("o")
        });
        await SendJsonResponse(ctx, new { success = true, data });
    }

    private async Task HandleAddSoulEntryRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<ContentRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Content))
        {
            await SendErrorResponse(ctx, 400, "Content is required");
            return;
        }

        _memoryService.AddSoulEntry(body.Content);
        ColorLog.Success("SOUL", "Soul 记录已添加");
        await SendJsonResponse(ctx, new { success = true, message = "Soul entry added" });
    }

    private async Task HandleUpdateSoulEntryRequest(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath;
        var idStr = path.Replace("/api/soul/", "");

        if (!int.TryParse(idStr, out var id))
        {
            await SendErrorResponse(ctx, 400, "Invalid soul entry ID");
            return;
        }

        var body = await ReadRequestBody<ContentRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Content))
        {
            await SendErrorResponse(ctx, 400, "Content is required");
            return;
        }

        _memoryService.UpdateSoulEntry(id, body.Content);
        ColorLog.Success("SOUL", $"Soul 记录已更新: {id}");
        await SendJsonResponse(ctx, new { success = true, message = "Soul entry updated" });
    }

    private async Task HandleDeleteSoulEntryRequest(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath;
        var idStr = path.Replace("/api/soul/", "");

        if (!int.TryParse(idStr, out var id))
        {
            await SendErrorResponse(ctx, 400, "Invalid soul entry ID");
            return;
        }

        _memoryService.DeleteSoulEntry(id);
        ColorLog.Success("SOUL", $"Soul 记录已删除: {id}");
        await SendJsonResponse(ctx, new { success = true, message = "Soul entry deleted" });
    }

    // ===== Channel Config API =====

    private async Task HandleGetChannelsConfigRequest(HttpListenerContext ctx)
    {
        var config = _gateway.Config;
        var clients = _wsManager.GetSnapshot();

        var data = new
        {
            iLink = new
            {
                enabled = config.Channel.ILink.Enabled,
                token = config.Channel.ILink.Token,
                webSocketUrl = config.Channel.ILink.WebSocketUrl,
                sendUrl = config.Channel.ILink.SendUrl,
                webhookPath = config.Channel.ILink.WebhookPath,
                defaultUserId = config.Channel.ILink.DefaultUserId
            },
            telegram = new
            {
                enabled = config.Channel.Telegram.Enabled,
                botToken = config.Channel.Telegram.BotToken,
                allowedUsers = config.Channel.Telegram.AllowedUsers
            },
            email = new
            {
                enabled = config.Channel.Email.Enabled,
                imapHost = config.Channel.Email.ImapHost,
                imapPort = config.Channel.Email.ImapPort,
                smtpHost = config.Channel.Email.SmtpHost,
                smtpPort = config.Channel.Email.SmtpPort,
                address = config.Channel.Email.Address,
                password = config.Channel.Email.Password
            },
            webSocket = new
            {
                connectedClients = clients.Count,
                status = "running"
            }
        };

        await SendJsonResponse(ctx, new { success = true, data });
    }

    private async Task HandleUpdateChannelsConfigRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<ChannelUpdateRequest>(ctx);
        if (body == null)
        {
            await SendErrorResponse(ctx, 400, "Invalid request body");
            return;
        }

        var config = _gateway.Config;
        var changes = new List<string>();

        // ILink updates
        if (body.ILinkEnabled.HasValue)
        {
            config.Channel.ILink.Enabled = body.ILinkEnabled.Value;
            changes.Add($"ILink.Enabled={body.ILinkEnabled.Value}");
        }
        if (!string.IsNullOrWhiteSpace(body.ILinkToken))
        {
            config.Channel.ILink.Token = body.ILinkToken;
            changes.Add("ILink.Token=***");
        }
        if (!string.IsNullOrWhiteSpace(body.ILinkWebSocketUrl)) config.Channel.ILink.WebSocketUrl = body.ILinkWebSocketUrl;
        if (!string.IsNullOrWhiteSpace(body.ILinkSendUrl)) config.Channel.ILink.SendUrl = body.ILinkSendUrl;
        if (!string.IsNullOrWhiteSpace(body.ILinkWebhookPath)) config.Channel.ILink.WebhookPath = body.ILinkWebhookPath;
        if (!string.IsNullOrWhiteSpace(body.ILinkDefaultUserId)) config.Channel.ILink.DefaultUserId = body.ILinkDefaultUserId;

        // Telegram updates
        if (body.TelegramEnabled.HasValue)
        {
            config.Channel.Telegram.Enabled = body.TelegramEnabled.Value;
            changes.Add($"Telegram.Enabled={body.TelegramEnabled.Value}");
        }
        if (!string.IsNullOrWhiteSpace(body.BotToken))
        {
            config.Channel.Telegram.BotToken = body.BotToken;
            changes.Add("Telegram.BotToken=***");
        }
        if (body.AllowedUsers != null)
        {
            config.Channel.Telegram.AllowedUsers = body.AllowedUsers;
            changes.Add($"Telegram.AllowedUsers=[{string.Join(",", body.AllowedUsers)}]");
        }

        // Email updates
        if (body.EmailEnabled.HasValue)
        {
            config.Channel.Email.Enabled = body.EmailEnabled.Value;
            changes.Add($"Email.Enabled={body.EmailEnabled.Value}");
        }
        if (!string.IsNullOrWhiteSpace(body.ImapHost)) config.Channel.Email.ImapHost = body.ImapHost;
        if (body.ImapPort.HasValue) config.Channel.Email.ImapPort = body.ImapPort.Value;
        if (!string.IsNullOrWhiteSpace(body.SmtpHost)) config.Channel.Email.SmtpHost = body.SmtpHost;
        if (body.SmtpPort.HasValue) config.Channel.Email.SmtpPort = body.SmtpPort.Value;
        if (!string.IsNullOrWhiteSpace(body.EmailAddress)) config.Channel.Email.Address = body.EmailAddress;
        if (!string.IsNullOrWhiteSpace(body.EmailPassword))
        {
            config.Channel.Email.Password = body.EmailPassword;
            changes.Add("Email.Password=***");
        }

        ConfigLoader.Save(config);

        if (changes.Count > 0)
        {
            ColorLog.Success("CHANNEL", $"频道配置已更新: {string.Join(", ", changes)}");
        }
        else
        {
            ColorLog.Info("CHANNEL", "频道配置已保存 (无变更)");
        }

        await SendJsonResponse(ctx, new { success = true, message = "Channel config saved" });
    }

    // ===== Session API =====

    private async Task HandleGetSessionsRequest(HttpListenerContext ctx)
    {
        var sessions = _memoryService.GetAllSessions();
        var data = sessions.Select(s => new
        {
            fileName = s.FileName,
            messageCount = s.MessageCount,
            soulCount = s.SoulCount,
            lastModified = s.LastModified.ToString("o"),
            size = s.Size,
            isCurrent = s.IsCurrent
        });

        await SendJsonResponse(ctx, new { success = true, data });
    }

    private async Task HandleCreateSessionRequest(HttpListenerContext ctx)
    {
        var newDbName = _memoryService.CreateSession();
        ColorLog.Success("SESSION", $"新会话已创建: {newDbName}");
        await SendJsonResponse(ctx, new { success = true, data = new { fileName = newDbName } });
    }

    private async Task HandleSwitchSessionRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<SessionSwitchRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Session))
        {
            await SendErrorResponse(ctx, 400, "Session name is required");
            return;
        }

        var dbPath = Path.Combine("memorys", body.Session);
        if (!File.Exists(dbPath))
        {
            await SendErrorResponse(ctx, 404, "Session not found");
            return;
        }

        try
        {
            await _gateway.SwitchSession(body.Session);
            await SendJsonResponse(ctx, new { success = true, message = "Session switched", data = new { currentSession = body.Session } });
        }
        catch (Exception ex)
        {
            await SendErrorResponse(ctx, 500, $"Failed to switch session: {ex.Message}");
        }
    }

    private async Task HandleDeleteSessionRequest(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath;
        var name = Uri.UnescapeDataString(path.Replace("/api/sessions/", ""));

        if (string.IsNullOrWhiteSpace(name))
        {
            await SendErrorResponse(ctx, 400, "Session name is required");
            return;
        }

        if (name == _memoryService.CurrentDbName)
        {
            await SendErrorResponse(ctx, 400, "Cannot delete the currently active session");
            return;
        }

        try
        {
            _memoryService.DeleteSession(name);
            ColorLog.Success("SESSION", $"会话已删除: {name}");
            await SendJsonResponse(ctx, new { success = true, message = "Session deleted" });
        }
        catch (Exception ex)
        {
            await SendErrorResponse(ctx, 500, $"Failed to delete session: {ex.Message}");
        }
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
                        var reply = await _gateway.LlmService.AskAsync(GetStableHashCode(effectiveUsername), prompt, null, null, async (targetUser) =>
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

        // Webhook 暂不支持，因为 iLink4NET 使用轮询模式
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

public record SkillRequest
{
    public string? Name { get; init; }
    public string? Content { get; init; }
}

public record ConfigUpdateRequest
{
    public string? Model { get; init; }
    public string? BaseUrl { get; init; }
    public int? MaxTokens { get; init; }
    public int? MessageLimit { get; init; }
    public string? MessageSplitDelimiters { get; init; }
    public string? ImapHost { get; init; }
    public string? EmailAddress { get; init; }
    public string? MemoryFile { get; init; }
}

public record AgentSwitchRequest
{
    public string? MemoryFile { get; init; }
}

public record ChannelUpdateRequest
{
    public bool? ILinkEnabled { get; init; }
    public string? ILinkToken { get; init; }
    public string? ILinkWebSocketUrl { get; init; }
    public string? ILinkSendUrl { get; init; }
    public string? ILinkWebhookPath { get; init; }
    public string? ILinkDefaultUserId { get; init; }
    public bool? TelegramEnabled { get; init; }
    public string? BotToken { get; init; }
    public long[]? AllowedUsers { get; init; }
    public bool? EmailEnabled { get; init; }
    public string? ImapHost { get; init; }
    public int? ImapPort { get; init; }
    public string? SmtpHost { get; init; }
    public int? SmtpPort { get; init; }
    public string? EmailAddress { get; init; }
    public string? EmailPassword { get; init; }
}

public record AgentCreateUpdateRequest
{
    public string? Name { get; init; }
    public string? Content { get; init; }
}

public record SessionSwitchRequest
{
    public string? Session { get; init; }
}

public record SaveILinkCredentialsRequest
{
    public string? BotToken { get; init; }
    public string? ILinkBotId { get; init; }
    public string? ILinkUserId { get; init; }
    public string? ApiBaseUri { get; init; }
}
