using System.Text.Json;
using VirgoBot.Services;

namespace VirgoBot.Functions;

public static class SoulFunctions
{
    public static void ClearCache()
    {
        // Cache cleared; read_soul no longer uses caching (returns top-N by weight)
    }

    public static IEnumerable<FunctionDefinition> Register(MemoryService memoryService)
    {
        yield return new FunctionDefinition("read_soul", "Retrieve top important user memories (sorted by weight, most important first)", new
        {
            type = "object",
            properties = new
            {
                limit = new { type = "integer", description = "Max number of memories to retrieve (default 10)" }
            }
        }, async input =>
        {
            var limit = input.TryGetProperty("limit", out var l) && l.TryGetInt32(out var v) ? v : 10;
            var entries = memoryService.GetTopSoulByWeight(limit);
            if (entries.Count == 0) return "No memories yet";

            var sb = new System.Text.StringBuilder();
            foreach (var e in entries)
            {
                memoryService.IncrementSoulAccess(e.Id);
                sb.AppendLine($"#{e.Id} [w:{e.Weight:F2}] {e.Content}");
            }
            return sb.ToString();
        });

        yield return new FunctionDefinition("search_soul", "Search memories by keyword and/or tag filter", new
        {
            type = "object",
            properties = new
            {
                keyword = new { type = "string", description = "Keyword to search in memory content" },
                tag = new { type = "string", description = "Tag to filter by (e.g. 'emotion:happy')" },
                limit = new { type = "integer", description = "Max results (default 20)" }
            }
        }, async input =>
        {
            var keyword = input.TryGetProperty("keyword", out var kw) ? kw.GetString() : null;
            var tag = input.TryGetProperty("tag", out var t) ? t.GetString() : null;
            var limit = input.TryGetProperty("limit", out var l) && l.TryGetInt32(out var v2) ? v2 : 20;
            var entries = memoryService.SearchSoul(keyword, tag, limit);
            if (entries.Count == 0) return "No matching memories found";

            var sb = new System.Text.StringBuilder();
            foreach (var e in entries)
            {
                memoryService.IncrementSoulAccess(e.Id);
                var tagsStr = string.IsNullOrWhiteSpace(e.Tags) ? "" : $" [{e.Tags}]";
                sb.AppendLine($"#{e.Id}{tagsStr} [w:{e.Weight:F2}] {e.Content}");
            }
            return sb.ToString();
        });

        yield return new FunctionDefinition("append_soul", "Append modifiable memories (e.g. user's recent activities, must include date)", new
        {
            type = "object",
            properties = new
            {
                content = new { type = "string", description = "New memory content about the user to append" },
                tags = new { type = "string", description = "Comma-separated tags (e.g. 'emotion:happy,scene:work')" },
                weight = new { type = "number", description = "Importance weight 0.0-1.0 (default 1.0)" },
                message_context_id = new { type = "integer", description = "Optional message ID to link this memory to" }
            },
            required = new[] { "content" }
        }, async input =>
        {
            var content = input.GetProperty("content").GetString() ?? "";
            var tags = input.TryGetProperty("tags", out var tagsEl) ? tagsEl.GetString() : null;
            var weight = input.TryGetProperty("weight", out var wEl) && wEl.TryGetDouble(out var w) ? w : 1.0;
            memoryService.AddSoulEntry(content, tags, weight, "llm");

            if (input.TryGetProperty("message_context_id", out var msgIdEl) && msgIdEl.TryGetInt32(out var msgId))
            {
                var entries = memoryService.GetAllSoulEntries();
                var last = entries.LastOrDefault();
                if (last != null)
                    memoryService.LinkSoulToMessage(last.Id, msgId);
            }

            return "New memory saved successfully";
        });

        yield return new FunctionDefinition("rollback_soul", "Restore a memory entry to a previous version", new
        {
            type = "object",
            properties = new
            {
                id = new { type = "integer", description = "ID of the soul memory entry to rollback" },
                version = new { type = "integer", description = "Target version number to restore" }
            },
            required = new[] { "id", "version" }
        }, async input =>
        {
            var id = input.GetProperty("id").GetInt32();
            var version = input.GetProperty("version").GetInt32();
            var success = memoryService.RollbackSoulEntry(id, version);
            return success ? $"Memory #{id} rolled back to version {version}" : $"Version {version} not found for memory #{id}";
        });

        yield return new FunctionDefinition("forget_soul", "Manually archive (soft-delete) a memory entry. Low-weight memories are also auto-archived by decay.", new
        {
            type = "object",
            properties = new
            {
                id = new { type = "integer", description = "ID of the memory entry to forget" }
            },
            required = new[] { "id" }
        }, async input =>
        {
            var id = input.GetProperty("id").GetInt32();
            var success = memoryService.ForgetSoulEntry(id);
            return success ? $"Memory #{id} has been archived" : $"Memory #{id} not found";
        });
    }
}
