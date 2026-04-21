using System.Text.Json;
using VirgoBot.Services;
using VirgoBot.Utilities;

namespace VirgoBot.Configuration;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static Config Load()
    {
        var configDir = AppConstants.ConfigDirectory;
        var configPath = Path.Combine(configDir, AppConstants.ConfigFileName);

        Directory.CreateDirectory(configDir);

        if (!File.Exists(configPath))
        {
            var defaultConfig = new Config
            {
                ApiKey = "YOUR_API_KEY",
                BaseUrl = "https://localhost/",
                Model = "gpt-4.5",
                Channel = new ChannelConfig
                {
                    Telegram = new TelegramChannelConfig
                    {
                        Enabled = false,
                        BotToken = "YOUR_BOT_TOKEN",
                        AllowedUsers = Array.Empty<long>()
                    },
                    Email = new EmailChannelConfig
                    {
                        Enabled = false,
                        ImapHost = "imap.example.com",
                        ImapPort = 993,
                        SmtpHost = "smtp.example.com",
                        SmtpPort = 587,
                        Address = "your@email.com",
                        Password = "your_password"
                    },
                    ILink = new ILinkChannelConfig
                    {
                        Enabled = false,
                        Token = ""
                    }
                }
            };
            File.WriteAllText(configPath, JsonSerializer.Serialize(defaultConfig, JsonOptions));
            ColorLog.Info("CONFIG", $"已创建默认配置文件: {configPath}");
        }

        var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath), JsonOptions)
            ?? throw new InvalidOperationException($"无法解析配置文件: {configPath}");

        if (config.Channel.Telegram.AllowedUsers.Length == 0)
        { 
            //throw new InvalidOperationException("配置错误: Channel.Telegram.AllowedUsers 不能为空，请在 config.json 中添加至少一个用户 ID");
        }

        return config;
    }

    public static string LoadSystemMemory(Config config, MemoryService memoryService)
    {
        var configDir = AppConstants.ConfigDirectory;
        var memoryPath = Path.Combine(configDir, config.MemoryFile);
        var soulPath = Path.Combine(configDir, config.SoulFile);
        var rulePath = Path.Combine(configDir, config.RuleFile);

        if (!File.Exists(memoryPath))
        {
            File.WriteAllText(memoryPath, "You are a helpful assistant.");
            ColorLog.Info("MEMORY", $"已创建默认记忆文件: {memoryPath}");
        }

        if (!File.Exists(rulePath))
        {
            File.WriteAllText(rulePath, "");
            ColorLog.Info("RULE", $"已创建默认规则文件: {rulePath}");
        }

        if (File.Exists(soulPath))
        {
            var soulFileContent = File.ReadAllText(soulPath);
            if (!string.IsNullOrWhiteSpace(soulFileContent))
            {
                memoryService.AddSoulEntry(soulFileContent);
                File.Delete(soulPath);
                ColorLog.Success("MEMORY", "Soul 文件已迁移到数据库");
            }
        }

        var systemMemory = File.ReadAllText(memoryPath);

        var soulContent = memoryService.GetAllSoulContent();
        if (!string.IsNullOrWhiteSpace(soulContent))
        {
            systemMemory = $"{systemMemory.Replace("{{EMAIL}}", config.Channel.Email.Address)}\n\nyour SoulMemory: \n{soulContent}";
        }
        else
        {
            systemMemory = systemMemory.Replace("{{EMAIL}}", config.Channel.Email.Address);
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
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(configPath, json);
        ColorLog.Success("CONFIG", "配置已保存");
    }
}
