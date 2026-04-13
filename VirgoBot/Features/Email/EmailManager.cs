using VirgoBot.Services;
using VirgoBot.Utilities;

namespace VirgoBot.Features.Email;

public class EmailManager
{
    private readonly EmailService _emailService;
    private readonly EmailNotificationDispatcher _notificationDispatcher;
    private readonly long _userId;
    private readonly LLMService _llmService;
    private readonly Dictionary<string, EmailMessage> _pendingEmails = new();

    public EmailManager(
        EmailService emailService,
        EmailNotificationDispatcher notificationDispatcher,
        long userId,
        LLMService llmService)
    {
        _emailService = emailService;
        _notificationDispatcher = notificationDispatcher;
        _userId = userId;
        _llmService = llmService;
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
        var prompt = $"有新邮件：\n发件人: {email.From}\n主题: {email.Subject}\n内容: {GetPreview(email.Body)}\n\nUID: {email.Uid}\n\n请用你的风格提醒我有新邮件。";

        var aiResponse = await _llmService.AskAsync(_userId, prompt);
        await _notificationDispatcher.DispatchNewEmailAsync(email, aiResponse);
    }

    public async Task<bool> HandleReply(string emailUid, string replyText)
    {
        if (!_pendingEmails.TryGetValue(emailUid, out var email))
        {
            return false;
        }

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

    private static string GetPreview(string body)
    {
        return body[..Math.Min(300, body.Length)];
    }
}
