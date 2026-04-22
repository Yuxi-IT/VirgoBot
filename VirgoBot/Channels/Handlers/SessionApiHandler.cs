using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VirgoBot.Configuration;
using VirgoBot.Services;
using VirgoBot.Utilities;
using static VirgoBot.Channels.Handlers.HttpResponseHelper;

namespace VirgoBot.Channels.Handlers;

public class SessionApiHandler
{
    private readonly Gateway _gateway;
    private readonly MemoryService _memoryService;

    public SessionApiHandler(Gateway gateway, MemoryService memoryService)
    {
        _gateway = gateway;
        _memoryService = memoryService;
    }

    public async Task HandleGetSessionsRequest(HttpListenerContext ctx)
    {
        var sessions = _memoryService.GetAllSessions();
        var data = sessions.Select(s => new
        {
            fileName = s.FileName,
            sessionName = s.SessionName,
            messageCount = s.MessageCount,
            soulCount = s.SoulCount,
            lastModified = s.LastModified.ToString("o"),
            size = s.Size,
            isCurrent = s.IsCurrent
        });

        await SendJsonResponse(ctx, new { success = true, data });
    }

    public async Task HandleCreateSessionRequest(HttpListenerContext ctx)
    {
        var newDbName = _memoryService.CreateSession();
        ColorLog.Success("SESSION", $"新会话已创建: {newDbName}");
        await SendJsonResponse(ctx, new { success = true, data = new { fileName = newDbName } });
    }

    public async Task HandleSwitchSessionRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<SessionSwitchRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Session))
        {
            await SendErrorResponse(ctx, 400, "Session name is required");
            return;
        }

        var dbPath = Path.Combine("memorys", body.Session);
        if (!File.Exists(dbPath))
        {
            await SendErrorResponse(ctx, 404, "Session not found");
            return;
        }

        try
        {
            await _gateway.SwitchSession(body.Session);
            await SendJsonResponse(ctx, new { success = true, message = "Session switched", data = new { currentSession = body.Session } });
        }
        catch (Exception ex)
        {
            await SendErrorResponse(ctx, 500, $"Failed to switch session: {ex.Message}");
        }
    }

    public async Task HandleDeleteSessionRequest(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath;
        var name = Uri.UnescapeDataString(path.Replace("/api/sessions/", ""));

        if (string.IsNullOrWhiteSpace(name))
        {
            await SendErrorResponse(ctx, 400, "Session name is required");
            return;
        }

        if (name == _memoryService.CurrentDbName)
        {
            await SendErrorResponse(ctx, 400, "Cannot delete the currently active session");
            return;
        }

        try
        {
            _memoryService.DeleteSession(name);
            ColorLog.Success("SESSION", $"会话已删除: {name}");
            await SendJsonResponse(ctx, new { success = true, message = "Session deleted" });
        }
        catch (Exception ex)
        {
            await SendErrorResponse(ctx, 500, $"Failed to delete session: {ex.Message}");
        }
    }

    public async Task HandleRenameSessionRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<SessionRenameRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Name))
        {
            await SendErrorResponse(ctx, 400, "Name is required");
            return;
        }

        _memoryService.SetSessionName(body.Name.Trim());
        ColorLog.Success("SESSION", $"会话已重命名: {body.Name}");
        await SendJsonResponse(ctx, new { success = true, message = "Session renamed" });
    }

    public async Task HandleGenerateSessionNameRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<SessionGenerateNameRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Message))
        {
            await SendErrorResponse(ctx, 400, "Message is required");
            return;
        }

        var config = _gateway.Config;
        var provider = ConfigLoader.GetCurrentProvider(config);
        if (provider == null || string.IsNullOrWhiteSpace(provider.BaseUrl) || string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            await SendErrorResponse(ctx, 400, "LLM API not configured");
            return;
        }

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

            var requestBody = new
            {
                model = provider.CurrentModel,
                max_tokens = 50,
                messages = new[]
                {
                    new { role = "user", content = $"请你根据用户的输入来对本次会话生成一个会话名，不超过10个字，不要有其他字符，直接输出即可。\n\n用户输入：{body.Message}" }
                }
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var baseUrl = provider.BaseUrl.TrimEnd('/');
            string chatUrl;
            if (baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) ||
                baseUrl.EndsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
                chatUrl = baseUrl;
            else if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                chatUrl = $"{baseUrl}/chat/completions";
            else
                chatUrl = $"{baseUrl}/v1/chat/completions";

            using var response = await http.PostAsync(chatUrl, new StringContent(json, Encoding.UTF8, "application/json"));
            var result = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(result);
            if (response.IsSuccessStatusCode &&
                doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0)
            {
                var message = choices[0].GetProperty("message");
                var content = message.GetProperty("content");
                var generatedName = (content.ValueKind == JsonValueKind.String
                    ? content.GetString() : content.ToString())?.Trim() ?? "新会话";

                // Limit to 10 chars
                if (generatedName.Length > 10)
                    generatedName = generatedName[..10];

                _memoryService.SetSessionName(generatedName);
                ColorLog.Success("SESSION", $"AI 生成会话名: {generatedName}");
                await SendJsonResponse(ctx, new { success = true, data = new { name = generatedName } });
            }
            else
            {
                await SendErrorResponse(ctx, 500, "Failed to generate session name");
            }
        }
        catch (Exception ex)
        {
            await SendErrorResponse(ctx, 500, $"Failed to generate name: {ex.Message}");
        }
    }
}

public record SessionSwitchRequest
{
    public string? Session { get; init; }
}

public record SessionRenameRequest
{
    public string? Name { get; init; }
}

public record SessionGenerateNameRequest
{
    public string? Message { get; init; }
}
