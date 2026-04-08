using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MimeKit;
using VirgoBot.Helpers;

namespace VirgoBot.Features.Email;

public class EmailService
{
    private readonly string _imapHost;
    private readonly int _imapPort;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _email;
    private readonly string _password;
    private readonly HashSet<string> _seenUids = new();

    public EmailService(string imapHost, int imapPort, string smtpHost, int smtpPort, string email, string password)
    {
        _imapHost = imapHost;
        _imapPort = imapPort;
        _smtpHost = smtpHost;
        _smtpPort = smtpPort;
        _email = email;
        _password = password;
    }

    public async Task InitializeAsync()
    {
        using var client = new ImapClient();
        await client.ConnectAsync(_imapHost, _imapPort, true);

        client.Identify(new ImapImplementation
        {
            Name = "VirgoBot",
            Version = "1.0.0"
        });

        await client.AuthenticateAsync(_email, _password);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly);

        var uids = await inbox.SearchAsync(SearchQuery.All);
        foreach (var uid in uids)
        {
            _seenUids.Add(uid.ToString());
        }

        await client.DisconnectAsync(true);
        ColorLog.Info("EMAIL", $"已标记 {_seenUids.Count} 封现有邮件");
    }

    public async Task<List<EmailMessage>> CheckNewEmailsAsync()
    {
        var newEmails = new List<EmailMessage>();

        using var client = new ImapClient();
        await client.ConnectAsync(_imapHost, _imapPort, true);

        client.Identify(new ImapImplementation
        {
            Name = "VirgoBot",
            Version = "1.0.0"
        });

        await client.AuthenticateAsync(_email, _password);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly);

        var uids = await inbox.SearchAsync(SearchQuery.NotSeen);

        foreach (var uid in uids.Take(5))
        {
            var uidStr = uid.ToString();
            if (_seenUids.Contains(uidStr))
            {
                continue;
            }

            var message = await inbox.GetMessageAsync(uid);
            newEmails.Add(new EmailMessage
            {
                Uid = uidStr,
                From = message.From.ToString(),
                Subject = message.Subject ?? "",
                Body = message.TextBody ?? message.HtmlBody ?? "",
                Date = message.Date.DateTime
            });

            _seenUids.Add(uidStr);
        }

        await client.DisconnectAsync(true);
        return newEmails;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("", _email));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(_smtpHost, _smtpPort, MailKit.Security.SecureSocketOptions.SslOnConnect);
        await client.AuthenticateAsync(_email, _password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}

public class EmailMessage
{
    public string Uid { get; set; } = "";
    public string From { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime Date { get; set; }
}
