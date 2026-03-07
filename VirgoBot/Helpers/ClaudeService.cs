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

    public async Task<string> AskAsync(long userId, string? prompt, Action<string>? onProgress = null, Func<string, Task>? onSticker = null)
    {
        if (!string.IsNullOrEmpty(prompt))
        {
            var userMsg = new
            {
                role = "user",
                content = new[] { new { type = "text", text = $"{prompt}\n\n参数：北京时间-{DateTime.Now:yyyy-MM-dd HH:mm}" } }
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

        var options = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(body, options);

        var debugInfo = new
        {
            model = _model,
            max_tokens = 8192,
            system_length = _systemMemory.Length,
            tools_count = _functions.GetToolSchemas().Length,
            messages_count = messages.Count,
            messages
        };
        //ColorLog.Debug("API", JsonSerializer.Serialize(debugInfo, new JsonSerializerOptions { WriteIndented = true }));

        // 输出工具到文件
        //File.WriteAllText("debug_tools.json", JsonSerializer.Serialize(_functions.GetToolSchemas(), new JsonSerializerOptions { WriteIndented = true }));

        var resp = await _http.PostAsync(_baseUrl, new StringContent(json, Encoding.UTF8, "application/json"));
        var result = await resp.Content.ReadAsStringAsync();

        ColorLog.Debug("API", $"响应: {result.Substring(0, Math.Min(500, result.Length))}");

        using var doc = JsonDocument.Parse(result);

        //Console.WriteLine($"[DEBUG] API Response: {result.Substring(0, Math.Min(500, result.Length))}");

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

            if (type == "tool_use")
            {
                var toolName = block.GetProperty("name").GetString();
                var toolId = block.GetProperty("id").GetString();
                var toolInput = block.GetProperty("input");

                var assistantContent = new List<object>();
                var textBeforeTool = "";

                foreach (var b in content.EnumerateArray())
                {
                    var t = b.GetProperty("type").GetString();
                    if (t == "text")
                    {
                        textBeforeTool = b.GetProperty("text").GetString() ?? "";
                        assistantContent.Add(new
                        {
                            type = "text",
                            text = textBeforeTool
                        });
                    }
                    else if (t == "tool_use")
                    {
                        assistantContent.Add(new
                        {
                            type = "tool_use",
                            id = b.GetProperty("id").GetString(),
                            name = b.GetProperty("name").GetString(),
                            input = JsonSerializer.Deserialize<object>(b.GetProperty("input").GetRawText())
                        });
                    }
                }

                _memory.SaveMessage(userId, "assistant", assistantContent.ToArray());

                if (!string.IsNullOrEmpty(textBeforeTool))
                {
                    onProgress?.Invoke(textBeforeTool);
                }

                string toolResult;
                try
                {
                    toolResult = await _functions.ExecuteAsync(toolName, toolInput);
                    if (toolName == "send_sticker" && toolResult != "no_match" && onSticker != null)
                    {
                        await onSticker(toolResult);
                    }
                }
                catch (Exception ex)
                {
                    toolResult = $"工具执行失败: {ex.Message}";
                }

                _memory.SaveMessage(userId, "user", new[]
                {
                    new {
                        type = "tool_result",
                        tool_use_id = toolId,
                        content = toolResult
                    }
                });

                return await AskAsync(userId, null, onProgress, onSticker);
            }
        }

        var textContent = "";
        foreach (var block in content.EnumerateArray())
        {
            if (block.GetProperty("type").GetString() == "text")
            {
                textContent = block.GetProperty("text").GetString() ?? "";
                break;
            }
        }

        _memory.SaveMessage(userId, "assistant", new[] { new { type = "text", text = textContent } });
        _memory.ClearOldMessages(userId);
        return textContent;
    }
}
