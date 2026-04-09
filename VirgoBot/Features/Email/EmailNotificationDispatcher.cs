using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using VirgoBot.Helpers;
using VirgoBot.Integrations.ILink;

namespace VirgoBot.Features.Email;

public class EmailNotificationDispatcher
{
    private readonly TelegramBotClient _bot;
    private readonly long _userId;
    private readonly List<WebSocket> _wsClients;
    private readonly ILinkBridgeService? _iLinkBridge;

    public EmailNotificationDispatcher(
        TelegramBotClient bot,
        long userId,
        List<WebSocket> wsClients,
        ILinkBridgeService? iLinkBridge = null)
    {
        _bot = bot;
        _userId = userId;
        _wsClients = wsClients;
        _iLinkBridge = iLinkBridge;
    }

    public async Task DispatchNewEmailAsync(EmailMessage email, string aiResponse, CancellationToken cancellationToken = default)
    {
        await SendTelegramNotificationAsync(email, aiResponse, cancellationToken);
        await SendWebSocketNotificationAsync(email, aiResponse, cancellationToken);
        await SendILinkNotificationAsync(email, aiResponse, cancellationToken);
    }

    private async Task SendTelegramNotificationAsync(EmailMessage email, string aiResponse, CancellationToken cancellationToken)
    {
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

        var buffer = Encoding.UTF8.GetBytes(notification);
        var sentCount = 0;

        foreach (var ws in _wsClients.ToList())
        {
            if (ws.State != WebSocketState.Open)
            {
                continue;
            }

            try
            {
                await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cancellationToken);
                sentCount++;
            }
            catch
            {
                _wsClients.Remove(ws);
            }
        }

        if (sentCount > 0)
        {
            ColorLog.Success("WS", $"邮件通知已推送到 {sentCount} 个客户端");
        }
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
            await _iLinkBridge.SendLongMessageAsync(message, cancellationToken);
            ColorLog.Success("ILINK", $"邮件通知已推送到 iLink: {email.Subject}");
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
