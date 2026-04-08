namespace VirgoBot.Helpers;

public class Config
{
    public string BotToken { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string Model { get; set; } = "";
    public long[] AllowedUsers { get; set; } = Array.Empty<long>();
    public EmailConfig Email { get; set; } = new();
    public string MemoryFile { get; set; } = "";
    public ILinkConfig ILink { get; set; } = new();
}

public class EmailConfig
{
    public string ImapHost { get; set; } = "";
    public int ImapPort { get; set; }
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; }
    public string Address { get; set; } = "";
    public string Password { get; set; } = "";
}

public class ILinkConfig
{
    public bool Enabled { get; set; }
    public string Token { get; set; } = "";
    public string WebSocketUrl { get; set; } = "";
    public string SendUrl { get; set; } = "";
    public string WebhookPath { get; set; } = "/ilink/webhook";
    public string DefaultUserId { get; set; } = "ilink";
}
