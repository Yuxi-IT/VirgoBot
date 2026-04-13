using System.Text.Json;
using VirgoBot.Configuration;

namespace VirgoBot.Functions;

public static class SoulFunctions
{
    private static string? _cachedSoulContent;
    private static DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public static IEnumerable<FunctionDefinition> Register(Config config)
    {
        yield return new FunctionDefinition("read_soul", "获取最近关于用户的记忆", new
        {
            type = "object"
        }, async input =>
        {
            var soulPath = Path.Combine(AppConstants.ConfigDirectory, config.SoulFile);
            if (!File.Exists(soulPath)) return "记忆文件不存在";

            if (_cachedSoulContent != null && DateTime.UtcNow < _cacheExpiry)
            {
                return _cachedSoulContent;
            }

            _cachedSoulContent = File.ReadAllText(soulPath);
            _cacheExpiry = DateTime.UtcNow + CacheTtl;
            return _cachedSoulContent;
        });

        yield return new FunctionDefinition("append_soul", "追加可更改的记忆[例如用户最近的事情(必须标注日期)]", new
        {
            type = "object",
            properties = new
            {
                content = new { type = "string", description = "要新追加的关于用户记忆的文本内容" },
            },
            required = new[] { "content" }
        }, async input =>
        {
            var content = input.GetProperty("content").GetString() ?? "";
            var soulPath = Path.Combine(AppConstants.ConfigDirectory, config.SoulFile);
            File.AppendAllText(soulPath, Environment.NewLine + content);

            // Invalidate cache after write
            _cachedSoulContent = null;
            _cacheExpiry = DateTime.MinValue;

            return "新记忆追加成功";
        });
    }
}
