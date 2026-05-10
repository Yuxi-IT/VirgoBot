using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    private const string EncryptedPrefix = "ENC:";
    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "ApiKey", "Password", "Token", "BotToken"
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
                Providers = new List<ProviderConfig>
                {
                    new()
                    {
                        Name = "default",
                        ApiKey = "YOUR_API_KEY",
                        BaseUrl = "https://localhost/",
                        CurrentModel = "gpt-4.5",
                        Protocol = "openai"
                    }
                },
                CurrentProvider = "default",
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

        var json = File.ReadAllText(configPath);

        // Decrypt sensitive fields before deserialization
        var jsonNode = JsonNode.Parse(json);
        if (jsonNode != null)
        {
            DecryptSensitiveFields(jsonNode);
            json = jsonNode.ToJsonString(JsonOptions);
        }

        var config = JsonSerializer.Deserialize<Config>(json, JsonOptions)
            ?? throw new InvalidOperationException($"无法解析配置文件: {configPath}");

        // Migrate legacy format: if Providers is empty, check for old apiKey/baseUrl/model fields
        if (config.Providers.Count == 0)
        {
            MigrateLegacyConfig(config, json);
        }

        return config;
    }

    private static void MigrateLegacyConfig(Config config, string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            var apiKey = TryGetString(root, "apiKey") ?? "";
            var baseUrl = TryGetString(root, "baseUrl") ?? "";
            var model = TryGetString(root, "model") ?? "";

            if (string.IsNullOrWhiteSpace(apiKey) && string.IsNullOrWhiteSpace(baseUrl))
                return;

            var protocol = "openai";
            if (root.TryGetProperty("apiStandard", out var stdEl))
            {
                var stdStr = stdEl.ValueKind == JsonValueKind.String ? stdEl.GetString() : stdEl.ToString();
                if (stdStr?.Equals("Anthropic", StringComparison.OrdinalIgnoreCase) == true)
                    protocol = "anthropic";
                else if (stdStr?.Equals("Gemini", StringComparison.OrdinalIgnoreCase) == true)
                    protocol = "gemini";
            }

            var provider = new ProviderConfig
            {
                Name = "default",
                ApiKey = apiKey,
                BaseUrl = baseUrl,
                CurrentModel = model,
                Protocol = protocol
            };

            config.Providers.Add(provider);
            config.CurrentProvider = "default";

            Save(config);
            ColorLog.Success("CONFIG", "旧配置已自动迁移为多供应商格式");
        }
        catch (Exception ex)
        {
            ColorLog.Error("CONFIG", $"配置迁移失败: {ex.Message}");
        }
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
    }

    public static ProviderConfig? GetCurrentProvider(Config config)
    {
        if (string.IsNullOrWhiteSpace(config.CurrentProvider))
            return config.Providers.FirstOrDefault();

        return config.Providers.FirstOrDefault(p =>
            p.Name.Equals(config.CurrentProvider, StringComparison.OrdinalIgnoreCase))
            ?? config.Providers.FirstOrDefault();
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
            var defaultRule = LoadEmbeddedRule();
            File.WriteAllText(rulePath, defaultRule);
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

        // Serialize to JSON, then encrypt sensitive fields before writing
        var json = JsonSerializer.Serialize(config, JsonOptions);
        var jsonNode = JsonNode.Parse(json);
        if (jsonNode != null)
        {
            EncryptSensitiveFields(jsonNode);
            json = jsonNode.ToJsonString(JsonOptions);
        }

        File.WriteAllText(configPath, json);
        ColorLog.Success("CONFIG", "配置已保存");
    }

    private static void EncryptSensitiveFields(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var kvp in obj)
            {
                if (kvp.Value is JsonValue jv && jv.TryGetValue<string>(out var strVal) && SensitiveFields.Contains(kvp.Key))
                {
                    // Don't double-encrypt
                    if (strVal != null && !strVal.StartsWith(EncryptedPrefix) && !string.IsNullOrEmpty(strVal))
                    {
                        var plainBytes = Encoding.UTF8.GetBytes(strVal);
                        var encrypted = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                        obj[kvp.Key] = EncryptedPrefix + Convert.ToBase64String(encrypted);
                    }
                }
                else if (kvp.Value != null)
                {
                    EncryptSensitiveFields(kvp.Value);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item != null)
                    EncryptSensitiveFields(item);
            }
        }
    }

    private static void DecryptSensitiveFields(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var kvp in obj)
            {
                if (kvp.Value is JsonValue jv && jv.TryGetValue<string>(out var strVal))
                {
                    if (strVal != null && strVal.StartsWith(EncryptedPrefix))
                    {
                        try
                        {
                            var encrypted = Convert.FromBase64String(strVal[EncryptedPrefix.Length..]);
                            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                            obj[kvp.Key] = Encoding.UTF8.GetString(decrypted);
                        }
                        catch
                        {
                            // If decryption fails (e.g., different user/machine), leave as-is
                            ColorLog.Warning("CONFIG", $"无法解密字段 {kvp.Key}，保留原始值");
                        }
                    }
                }
                else if (kvp.Value != null)
                {
                    DecryptSensitiveFields(kvp.Value);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item != null)
                    DecryptSensitiveFields(item);
            }
        }
    }

    private static string LoadEmbeddedRule()
    {
        var assembly = typeof(ConfigLoader).Assembly;
        using var stream = assembly.GetManifestResourceStream("VirgoBot.Resource.RULE.md");
        if (stream == null) return "";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
