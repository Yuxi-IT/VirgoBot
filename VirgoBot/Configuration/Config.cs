namespace VirgoBot.Configuration;

public class Config
{
    public string BotToken { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string Model { get; set; } = "";
    public long[] AllowedUsers { get; set; } = Array.Empty<long>();
    public EmailConfig Email { get; set; } = new();
    public string MemoryFile { get; set; } = "system_memory.md";
    public string SoulFile { get; set; } = "soul.md";
    public string RuleFile { get; set; } = "RULE.md";
    public ILinkConfig ILink { get; set; } = new();
    public ServerConfig Server { get; set; } = new();
}

public class ServerConfig
{
    public string ListenUrl { get; set; } = AppConstants.DefaultListenUrl;
    public int MaxTokens { get; set; } = AppConstants.DefaultMaxTokens;
    public int MessageLimit { get; set; } = AppConstants.DefaultMessageLimit;
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
