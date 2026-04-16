using System.Net;
using VirgoBot.Configuration;
using VirgoBot.Integrations.ILink;
using VirgoBot.Services;
using VirgoBot.Utilities;
using static VirgoBot.Channels.Handlers.HttpResponseHelper;

namespace VirgoBot.Channels.Handlers;

public class ChannelApiHandler
{
    private readonly Gateway _gateway;
    private readonly ILinkLoginService _iLinkLoginService;

    public ChannelApiHandler(Gateway gateway, ILinkLoginService iLinkLoginService)
    {
        _gateway = gateway;
        _iLinkLoginService = iLinkLoginService;
    }

    public async Task HandleGetChannelsConfigRequest(HttpListenerContext ctx)
    {
        var config = _gateway.Config;

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
            }
        };

        await SendJsonResponse(ctx, new { success = true, data });
    }

    public async Task HandleUpdateChannelsConfigRequest(HttpListenerContext ctx)
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

    public async Task HandleCreateILinkQrCodeRequest(HttpListenerContext ctx)
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

    public async Task HandleQueryILinkLoginStatusRequest(HttpListenerContext ctx)
    {
        try
        {
            var qrId = GetQueryParam(ctx, "qrId");
            if (string.IsNullOrWhiteSpace(qrId))
            {
                await SendErrorResponse(ctx, 400, "qrId is required");
                return;
            }

            var result = await _iLinkLoginService.QueryStatusAsync(qrId);
            await SendJsonResponse(ctx, new { success = true, data = result });
        }
        catch (Exception ex)
        {
            ColorLog.Error("ILINK-LOGIN", $"查询登录状态失败: {ex.Message}");
            await SendJsonResponse(ctx, new { success = false, error = ex.Message }, 500);
        }
    }

    public async Task HandleSaveILinkCredentialsRequest(HttpListenerContext ctx)
    {
        try
        {
            var body = await ReadRequestBody<SaveILinkCredentialsRequest>(ctx);
            if (body == null)
            {
                await SendErrorResponse(ctx, 400, "Invalid request body");
                return;
            }

            ColorLog.Info("ILINK-LOGIN", $"保存凭证: BotToken={body.BotToken?[..10]}..., BotId={body.ILinkBotId}, UserId={body.ILinkUserId}, ApiBaseUri={body.ApiBaseUri}");

            var config = _gateway.Config;
            if (!string.IsNullOrWhiteSpace(body.BotToken)) config.Channel.ILink.Token = body.BotToken;
            if (!string.IsNullOrWhiteSpace(body.ILinkUserId)) config.Channel.ILink.DefaultUserId = body.ILinkUserId;
            if (!string.IsNullOrWhiteSpace(body.ApiBaseUri))
            {
                config.Channel.ILink.WebSocketUrl = $"{body.ApiBaseUri}/bot/v1/ws?token={body.BotToken}";
                config.Channel.ILink.SendUrl = $"{body.ApiBaseUri}/bot/v1/message/send";
            }

            ConfigLoader.Save(config);
            ColorLog.Success("ILINK-LOGIN", "iLink 凭证已保存到配置文件");

            await SendJsonResponse(ctx, new { success = true });
        }
        catch (Exception ex)
        {
            ColorLog.Error("ILINK-LOGIN", $"保存凭证失败: {ex.Message}");
            await SendJsonResponse(ctx, new { success = false, error = ex.Message }, 500);
        }
    }
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

public record SaveILinkCredentialsRequest
{
    public string? BotToken { get; init; }
    public string? ILinkBotId { get; init; }
    public string? ILinkUserId { get; init; }
    public string? ApiBaseUri { get; init; }
}
