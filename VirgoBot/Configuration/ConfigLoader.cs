using System.Text.Json;
using VirgoBot.Services;
using VirgoBot.Utilities;

namespace VirgoBot.Configuration;

public static class ConfigLoader
{
    public static Config Load()
    {
        var configDir = AppConstants.ConfigDirectory;
        var configPath = Path.Combine(configDir, AppConstants.ConfigFileName);

        Directory.CreateDirectory(configDir);

        if (!File.Exists(configPath))
        {
            var defaultConfig = new Config
            {
                BotToken = "YOUR_BOT_TOKEN",
                ApiKey = "YOUR_API_KEY",
                BaseUrl = "https://localhost/",
                Model = "gpt-4.5",
                AllowedUsers = Array.Empty<long>(),
                Email = new EmailConfig
                {
                    ImapHost = "imap.example.com",
                    ImapPort = 993,
                    SmtpHost = "smtp.example.com",
                    SmtpPort = 587,
                    Address = "your@email.com",
                    Password = "your_password"
                },
                ILink = new ILinkConfig
                {
                    Enabled = false,
                    Token = "YOUR_ILINK_TOKEN",
                    WebSocketUrl = "wss://localhost/bot/v1/ws?token=YOUR_ILINK_TOKEN",
                    SendUrl = "http:/localhost/bot/v1/message/send",
                    WebhookPath = "/ilink/webhook",
                    DefaultUserId = "ilink"
                }
            };
            File.WriteAllText(configPath, JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));
            ColorLog.Info("CONFIG", $"已创建默认配置文件: {configPath}");
        }

        var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath))
            ?? throw new InvalidOperationException($"无法解析配置文件: {configPath}");

        if (config.AllowedUsers.Length == 0)
        {
            throw new InvalidOperationException("配置错误: AllowedUsers 不能为空，请在 config.json 中添加至少一个用户 ID");
        }

        return config;
    }

    public static string LoadSystemMemory(Config config, MemoryService memoryService)
    {
        var memoryPath = Path.Combine(AppConstants.ConfigDirectory, config.MemoryFile);
        var soulPath = Path.Combine(AppConstants.ConfigDirectory, config.SoulFile);
        var rulePath = Path.Combine(AppConstants.ConfigDirectory, config.RuleFile);

        if (!File.Exists(memoryPath))
        {
            File.WriteAllText(memoryPath, "# 系统记忆\n\n你是 Virgo，一个智能助手。\n\n## 能力\n- 邮件收发管理\n- 文件读写操作\n- Shell命令执行\n- 网页浏览\n- 通讯录管理\n");
            ColorLog.Info("MEMORY", $"已创建默认记忆文件: {memoryPath}");
        }

        if (!File.Exists(rulePath))
        {
            File.WriteAllText(rulePath, "# Rules\n\n在此添加自定义规则，这些规则会自动加载到系统提示中。\n");
            ColorLog.Info("RULE", $"已创建默认规则文件: {rulePath}");
        }

        // Migrate soul.md to database on first run
        if (memoryService.GetSoulCount() == 0 && File.Exists(soulPath))
        {
            var soulFileContent = File.ReadAllText(soulPath);
            if (!string.IsNullOrWhiteSpace(soulFileContent))
            {
                memoryService.AddSoulEntry(soulFileContent);
                ColorLog.Success("MEMORY", "Soul 文件已迁移到数据库");
            }
        }

        var systemMemory = File.ReadAllText(memoryPath);

        // Read soul from database instead of file
        var soulContent = memoryService.GetAllSoulContent();
        if (!string.IsNullOrWhiteSpace(soulContent))
        {
            systemMemory = $"{systemMemory.Replace("{{EMAIL}}", config.Email.Address)}\n\nyour SoulMemory: \n{soulContent}";
        }
        else
        {
            systemMemory = systemMemory.Replace("{{EMAIL}}", config.Email.Address);
        }

        var ruleContent = File.ReadAllText(rulePath);
        if (!string.IsNullOrWhiteSpace(ruleContent))
        {
            systemMemory = $"{ruleContent}\n{systemMemory}";
        }

        ColorLog.Info("MEMORY", $"记忆已加载, [{systemMemory.Length}]Tokens");
        return systemMemory;
    }

    public static void Save(Config config)
    {
        var configPath = Path.Combine(AppConstants.ConfigDirectory, AppConstants.ConfigFileName);
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(configPath, json);
        ColorLog.Success("CONFIG", "配置已保存");
    }
}
