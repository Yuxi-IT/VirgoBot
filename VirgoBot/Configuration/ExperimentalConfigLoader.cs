using System.Text.Json;

namespace VirgoBot.Configuration;

/// <summary>
/// 实验性功能配置加载器
/// </summary>
public static class ExperimentalConfigLoader
{
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
                var defaultConfig = new ExperimentalConfig
                {
                    Voice = new VoiceConfig()
                };
                Save(defaultConfig);
                return defaultConfig;
            }

            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<ExperimentalConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return config ?? new ExperimentalConfig { Voice = new VoiceConfig() };
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

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
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
}
