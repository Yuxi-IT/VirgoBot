using System.Diagnostics;
using System.Text;
using System.Text.Json;
using VirgoBot.Functions;
using VirgoBot.Utilities;

namespace VirgoBot.Services;

public class LLMService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly MemoryService _memory;
    private readonly FunctionRegistry _functions;
    private readonly string _systemMemory;
    private readonly int _maxTokens;
    private readonly TokenStatsService? _tokenStats;

    public LLMService(
        HttpClient http,
        string baseUrl,
        string model,
        MemoryService memory,
        FunctionRegistry functions,
        string systemMemory,
        int maxTokens = 8192,
        TokenStatsService? tokenStats = null)
    {
        _http = http;
        _baseUrl = baseUrl;
        _model = model;
        _memory = memory;
        _functions = functions;
        _systemMemory = systemMemory;
        _maxTokens = maxTokens;
        _tokenStats = tokenStats;
    }

    public async Task<string> AskAsync(
        string? prompt,
        Action<string>? onProgress = null,
        Func<string, Task>? onSticker = null,
        Func<string, Task>? onSwitchChat = null)
    {
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            _memory.SaveMessage("user", $"{prompt}\n\n参数：北京时间 {DateTime.Now:yyyy-MM-dd HH:mm}");
        }

        var memoryMessages = _memory.LoadMessages();
        var messages = BuildOpenAiMessages(memoryMessages);

        ColorLog.Info("LLM", $"Messages loaded: {messages.Count}");

        var body = new
        {
            model = _model,
            max_tokens = _maxTokens,
            messages,
            tools = BuildOpenAiTools(),
            tool_choice = "auto"
        };

        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        });

        using var response = await _http.PostAsync(
            NormalizeChatCompletionsUrl(_baseUrl),
            new StringContent(json, Encoding.UTF8, "application/json"));

        var result = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(result);

        if (doc.RootElement.TryGetProperty("usage", out var usage))
        {
            var promptTk = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
            var completionTk = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
            _tokenStats?.Record(promptTk, completionTk);
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = TryGetErrorMessage(doc.RootElement) ?? $"HTTP {(int)response.StatusCode}";
            return $"错误: {error}";
        }

        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            var error = TryGetErrorMessage(doc.RootElement) ?? "API 响应格式错误";
            return $"错误: {error}";
        }
        var message = choices[0].GetProperty("message");
        var assistantText = ExtractAssistantText(message);

        try
        {
            ColorLog.Debug("API", $"思考：{ExtractTextContent(message.GetProperty("reasoning_content"))}");
            ColorLog.Debug("API", $"输出：{assistantText}");
            ColorLog.Debug("API", $"模型：{ExtractTextContent(doc.RootElement.GetProperty("model"))}");
        }
        catch(Exception ex)
        {
            ColorLog.Error("API", $"{ex.Message}\n{result}");
        }

        if (!string.IsNullOrWhiteSpace(assistantText))
        {
            onProgress?.Invoke(assistantText);
        }

        if (message.TryGetProperty("tool_calls", out var toolCalls) &&
            toolCalls.ValueKind == JsonValueKind.Array &&
            toolCalls.GetArrayLength() > 0)
        {
            _memory.SaveMessage("assistant", BuildAssistantToolCallMemory(assistantText, toolCalls));

            var toolCallItems = toolCalls.EnumerateArray().Select(tc =>
            {
                var functionCall = tc.GetProperty("function");
                return new
                {
                    Name = functionCall.GetProperty("name").GetString() ?? string.Empty,
                    Id = tc.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N"),
                    ArgumentsJson = functionCall.TryGetProperty("arguments", out var argsEl)
                        ? argsEl.GetString() ?? "{}"
                        : "{}"
                };
            }).ToList();

            var totalCount = toolCallItems.Count;
            ColorLog.Info("TOOL", $"开始并行执行 {totalCount} 个工具调用...");
            var totalSw = Stopwatch.StartNew();

            var tasks = toolCallItems.Select(async item =>
            {
                var sw = Stopwatch.StartNew();
                string toolResult;
                try
                {
                    using var argsDoc = JsonDocument.Parse(
                        string.IsNullOrWhiteSpace(item.ArgumentsJson) ? "{}" : item.ArgumentsJson);
                    toolResult = await _functions.ExecuteAsync(item.Name, argsDoc.RootElement);

                    if (item.Name == "send_sticker" && toolResult != "no_match" && onSticker is not null)
                    {
                        await onSticker(toolResult);
                    }

                    if (item.Name == "switch_douyin_chat" && toolResult.StartsWith("switch_chat:") && onSwitchChat is not null)
                    {
                        await onSwitchChat(toolResult.Replace("switch_chat:", "").Trim());
                    }
                }
                catch (Exception ex)
                {
                    toolResult = $"工具执行失败: {ex.Message}";
                }
                sw.Stop();

                ColorLog.Success("TOOL", $"[{item.Name}] {sw.ElapsedMilliseconds}ms → {Truncate(toolResult, 200)}");

                return new { item.Name, item.Id, Result = toolResult };
            }).ToList();

            var results = await Task.WhenAll(tasks);
            totalSw.Stop();

            foreach (var r in results)
            {
                _memory.SaveMessage("tool", new
                {
                    tool_call_id = r.Id,
                    content = r.Result
                });
            }

            ColorLog.Info("TOOL", $"全部 {totalCount} 个工具执行完成, 总耗时 {totalSw.ElapsedMilliseconds}ms");

            return await AskAsync(null, onProgress, onSticker, onSwitchChat);
        }

        _memory.SaveMessage("assistant", assistantText);
        _memory.ClearOldMessages();
        return assistantText;
    }

    private List<object> BuildOpenAiMessages(List<object> memoryMessages)
    {
        var messages = new List<object>
        {
            new
            {
                role = "system",
                content = _systemMemory
            }
        };

        foreach (var entry in memoryMessages)
        {
            var entryJson = JsonSerializer.Serialize(entry);
            using var doc = JsonDocument.Parse(entryJson);
            var root = doc.RootElement;

            var role = root.GetProperty("role").GetString() ?? "user";
            var content = root.GetProperty("content");

            switch (role)
            {
                case "user":
                {
                    var text = ExtractTextContent(content);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        messages.Add(new { role = "user", content = text });
                    }
                    break;
                }
                case "assistant":
                {
                    if (TryBuildAssistantToolCallMessage(content, out var assistantToolMessage))
                    {
                        messages.Add(assistantToolMessage!);
                    }
                    else
                    {
                        var text = ExtractTextContent(content);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            messages.Add(new { role = "assistant", content = text });
                        }
                    }
                    break;
                }
                case "tool":
                {
                    if (content.ValueKind == JsonValueKind.Object &&
                        content.TryGetProperty("tool_call_id", out var toolCallIdEl) &&
                        content.TryGetProperty("content", out var toolContentEl))
                    {
                        messages.Add(new
                        {
                            role = "tool",
                            tool_call_id = toolCallIdEl.GetString(),
                            content = ExtractTextContent(toolContentEl)
                        });
                    }
                    break;
                }
            }
        }

        return messages;
    }

    private object[] BuildOpenAiTools()
    {
        return _functions
            .GetToolSchemas()
            .Select(schema =>
            {
                var json = JsonSerializer.Serialize(schema);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var name = root.GetProperty("name").GetString() ?? string.Empty;
                var description = root.GetProperty("description").GetString() ?? string.Empty;
                var parameters = root.GetProperty("input_schema").Deserialize<object>();

                return (object)new
                {
                    type = "function",
                    function = new
                    {
                        name,
                        description,
                        parameters
                    }
                };
            })
            .ToArray();
    }

    private static string NormalizeChatCompletionsUrl(string url)
    {
        var trimmed = url.TrimEnd('/');
        if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            return $"{trimmed}/chat/completions";
        }

        return $"{trimmed}/v1/chat/completions";
    }

    private static string? TryGetErrorMessage(JsonElement root)
    {
        if (!root.TryGetProperty("error", out var error))
        {
            return null;
        }

        if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message))
        {
            return message.GetString();
        }

        return error.ToString();
    }

    private static string ExtractAssistantText(JsonElement message)
    {
        if (!message.TryGetProperty("content", out var content))
        {
            return string.Empty;
        }

        return ExtractTextContent(content);
    }

    private static string ExtractTextContent(JsonElement content)
    {
        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString() ?? string.Empty,
            JsonValueKind.Array => string.Join(
                "\n",
                content.EnumerateArray()
                    .Select(item =>
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            return item.GetString() ?? string.Empty;
                        }

                        if (item.ValueKind == JsonValueKind.Object &&
                            item.TryGetProperty("type", out var typeEl) &&
                            typeEl.GetString() == "text" &&
                            item.TryGetProperty("text", out var textEl))
                        {
                            return textEl.GetString() ?? string.Empty;
                        }

                        if (item.ValueKind == JsonValueKind.Object &&
                            item.TryGetProperty("content", out var contentEl))
                        {
                            return ExtractTextContent(contentEl);
                        }

                        return string.Empty;
                    })
                    .Where(text => !string.IsNullOrWhiteSpace(text))),
            JsonValueKind.Object => content.TryGetProperty("text", out var textEl)
                ? textEl.GetString() ?? string.Empty
                : content.TryGetProperty("content", out var contentEl)
                    ? ExtractTextContent(contentEl)
                    : content.ToString(),
            _ => content.ToString()
        };
    }

    private static bool TryBuildAssistantToolCallMessage(JsonElement content, out object? message)
    {
        message = null;

        if (content.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var textParts = new List<string>();
        var toolCalls = new List<object>();

        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("type", out var typeEl))
            {
                continue;
            }

            var type = typeEl.GetString();
            if (type == "text" && item.TryGetProperty("text", out var textValue))
            {
                var text = textValue.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    textParts.Add(text);
                }
            }
            else if (type == "tool_use")
            {
                var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : Guid.NewGuid().ToString("N");
                var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : string.Empty;
                var input = item.TryGetProperty("input", out var inputEl)
                    ? JsonSerializer.Deserialize<object>(inputEl.GetRawText())
                    : new { };

                toolCalls.Add(new
                {
                    id,
                    type = "function",
                    function = new
                    {
                        name,
                        arguments = JsonSerializer.Serialize(input)
                    }
                });
            }
        }

        if (toolCalls.Count == 0)
        {
            return false;
        }

        message = new
        {
            role = "assistant",
            content = textParts.Count == 0 ? null : string.Join("\n", textParts),
            tool_calls = toolCalls
        };

        return true;
    }

    private static object BuildAssistantToolCallMemory(string assistantText, JsonElement toolCalls)
    {
        var content = new List<object>();

        if (!string.IsNullOrWhiteSpace(assistantText))
        {
            content.Add(new
            {
                type = "text",
                text = assistantText
            });
        }

        foreach (var toolCall in toolCalls.EnumerateArray())
        {
            var function = toolCall.GetProperty("function");
            var arguments = function.TryGetProperty("arguments", out var argsEl)
                ? argsEl.GetString() ?? "{}"
                : "{}";

            object input;
            try
            {
                input = JsonSerializer.Deserialize<object>(arguments) ?? new { };
            }
            catch (Exception ex)
            {
                ColorLog.Error("LLM", $"参数解析失败: {ex.Message}, arguments={arguments}");
                input = new { };
            }

            content.Add(new
            {
                type = "tool_use",
                id = toolCall.GetProperty("id").GetString(),
                name = function.GetProperty("name").GetString(),
                input
            });
        }

        return content.ToArray();
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "(empty)";
        var singleLine = text.ReplaceLineEndings(" ");
        return singleLine.Length <= maxLength ? singleLine : singleLine[..maxLength] + "...";
    }
}
