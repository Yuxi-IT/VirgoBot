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
        yield return new FunctionDefinition("read_soul", "Retrieve recent user memories", new
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

            return string.IsNullOrEmpty(_cachedSoulContent) ? "No memories yet" : _cachedSoulContent;
        });

        yield return new FunctionDefinition("append_soul", "Append modifiable memories (e.g. user's recent activities, must include date)", new
        {
            type = "object",
            properties = new
            {
                content = new { type = "string", description = "New memory content about the user to append" },
            },
            required = new[] { "content" }
        }, async input =>
        {
            var content = input.GetProperty("content").GetString() ?? "";
            memoryService.AddSoulEntry(content);

            _cachedSoulContent = null;
            _cacheExpiry = DateTime.MinValue;

            return "New memory saved successfully";
        });
    }
}
