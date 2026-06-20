using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VirgoBot.Configuration;

/// <summary>
/// 实验性功能配置加载器
/// </summary>
public static class ExperimentalConfigLoader
{
    private const string EncryptedPrefix = "DPAPI:";
    private static readonly string ConfigPath = Path.Combine(
        AppConstants.ConfigDirectory, "experimental.json");

    /// <summary>
    /// 加载实验性功能配置
    /// </summary>
    public static ExperimentalConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                var defaultConfig = new ExperimentalConfig { Voice = new VoiceConfig() };
                Save(defaultConfig);
                return defaultConfig;
            }

            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<ExperimentalConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var result = config ?? new ExperimentalConfig { Voice = new VoiceConfig() };
            DecryptVoiceConfig(result);
            MigrateVoiceConfigIfNeeded(result);
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载实验性配置失败: {ex.Message}");
            return new ExperimentalConfig { Voice = new VoiceConfig() };
        }
    }

    /// <summary>
    /// 保存实验性功能配置
    /// </summary>
    public static void Save(ExperimentalConfig config)
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var toSave = CloneConfig(config);
            EncryptVoiceConfig(toSave);

            var json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存实验性配置失败: {ex.Message}");
        }
    }

    private static void DecryptVoiceConfig(ExperimentalConfig config)
    {
        if (config.Voice?.ApiKey != null && config.Voice.ApiKey.StartsWith(EncryptedPrefix))
        {
            try
            {
                var encrypted = Convert.FromBase64String(config.Voice.ApiKey[EncryptedPrefix.Length..]);
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                config.Voice.ApiKey = Encoding.UTF8.GetString(decrypted);
            }
            catch { /* keep as-is */ }
        }
    }

    private static void EncryptVoiceConfig(ExperimentalConfig config)
    {
        if (config.Voice?.ApiKey != null &&
            !config.Voice.ApiKey.StartsWith(EncryptedPrefix) &&
            !string.IsNullOrEmpty(config.Voice.ApiKey))
        {
            var plainBytes = Encoding.UTF8.GetBytes(config.Voice.ApiKey);
            var encrypted = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            config.Voice.ApiKey = EncryptedPrefix + Convert.ToBase64String(encrypted);
        }
    }

    private static void MigrateVoiceConfigIfNeeded(ExperimentalConfig config)
    {
        if (config.Voice?.ApiKey != null &&
            !config.Voice.ApiKey.StartsWith(EncryptedPrefix) &&
            !string.IsNullOrEmpty(config.Voice.ApiKey))
        {
            // Plaintext API key found — save encrypted version
            Save(config);
            Console.WriteLine("实验性配置: 语音 API Key 已加密");
        }
    }

    private static ExperimentalConfig CloneConfig(ExperimentalConfig source)
    {
        return new ExperimentalConfig
        {
            Voice = source.Voice == null ? new VoiceConfig() : new VoiceConfig
            {
                ApiKey = source.Voice.ApiKey ?? "",
                AsrResourceId = source.Voice.AsrResourceId ?? "",
                TtsResourceId = source.Voice.TtsResourceId ?? "",
                VoiceType = source.Voice.VoiceType ?? ""
            }
        };
    }
}
