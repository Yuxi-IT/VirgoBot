namespace VirgoBot.Configuration;

public class Config
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string Model { get; set; } = "";
    public string MemoryFile { get; set; } = "system_memory.md";
    public string SoulFile { get; set; } = "soul.md";
    public string RuleFile { get; set; } = "RULE.md";
    public string CurrentSession { get; set; } = "";
    public ServerConfig Server { get; set; } = new();
    public ChannelConfig Channel { get; set; } = new();
}

public class ServerConfig
{
    public string ListenUrl { get; set; } = AppConstants.DefaultListenUrl;
    public int MaxTokens { get; set; } = AppConstants.DefaultMaxTokens;
    public int MessageLimit { get; set; } = AppConstants.DefaultMessageLimit;
    public string MessageSplitDelimiters { get; set; } = "。|！|？|?|\n\n|\n";
    public AutoResponseConfig AutoResponse { get; set; } = new();
}

public class AutoResponseConfig
{
    public bool Enabled { get; set; } = false;
    public int MinIdleMinutes { get; set; } = 30;
    public int MaxIdleMinutes { get; set; } = 120;
}

public class ChannelConfig
{
    public ILinkChannelConfig ILink { get; set; } = new();
    public TelegramChannelConfig Telegram { get; set; } = new();
    public EmailChannelConfig Email { get; set; } = new();
}

public class TelegramChannelConfig
{
    public bool Enabled { get; set; } = false;
    public string BotToken { get; set; } = "";
    public long[] AllowedUsers { get; set; } = Array.Empty<long>();
}

public class EmailChannelConfig
{
    public bool Enabled { get; set; } = false;
    public string ImapHost { get; set; } = "";
    public int ImapPort { get; set; } = 993;
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string Address { get; set; } = "";
    public string Password { get; set; } = "";
    public EmailNotificationConfig Notification { get; set; } = new();
}

public class EmailNotificationConfig
{
    public bool NotifyToTelegram { get; set; } = false;
    public bool NotifyToILink { get; set; } = false;
    public bool NotifyToWebSocket { get; set; } = false;
}

public class ILinkChannelConfig
{
    public bool Enabled { get; set; } = false;
    public string Token { get; set; } = "";
    public string WebSocketUrl { get; set; } = "";
    public string SendUrl { get; set; } = "";
    public string WebhookPath { get; set; } = "/ilink/webhook";
    public string DefaultUserId { get; set; } = "ilink";
}
