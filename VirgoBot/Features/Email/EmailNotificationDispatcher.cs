using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using VirgoBot.Configuration;
using VirgoBot.Integrations.ILink;
using VirgoBot.Services;
using VirgoBot.Utilities;

namespace VirgoBot.Features.Email;

public class EmailNotificationDispatcher
{
    private readonly EmailNotificationConfig _notificationConfig;
    private readonly TelegramBotClient? _bot;
    private readonly long _userId;
    private readonly WebSocketClientManager _wsManager;
    private readonly ILinkBridgeService? _iLinkBridge;

    public EmailNotificationDispatcher(
        EmailNotificationConfig notificationConfig,
        WebSocketClientManager wsManager,
        TelegramBotClient? bot = null,
        long userId = 0,
        ILinkBridgeService? iLinkBridge = null)
    {
        _notificationConfig = notificationConfig;
        _bot = bot;
        _userId = userId;
        _wsManager = wsManager;
        _iLinkBridge = iLinkBridge;
    }

    public async Task DispatchNewEmailAsync(EmailMessage email, string aiResponse, CancellationToken cancellationToken = default)
    {
        if (_notificationConfig.NotifyToTelegram && _bot != null)
        {
            await SendTelegramNotificationAsync(email, aiResponse, cancellationToken);
        }

        if (_notificationConfig.NotifyToWebSocket)
        {
            await SendWebSocketNotificationAsync(email, aiResponse, cancellationToken);
        }

        if (_notificationConfig.NotifyToILink && _iLinkBridge != null)
        {
            await SendILinkNotificationAsync(email, aiResponse, cancellationToken);
        }
    }

    private async Task SendTelegramNotificationAsync(EmailMessage email, string aiResponse, CancellationToken cancellationToken)
    {
        if (_bot == null) return;

        var keyboard = new InlineKeyboardMarkup(
        [
            [InlineKeyboardButton.WithCallbackData("忽略邮件", $"ignore_{email.Uid}")]
        ]);

        await _bot.SendMessage(
            _userId,
            aiResponse,
            parseMode: ParseMode.Markdown,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task SendWebSocketNotificationAsync(EmailMessage email, string aiResponse, CancellationToken cancellationToken)
    {
        var notification = JsonSerializer.Serialize(new
        {
            type = "email",
            from = email.From,
            subject = email.Subject,
            body = GetPreview(email.Body),
            uid = email.Uid,
            message = aiResponse
        });

        await _wsManager.BroadcastAsync(notification, cancellationToken);
    }

    private async Task SendILinkNotificationAsync(EmailMessage email, string aiResponse, CancellationToken cancellationToken)
    {
        if (_iLinkBridge is null || !_iLinkBridge.IsEnabled)
        {
            return;
        }

        try
        {
            var message = BuildILinkMessage(email, aiResponse);
            // 注意：iLink 需要原始 IncomingMessage 才能回复，邮件通知暂不支持
            ColorLog.Warning("ILINK", "邮件通知到 iLink 暂不支持（需要原始消息上下文）");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            ColorLog.Error("ILINK", $"邮件通知推送失败: {ex.Message}");
        }
    }

    private static string BuildILinkMessage(EmailMessage email, string aiResponse)
    {
        var preview = GetPreview(email.Body);
        return $"新邮件提醒\n发件人: {email.From}\n主题: {email.Subject}\n摘要: {preview}\nUID: {email.Uid}\n\n{aiResponse}";
    }

    private static string GetPreview(string body)
    {
        return body[..Math.Min(300, body.Length)];
    }
}
