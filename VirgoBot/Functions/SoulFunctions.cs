using System.Text.Json;
using VirgoBot.Services;

namespace VirgoBot.Functions;

public static class SoulFunctions
{
    private static string? _cachedSoulContent;
    private static DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public static void ClearCache()
    {
        _cachedSoulContent = null;
        _cacheExpiry = DateTime.MinValue;
    }

    public static IEnumerable<FunctionDefinition> Register(MemoryService memoryService)
    {
        yield return new FunctionDefinition("read_soul", "获取最近关于用户的记忆", new
        {
            type = "object"
        }, async input =>
        {
            if (_cachedSoulContent != null && DateTime.UtcNow < _cacheExpiry)
            {
                return _cachedSoulContent;
            }

            _cachedSoulContent = memoryService.GetAllSoulContent();
            _cacheExpiry = DateTime.UtcNow + CacheTtl;

            return string.IsNullOrEmpty(_cachedSoulContent) ? "暂无记忆" : _cachedSoulContent;
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
            memoryService.AddSoulEntry(content);

            _cachedSoulContent = null;
            _cacheExpiry = DateTime.MinValue;

            return "新记忆追加成功";
        });
    }
}
