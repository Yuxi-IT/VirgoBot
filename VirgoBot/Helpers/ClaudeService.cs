using System.Text;
using System.Text.Json;

namespace VirgoBot.Helpers;

public class ClaudeService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly MemoryService _memory;
    private readonly FunctionRegistry _functions;
    private readonly string _systemMemory;

    public ClaudeService(
        HttpClient http,
        string baseUrl,
        string model,
        MemoryService memory,
        FunctionRegistry functions,
        string systemMemory)
    {
        _http = http;
        _baseUrl = baseUrl;
        _model = model;
        _memory = memory;
        _functions = functions;
        _systemMemory = systemMemory;
    }

    public async Task<string> AskAsync(long userId, string? prompt)
    {
        if (!string.IsNullOrEmpty(prompt))
        {
            var userMsg = new
            {
                role = "user",
                content = new[] { new { type = "text", text = prompt } }
            };
            _memory.SaveMessage(userId, "user", userMsg.content);
        }

        var messages = _memory.LoadMessages(userId);
        Console.WriteLine($"[INFO] 本回合调用前{messages.Count}条聊天记录");

        var body = new
        {
            model = _model,
            max_tokens = 8192,
            system = _systemMemory,
            tools = _functions.GetToolSchemas(),
            messages
        };

        var json = JsonSerializer.Serialize(body);
        var resp = await _http.PostAsync(_baseUrl, new StringContent(json, Encoding.UTF8, "application/json"));
        var result = await resp.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(result);

        if (!doc.RootElement.TryGetProperty("content", out var content))
        {
            var error = doc.RootElement.TryGetProperty("error", out var err)
                ? err.GetProperty("message").GetString()
                : "API 响应格式错误";
            return $"错误: {error}";
        }

        foreach (var block in content.EnumerateArray())
        {
            var type = block.GetProperty("type").GetString();

            if (type == "text")
            {
                var text = block.GetProperty("text").GetString() ?? "";
                _memory.SaveMessage(userId, "assistant", new[] { new { type = "text", text } });
                _memory.ClearOldMessages(userId);
                return text;
            }

            if (type == "tool_use")
            {
                var toolName = block.GetProperty("name").GetString();
                var toolId = block.GetProperty("id").GetString();
                var toolInput = block.GetProperty("input");

                _memory.SaveMessage(userId, "assistant", new[]
                {
                    new {
                        type = "tool_use",
                        id = toolId,
                        name = toolName,
                        input = JsonSerializer.Deserialize<object>(toolInput.GetRawText())
                    }
                });

                var toolResult = _functions.Execute(toolName, toolInput);

                _memory.SaveMessage(userId, "user", new[]
                {
                    new {
                        type = "tool_result",
                        tool_use_id = toolId,
                        content = toolResult
                    }
                });

                return await AskAsync(userId, null);
            }
        }

        return "无响应";
    }
}
