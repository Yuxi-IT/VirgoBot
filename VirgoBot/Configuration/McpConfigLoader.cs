using System.Text.Json;
using VirgoBot.Utilities;

namespace VirgoBot.Configuration;

public class McpServerConfig
{
    public string Name { get; set; } = "";
    public string Transport { get; set; } = "stdio"; // "stdio" | "sse"
    public string Command { get; set; } = "";
    public string[] Args { get; set; } = Array.Empty<string>();
    public Dictionary<string, string> Env { get; set; } = new();
    public string Url { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public static class McpConfigLoader
{
    private const string McpConfigFileName = "mcp.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static List<McpServerConfig> Load()
    {
        var configDir = AppConstants.ConfigDirectory;
        var configPath = Path.Combine(configDir, McpConfigFileName);

        Directory.CreateDirectory(configDir);

        if (!File.Exists(configPath))
        {
            return new List<McpServerConfig>();
        }

        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<List<McpServerConfig>>(json, JsonOptions)
                   ?? new List<McpServerConfig>();
        }
        catch (Exception ex)
        {
            ColorLog.Error("MCP", $"加载 MCP 配置失败: {ex.Message}");
            return new List<McpServerConfig>();
        }
    }

    public static void Save(List<McpServerConfig> configs)
    {
        var configDir = AppConstants.ConfigDirectory;
        var configPath = Path.Combine(configDir, McpConfigFileName);

        Directory.CreateDirectory(configDir);

        var json = JsonSerializer.Serialize(configs, JsonOptions);
        File.WriteAllText(configPath, json);
        ColorLog.Success("MCP", "MCP 配置已保存");
    }
}
