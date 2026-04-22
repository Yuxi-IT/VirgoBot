using System.Net;
using VirgoBot.Configuration;
using VirgoBot.Services;
using VirgoBot.Utilities;
using static VirgoBot.Channels.Handlers.HttpResponseHelper;

namespace VirgoBot.Channels.Handlers;

public class ConfigApiHandler
{
    private readonly Gateway _gateway;
    private readonly MemoryService _memoryService;

    public ConfigApiHandler(Gateway gateway, MemoryService memoryService)
    {
        _gateway = gateway;
        _memoryService = memoryService;
    }

    public async Task HandleGetConfigRequest(HttpListenerContext ctx)
    {
        var config = _gateway.Config;
        var provider = ConfigLoader.GetCurrentProvider(config);
        var data = new
        {
            model = provider?.CurrentModel ?? "",
            baseUrl = provider?.BaseUrl ?? "",
            apiKey = MaskSecret(provider?.ApiKey ?? ""),
            currentProvider = config.CurrentProvider,
            memoryFile = config.MemoryFile,
            server = new
            {
                listenUrl = config.Server.ListenUrl,
                maxTokens = config.Server.MaxTokens,
                messageLimit = config.Server.MessageLimit,
                messageSplitDelimiters = config.Server.MessageSplitDelimiters,
                autoResponse = new
                {
                    enabled = config.Server.AutoResponse.Enabled,
                    minIdleMinutes = config.Server.AutoResponse.MinIdleMinutes,
                    maxIdleMinutes = config.Server.AutoResponse.MaxIdleMinutes
                }
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
                }
            }
        };

        await SendJsonResponse(ctx, new { success = true, data });
    }

    public async Task HandleUpdateConfigRequest(HttpListenerContext ctx)
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
            var provider = ConfigLoader.GetCurrentProvider(config);

            if (provider != null)
            {
                if (!string.IsNullOrWhiteSpace(body.Model)) provider.CurrentModel = body.Model;
                if (!string.IsNullOrWhiteSpace(body.BaseUrl)) provider.BaseUrl = body.BaseUrl;
            }
            if (body.MaxTokens.HasValue) config.Server.MaxTokens = body.MaxTokens.Value;
            if (body.MessageLimit.HasValue) config.Server.MessageLimit = body.MessageLimit.Value;
            if (!string.IsNullOrWhiteSpace(body.MessageSplitDelimiters)) config.Server.MessageSplitDelimiters = body.MessageSplitDelimiters;
            if (body.AutoResponseEnabled.HasValue) config.Server.AutoResponse.Enabled = body.AutoResponseEnabled.Value;
            if (body.AutoResponseMinIdle.HasValue) config.Server.AutoResponse.MinIdleMinutes = body.AutoResponseMinIdle.Value;
            if (body.AutoResponseMaxIdle.HasValue) config.Server.AutoResponse.MaxIdleMinutes = body.AutoResponseMaxIdle.Value;
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

    public async Task HandleGetSystemMemoryRequest(HttpListenerContext ctx)
    {
        var memoryPath = Path.Combine(AppConstants.ConfigDirectory, _gateway.Config.MemoryFile);
        var content = File.Exists(memoryPath) ? await File.ReadAllTextAsync(memoryPath) : "";
        await SendJsonResponse(ctx, new { success = true, data = new { content } });
    }

    public async Task HandleUpdateSystemMemoryRequest(HttpListenerContext ctx)
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

    public async Task HandleGetSoulRequest(HttpListenerContext ctx)
    {
        var soulPath = Path.Combine(AppConstants.ConfigDirectory, _gateway.Config.SoulFile);
        var content = File.Exists(soulPath) ? await File.ReadAllTextAsync(soulPath) : "";
        await SendJsonResponse(ctx, new { success = true, data = new { content } });
    }

    public async Task HandleUpdateSoulRequest(HttpListenerContext ctx)
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

    public async Task HandleGetRuleRequest(HttpListenerContext ctx)
    {
        var rulePath = Path.Combine(AppConstants.ConfigDirectory, _gateway.Config.RuleFile);
        var content = File.Exists(rulePath) ? await File.ReadAllTextAsync(rulePath) : "";
        await SendJsonResponse(ctx, new { success = true, data = new { content } });
    }

    public async Task HandleUpdateRuleRequest(HttpListenerContext ctx)
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

    private static string MaskSecret(string secret)
    {
        if (string.IsNullOrEmpty(secret) || secret.Length <= 8)
            return "****";
        return secret[..4] + "****" + secret[^4..];
    }
}

public record ContentRequest
{
    public string? Content { get; init; }
}

public record ConfigUpdateRequest
{
    public string? Model { get; init; }
    public string? BaseUrl { get; init; }
    public int? MaxTokens { get; init; }
    public int? MessageLimit { get; init; }
    public string? MessageSplitDelimiters { get; init; }
    public bool? AutoResponseEnabled { get; init; }
    public int? AutoResponseMinIdle { get; init; }
    public int? AutoResponseMaxIdle { get; init; }
    public string? ImapHost { get; init; }
    public string? EmailAddress { get; init; }
    public string? MemoryFile { get; init; }
}
