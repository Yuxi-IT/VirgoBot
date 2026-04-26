using System.Net;
using VirgoBot.Services;
using VirgoBot.Configuration;
using VirgoBot.Utilities;
using static VirgoBot.Channels.Handlers.HttpResponseHelper;

namespace VirgoBot.Channels.Handlers;

public class StatusApiHandler
{
    private readonly Gateway _gateway;
    private readonly MemoryService _memoryService;
    private readonly LogService _logService;
    private readonly WebSocketClientManager _wsManager;
    private static readonly DateTime StartTime = DateTime.UtcNow;

    public StatusApiHandler(Gateway gateway, MemoryService memoryService, LogService logService, WebSocketClientManager wsManager)
    {
        _gateway = gateway;
        _memoryService = memoryService;
        _logService = logService;
        _wsManager = wsManager;
    }

    public async Task HandleStatusRequest(HttpListenerContext ctx)
    {
        var uptime = DateTime.UtcNow - StartTime;
        var uptimeStr = uptime.TotalHours >= 1
            ? $"{(int)uptime.TotalHours}h {uptime.Minutes}m"
            : $"{uptime.Minutes}m {uptime.Seconds}s";

        var clients = _wsManager.GetSnapshot();

        var provider = ConfigLoader.GetCurrentProvider(_gateway.Config);
        var data = new
        {
            success = true,
            data = new
            {
                botName = "VirgoBot",
                model = provider?.CurrentModel ?? "",
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
                },
                tokenStats = GetTokenStatsData()
            }
        };

        await SendJsonResponse(ctx, data);
    }

    public async Task HandleGetMessagesRequest(HttpListenerContext ctx)
    {
        var limitStr = GetQueryParam(ctx, "limit") ?? "50";
        var offsetStr = GetQueryParam(ctx, "offset") ?? "0";

        int.TryParse(limitStr, out var limit);
        int.TryParse(offsetStr, out var offset);
        if (limit <= 0) limit = 50;
        if (offset < 0) offset = 0;

        var (messages, total) = _memoryService.LoadMessagesWithPagination(limit, offset);

        var data = new
        {
            messages = messages.Select(m => new
            {
                id = m.Id,
                role = m.Role,
                content = m.Content.Split(Environment.NewLine)[..^1],
                createdAt = m.CreatedAt.ToString("o")
            }),
            total
        };

        await SendJsonResponse(ctx, new { success = true, data });
    }

    public async Task HandleGetLogsRequest(HttpListenerContext ctx)
    {
        var level = GetQueryParam(ctx, "level");
        var limitStr = GetQueryParam(ctx, "limit") ?? "100";
        var offsetStr = GetQueryParam(ctx, "offset") ?? "0";

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

    public async Task HandleClearLogsRequest(HttpListenerContext ctx)
    {
        _logService.Clear();
        ColorLog.Info("LOGS", "日志已清空");
        await SendJsonResponse(ctx, new { success = true, message = "Logs cleared" });
    }

    public async Task HandleGatewayRestartRequest(HttpListenerContext ctx)
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

    public async Task HandleGatewayStatusRequest(HttpListenerContext ctx)
    {
        var gwProvider = ConfigLoader.GetCurrentProvider(_gateway.Config);
        var data = new
        {
            isRunning = _gateway.IsRunning,
            channels = _gateway.ChannelStatuses.ToDictionary(
                kv => kv.Key,
                kv => new { kv.Value.Name, kv.Value.Enabled, kv.Value.Status }),
            config = new
            {
                model = gwProvider?.CurrentModel ?? "",
                baseUrl = gwProvider?.BaseUrl ?? "",
                maxTokens = _gateway.Config.Server.MaxTokens,
                messageLimit = _gateway.Config.Server.MessageLimit
            }
        };

        await SendJsonResponse(ctx, new { success = true, data });
    }

    public async Task HandleDeleteMessageRequest(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath;
        var idStr = path.Replace("/api/messages/", "");

        if (!int.TryParse(idStr, out var messageId))
        {
            await SendErrorResponse(ctx, 400, "Invalid message ID");
            return;
        }

        _memoryService.DeleteMessage(messageId);
        await SendJsonResponse(ctx, new { success = true, message = "Message deleted" });
    }

    public async Task HandleTokenStatsRequest(HttpListenerContext ctx)
    {
        await SendJsonResponse(ctx, new { success = true, data = GetTokenStatsData() });
    }

    private object GetTokenStatsData()
    {
        var stats = _gateway.TokenStatsService.GetStats();
        return new
        {
            promptTokens = stats.PromptTokens,
            completionTokens = stats.CompletionTokens,
            totalTokens = stats.TotalTokens,
            requestCount = stats.RequestCount
        };
    }
}
