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
    private readonly string _protocol; // openai / anthropic / gemini
    private readonly MemoryService _memory;
    private readonly FunctionRegistry _functions;
    private readonly string _systemMemory;
    private readonly int _maxTokens;
    private readonly TokenStatsService? _tokenStats;
    private ScheduledTaskService? _scheduledTaskService;
    private int _userMessageCount;
    private volatile bool _isSummarizing;
    private const int UserProfileSummaryInterval = 10;
    private const string UserProfileSummaryInstruction =
        "系统消息：用户画像定时总结触发\n" +
        "请根据最近的对话内容，总结用户的性格特点、说话方式、最近在做的事情，以及你对用户的评价。" +
        "用简洁的条目形式写出来，然后调用 append_soul 工具将总结保存到 Soul 中。" +
        "注意：不要重复已有的 Soul 内容，只补充新的观察。如果没有新的发现则不需要保存。";

    public LLMService(
        HttpClient http,
        string baseUrl,
        string model,
        MemoryService memory,
        FunctionRegistry functions,
        string systemMemory,
        int maxTokens = 8192,
        TokenStatsService? tokenStats = null,
        string protocol = "openai")
    {
        _http = http;
        _baseUrl = baseUrl;
        _model = model;
        _protocol = protocol.ToLowerInvariant();
        _memory = memory;
        _functions = functions;
        _systemMemory = systemMemory;
        _maxTokens = maxTokens;
        _tokenStats = tokenStats;
    }

    public void SetScheduledTaskService(ScheduledTaskService service)
    {
        _scheduledTaskService = service;
    }

    public async Task<string> AskAsync(
        string? prompt,
        Action<string>? onProgress = null,
        Func<string, Task>? onSticker = null,
        Func<string, Task>? onSwitchChat = null,
        bool isSystemTask = false,
        IReadOnlyList<ImageInput>? images = null)
    {
        if (!string.IsNullOrWhiteSpace(prompt) || (images != null && images.Count > 0))
        {
            if (isSystemTask)
            {
                _memory.SaveMessage("user", prompt ?? "");
            }
            else
            {
                var userContent = BuildUserContent(prompt, images);
                _memory.SaveMessage("user", userContent);
                _scheduledTaskService?.NotifyMessage("user");

                _userMessageCount++;
                if (_userMessageCount >= UserProfileSummaryInterval && !_isSummarizing)
                {
                    _userMessageCount = 0;
                    _ = Task.Run(async () =>
                    {
                        _isSummarizing = true;
                        try
                        {
                            ColorLog.Info("TASK", "触发用户画像总结");
                            var result = await AskAsync(UserProfileSummaryInstruction, isSystemTask: true);
                            ColorLog.Success("TASK", $"用户画像总结完成: {Truncate(result, 200)}");
                        }
                        catch (Exception ex)
                        {
                            ColorLog.Error("TASK", $"用户画像总结失败: {ex.Message}");
                        }
                        finally
                        {
                            _isSummarizing = false;
                        }
                    });
                }
            }
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

        if (!response.IsSuccessStatusCode)
        {
            // 尝试从 JSON 响应中提取错误信息，失败则直接返回 HTTP 状态码
            try
            {
                using var errDoc = JsonDocument.Parse(result);
                var error = TryGetErrorMessage(errDoc.RootElement) ?? $"HTTP {(int)response.StatusCode}";
                return $"错误: {error}";
            }
            catch
            {
                return $"错误: HTTP {(int)response.StatusCode}";
            }
        }

        using var doc = JsonDocument.Parse(result);

        if (doc.RootElement.TryGetProperty("usage", out var usage))
        {
            var promptTk = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
            var completionTk = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
            _tokenStats?.Record(promptTk, completionTk);
        }

        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            var error = TryGetErrorMessage(doc.RootElement) ?? "API 响应格式错误";
            return $"错误: {error}";
        }
        var message = choices[0].GetProperty("message");
        var assistantText = ExtractAssistantText(message);

        // Extract reasoning_content (DeepSeek thinking mode)
        string? reasoningContent = null;
        if (message.TryGetProperty("reasoning_content", out var reasoningEl))
        {
            reasoningContent = ExtractTextContent(reasoningEl);
            if (string.IsNullOrWhiteSpace(reasoningContent)) reasoningContent = null;
        }

        try
        {
            if (reasoningContent != null)
                ColorLog.Debug("API", $"思考：{reasoningContent}");
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
            _memory.SaveMessage("assistant", BuildAssistantToolCallMemory(assistantText, toolCalls, reasoningContent));

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

            if (totalSw.ElapsedMilliseconds > 120_000)
                ColorLog.Warning("TOOL", $"工具执行总耗时过长: {totalSw.ElapsedMilliseconds}ms");

            foreach (var r in results)
            {
                _memory.SaveMessage("tool", new
                {
                    tool_call_id = r.Id,
                    content = r.Result
                });
            }

            ColorLog.Info("TOOL", $"全部 {totalCount} 个工具执行完成, 总耗时 {totalSw.ElapsedMilliseconds}ms");

            return await AskAsync(null, onProgress, onSticker, onSwitchChat, images: null);
        }

        if (reasoningContent != null)
        {
            // Save as structured object to preserve reasoning_content
            _memory.SaveMessage("assistant", new { text = assistantText, reasoning_content = reasoningContent });
        }
        else
        {
            _memory.SaveMessage("assistant", assistantText);
        }
        _scheduledTaskService?.NotifyMessage("assistant");
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
                    // Check if content is a multimodal array (has image_url or base64 parts)
                    if (content.ValueKind == JsonValueKind.Array && IsMultimodalContent(content))
                    {
                        var parts = BuildMultimodalParts(content);
                        if (parts.Count > 0)
                            messages.Add(new { role = "user", content = parts });
                    }
                    else
                    {
                        var text = ExtractTextContent(content);
                        if (!string.IsNullOrWhiteSpace(text))
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
                        // Check for structured assistant message with reasoning_content
                        string? reasoning = null;
                        string text;
                        if (content.ValueKind == JsonValueKind.Object &&
                            content.TryGetProperty("reasoning_content", out var rcEl))
                        {
                            reasoning = ExtractTextContent(rcEl);
                            text = content.TryGetProperty("text", out var tEl)
                                ? ExtractTextContent(tEl) : string.Empty;
                        }
                        else
                        {
                            text = ExtractTextContent(content);
                        }

                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            if (!string.IsNullOrWhiteSpace(reasoning))
                            {
                                messages.Add(new
                                {
                                    role = "assistant",
                                    content = text,
                                    reasoning_content = reasoning
                                });
                            }
                            else
                            {
                                messages.Add(new { role = "assistant", content = text });
                            }
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
        string? reasoningContent = null;

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
            else if (type == "reasoning" && item.TryGetProperty("content", out var rcValue))
            {
                reasoningContent = rcValue.GetString();
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

        if (!string.IsNullOrWhiteSpace(reasoningContent))
        {
            message = new
            {
                role = "assistant",
                content = textParts.Count == 0 ? null : string.Join("\n", textParts),
                reasoning_content = reasoningContent,
                tool_calls = toolCalls
            };
        }
        else
        {
            message = new
            {
                role = "assistant",
                content = textParts.Count == 0 ? null : string.Join("\n", textParts),
                tool_calls = toolCalls
            };
        }

        return true;
    }

    private static object BuildAssistantToolCallMemory(string assistantText, JsonElement toolCalls, string? reasoningContent)
    {
        var content = new List<object>();

        if (!string.IsNullOrWhiteSpace(reasoningContent))
        {
            content.Add(new
            {
                type = "reasoning",
                content = reasoningContent
            });
        }

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

    // ── Multimodal helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Builds the user content to save in memory.
    /// For text-only messages: saves as plain string with timestamp.
    /// For multimodal messages: saves as JSON array of content parts.
    /// </summary>
    private object BuildUserContent(string? prompt, IReadOnlyList<ImageInput>? images)
    {
        var textPart = string.IsNullOrWhiteSpace(prompt)
            ? $"参数：北京时间 {DateTime.Now:yyyy-MM-dd HH:mm}"
            : $"{prompt}\n\n参数：北京时间 {DateTime.Now:yyyy-MM-dd HH:mm}";

        if (images == null || images.Count == 0)
            return textPart;

        // Build multimodal array: [{ type:"text", text:"..." }, { type:"image_url", ... }, ...]
        var parts = new List<object> { new { type = "text", text = textPart } };
        foreach (var img in images)
        {
            if (img.IsUrl)
                parts.Add(new { type = "image_url", image_url = new { url = img.Data } });
            else
                parts.Add(new { type = "image_base64", media_type = img.MediaType, data = img.Data });
        }
        return parts.ToArray();
    }

    private static bool IsMultimodalContent(JsonElement content)
    {
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("type", out var t))
            {
                var typeStr = t.GetString();
                if (typeStr == "image_url" || typeStr == "image_base64")
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Converts stored multimodal content parts into the format expected by the current provider protocol.
    /// OpenAI: { type:"image_url", image_url:{ url:"..." } } or data URL for base64
    /// Anthropic: { type:"image", source:{ type:"url"|"base64", ... } }
    /// Gemini (OpenAI-compat): same as OpenAI
    /// </summary>
    private List<object> BuildMultimodalParts(JsonElement content)
    {
        var parts = new List<object>();
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (!item.TryGetProperty("type", out var typeEl)) continue;
            var type = typeEl.GetString();

            if (type == "text")
            {
                var text = item.TryGetProperty("text", out var tEl) ? tEl.GetString() ?? "" : "";
                if (_protocol == "anthropic")
                    parts.Add(new { type = "text", text });
                else
                    parts.Add(new { type = "text", text });
            }
            else if (type == "image_url")
            {
                var url = item.TryGetProperty("image_url", out var iuEl) && iuEl.TryGetProperty("url", out var uEl)
                    ? uEl.GetString() ?? ""
                    : "";
                if (string.IsNullOrEmpty(url)) continue;

                if (_protocol == "anthropic")
                    parts.Add(new { type = "image", source = new { type = "url", url } });
                else
                    parts.Add(new { type = "image_url", image_url = new { url } });
            }
            else if (type == "image_base64")
            {
                var mediaType = item.TryGetProperty("media_type", out var mtEl) ? mtEl.GetString() ?? "image/jpeg" : "image/jpeg";
                var data = item.TryGetProperty("data", out var dEl) ? dEl.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(data)) continue;

                if (_protocol == "anthropic")
                {
                    parts.Add(new
                    {
                        type = "image",
                        source = new { type = "base64", media_type = mediaType, data }
                    });
                }
                else
                {
                    // OpenAI / Gemini: data URL
                    parts.Add(new { type = "image_url", image_url = new { url = $"data:{mediaType};base64,{data}" } });
                }
            }
        }
        return parts;
    }
}

/// <summary>Represents an image to send alongside a user message.</summary>
public class ImageInput
{
    /// <summary>True = Data is a URL; False = Data is base64-encoded image bytes.</summary>
    public bool IsUrl { get; init; }
    public string Data { get; init; } = "";
    public string MediaType { get; init; } = "image/jpeg"; // only used when IsUrl=false

    public static ImageInput FromUrl(string url) => new() { IsUrl = true, Data = url };
    public static ImageInput FromBase64(string base64, string mediaType = "image/jpeg")
        => new() { IsUrl = false, Data = base64, MediaType = mediaType };
}
