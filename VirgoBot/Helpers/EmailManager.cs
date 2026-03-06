using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace VirgoBot.Helpers;

public class EmailManager
{
    private readonly EmailService _emailService;
    private readonly TelegramBotClient _bot;
    private readonly long _userId;
    private readonly ClaudeService _claudeService;
    private readonly Dictionary<string, EmailMessage> _pendingEmails = new();

    public EmailManager(EmailService emailService, TelegramBotClient bot, long userId, ClaudeService claudeService)
    {
        _emailService = emailService;
        _bot = bot;
        _userId = userId;
        _claudeService = claudeService;
    }

    public async Task StartMonitoring()
    {
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
                Console.WriteLine($"[EMAIL ERROR] {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromMinutes(1));
        }
    }

    private async Task NotifyNewEmail(EmailMessage email)
    {
        _pendingEmails[email.Uid] = email;

        var prompt = $"有新邮件：\n发件人: {email.From}\n主题: {email.Subject}\n内容: {email.Body.Substring(0, Math.Min(300, email.Body.Length))}\n\n请用你的风格提醒我有新邮件。";

        var aiResponse = await _claudeService.AskAsync(_userId, prompt);

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("忽略邮件", $"ignore_{email.Uid}") }
        });

        await _bot.SendMessage(_userId, $"{aiResponse}\n\n<b>UID:</b> {email.Uid}",
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: keyboard);
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
