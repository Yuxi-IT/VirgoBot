using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace VirgoBot.Helpers;

public class EmailManager
{
    private readonly EmailService _emailService;
    private readonly TelegramBotClient _bot;
    private readonly long _userId;
    private readonly LLMService _llmService;
    private readonly Dictionary<string, EmailMessage> _pendingEmails = new();
    private readonly List<WebSocket> _wsClients;

    public EmailManager(EmailService emailService, TelegramBotClient bot, long userId, LLMService llmService, List<WebSocket> wsClients)
    {
        _emailService = emailService;
        _bot = bot;
        _userId = userId;
        _llmService = llmService;
        _wsClients = wsClients;
    }

    public async Task StartMonitoring()
    {
        ColorLog.Info("EMAIL", "邮件监控已启动");
        while (true)
        {
            try
            {
                var newEmails = await _emailService.CheckNewEmailsAsync();

                foreach (var email in newEmails)
                {
                    _pendingEmails[email.Uid] = email;
                    await NotifyNewEmail(email);
                }
            }
            catch (Exception ex)
            {
                ColorLog.Error("EMAIL", $"监控错误: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromMinutes(1));
        }
    }

    private async Task NotifyNewEmail(EmailMessage email)
    {
        _pendingEmails[email.Uid] = email;

        ColorLog.Info("EMAIL", $"新邮件: [{email.From}] {email.Subject}");
        var prompt = $"有新邮件：\n发件人: {email.From}\n主题: {email.Subject}\n内容: {email.Body.Substring(0, Math.Min(300, email.Body.Length))}\n\nUID: {email.Uid}\n\n请用你的风格提醒我有新邮件。";

        var aiResponse = await _llmService.AskAsync(_userId, prompt);

        var keyboard = new InlineKeyboardMarkup(
        [
            [InlineKeyboardButton.WithCallbackData("忽略邮件", $"ignore_{email.Uid}")]
        ]);

        await _bot.SendMessage(_userId, aiResponse,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown, replyMarkup: keyboard);

        // 推送到 WebSocket 客户端
        var notification = JsonSerializer.Serialize(new
        {
            type = "email",
            from = email.From,
            subject = email.Subject,
            body = email.Body.Substring(0, Math.Min(300, email.Body.Length)),
            uid = email.Uid,
            message = aiResponse
        });
        var buffer = Encoding.UTF8.GetBytes(notification);

        var sentCount = 0;
        foreach (var ws in _wsClients.ToList())
        {
            if (ws.State == WebSocketState.Open)
            {
                try
                {
                    await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                    sentCount++;
                }
                catch
                {
                    _wsClients.Remove(ws);
                }
            }
        }

        if (sentCount > 0)
        {
            ColorLog.Success("WS", $"邮件通知已推送到 {sentCount} 个客户端");
        }
    }

    public async Task<bool> HandleReply(string emailUid, string replyText)
    {
        if (!_pendingEmails.TryGetValue(emailUid, out var email)) return false;

        var toAddress = email.From.Contains('<')
            ? email.From.Split('<')[1].TrimEnd('>')
            : email.From;

        await _emailService.SendEmailAsync(toAddress, $"Re: {email.Subject}", replyText);
        _pendingEmails.Remove(emailUid);
        return true;
    }

    public bool IgnoreEmail(string emailUid)
    {
        return _pendingEmails.Remove(emailUid);
    }
}
