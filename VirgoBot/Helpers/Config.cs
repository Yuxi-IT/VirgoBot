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
