using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VirgoBot.Configuration;
using VirgoBot.Services;
using VirgoBot.Utilities;
using static VirgoBot.Channels.Handlers.HttpResponseHelper;

namespace VirgoBot.Channels.Handlers;

public class AgentApiHandler
{
    private readonly Gateway _gateway;
    private readonly MemoryService _memoryService;

    public AgentApiHandler(Gateway gateway, MemoryService memoryService)
    {
        _gateway = gateway;
        _memoryService = memoryService;
    }

    public async Task HandleGetAgentsRequest(HttpListenerContext ctx)
    {
        var agentDir = Path.Combine(AppConstants.ConfigDirectory, "agent");
        Directory.CreateDirectory(agentDir);

        var agents = new List<object>();
        foreach (var file in Directory.GetFiles(agentDir, "*.md"))
        {
            var fileName = Path.GetFileName(file);
            var content = await File.ReadAllTextAsync(file);
            var preview = content.Length > 200 ? content[..200] + "..." : content;
            agents.Add(new
            {
                name = Path.GetFileNameWithoutExtension(file),
                fileName,
                memoryPath = $"agent/{fileName}",
                preview,
                size = content.Length
            });
        }

        var currentAgent = _gateway.Config.MemoryFile;
        await SendJsonResponse(ctx, new { success = true, data = new { agents, currentAgent } });
    }

    public async Task HandleGetAgentRequest(HttpListenerContext ctx)
    {
        var name = Uri.UnescapeDataString(ctx.Request.Url!.AbsolutePath.Replace("/api/agents/", ""));
        var filePath = Path.Combine(AppConstants.ConfigDirectory, "agent", $"{name}.md");

        if (!File.Exists(filePath))
        {
            await SendErrorResponse(ctx, 404, "Agent not found");
            return;
        }

        var content = await File.ReadAllTextAsync(filePath);
        await SendJsonResponse(ctx, new { success = true, data = new { name, content } });
    }

    public async Task HandleSwitchAgentRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<AgentSwitchRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.MemoryFile))
        {
            await SendErrorResponse(ctx, 400, "MemoryFile is required");
            return;
        }

        var config = _gateway.Config;
        config.MemoryFile = body.MemoryFile;
        ConfigLoader.Save(config);
        await SendJsonResponse(ctx, new { success = true, message = "Agent switched" });
    }

    public async Task HandleCreateAgentRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<AgentCreateUpdateRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Name) || body.Content == null)
        {
            await SendErrorResponse(ctx, 400, "Name and content are required");
            return;
        }

        var safeName = body.Name.Trim();
        if (string.IsNullOrWhiteSpace(safeName))
        {
            await SendErrorResponse(ctx, 400, "Invalid agent name");
            return;
        }

        var agentDir = Path.Combine(AppConstants.ConfigDirectory, "agent");
        Directory.CreateDirectory(agentDir);
        var filePath = Path.Combine(agentDir, $"{safeName}.md");

        if (File.Exists(filePath))
        {
            await SendErrorResponse(ctx, 409, "Agent already exists");
            return;
        }

        await File.WriteAllTextAsync(filePath, body.Content);
        ColorLog.Success("AGENT", $"Agent 已创建: {safeName}.md");
        await SendJsonResponse(ctx, new { success = true, message = "Agent created" });
    }

    public async Task HandleUpdateAgentRequest(HttpListenerContext ctx)
    {
        var name = Uri.UnescapeDataString(ctx.Request.Url!.AbsolutePath.Replace("/api/agents/", ""));
        var filePath = Path.Combine(AppConstants.ConfigDirectory, "agent", $"{name}.md");

        if (!File.Exists(filePath))
        {
            await SendErrorResponse(ctx, 404, "Agent not found");
            return;
        }

        var body = await ReadRequestBody<AgentCreateUpdateRequest>(ctx);
        if (body == null || body.Content == null)
        {
            await SendErrorResponse(ctx, 400, "Content is required");
            return;
        }

        if (!string.IsNullOrWhiteSpace(body.Name) && body.Name != name)
        {
            var newFilePath = Path.Combine(AppConstants.ConfigDirectory, "agent", $"{body.Name}.md");
            if (File.Exists(newFilePath))
            {
                await SendErrorResponse(ctx, 409, "Target agent name already exists");
                return;
            }
            File.Delete(filePath);
            filePath = newFilePath;

            if (_gateway.Config.MemoryFile == $"agent/{name}.md")
            {
                _gateway.Config.MemoryFile = $"agent/{body.Name}.md";
                ConfigLoader.Save(_gateway.Config);
            }
        }

        await File.WriteAllTextAsync(filePath, body.Content);
        ColorLog.Success("AGENT", $"Agent 已更新: {Path.GetFileName(filePath)}");
        await SendJsonResponse(ctx, new { success = true, message = "Agent updated" });
    }

    public async Task HandleDeleteAgentRequest(HttpListenerContext ctx)
    {
        var name = Uri.UnescapeDataString(ctx.Request.Url!.AbsolutePath.Replace("/api/agents/", ""));
        var filePath = Path.Combine(AppConstants.ConfigDirectory, "agent", $"{name}.md");

        if (!File.Exists(filePath))
        {
            await SendErrorResponse(ctx, 404, "Agent not found");
            return;
        }

        if (_gateway.Config.MemoryFile == $"agent/{name}.md")
        {
            await SendErrorResponse(ctx, 400, "Cannot delete the currently active agent");
            return;
        }

        File.Delete(filePath);
        ColorLog.Success("AGENT", $"Agent 已删除: {name}.md");
        await SendJsonResponse(ctx, new { success = true, message = "Agent deleted" });
    }

    public async Task HandleGenerateAgentRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<AgentGenerateRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.CharacterName))
        {
            await SendErrorResponse(ctx, 400, "CharacterName is required");
            return;
        }

        var characterName = body.CharacterName.Trim();
        var agentName = string.IsNullOrWhiteSpace(body.AgentName) ? characterName : body.AgentName.Trim();

        var agentDir = Path.Combine(AppConstants.ConfigDirectory, "agent");
        Directory.CreateDirectory(agentDir);
        var filePath = Path.Combine(agentDir, $"{agentName}.md");

        if (File.Exists(filePath))
        {
            await SendErrorResponse(ctx, 409, $"Agent '{agentName}' already exists");
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
            var content = await GenerateAgentContentAsync(provider, characterName);
            await File.WriteAllTextAsync(filePath, content);
            ColorLog.Success("AGENT", $"AI 生成 Agent 已保存: {agentName}.md ({content.Length} 字符)");
            await SendJsonResponse(ctx, new { success = true, message = "Agent generated and saved", agentName, contentLength = content.Length });
        }
        catch (Exception ex)
        {
            ColorLog.Error("AGENT", $"AI 生成 Agent 失败: {ex.Message}");
            await SendErrorResponse(ctx, 500, $"Generation failed: {ex.Message}");
        }
    }

    private static async Task<string> GenerateAgentContentAsync(ProviderConfig provider, string characterName)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

        var prompt = $"""
请为角色「{characterName}」生成一份详细的 AI 智能体人物设定文档，用于配置聊天机器人的系统提示词。

要求：
1. 总字数不少于 2000 字符
2. 使用 Markdown 格式
3. 必须包含以下章节：
   - 角色基本信息（姓名、来源作品/背景、年龄、外貌特征）
   - 性格特点（详细描述核心性格、优缺点、行为模式）
   - 说话方式与语言风格（口头禅、语气、常用表达、说话习惯）
   - 兴趣爱好与专长
   - 人际关系与情感倾向
   - 行为准则与价值观
   - 典型对话示例（至少 5 组问答示例，展示角色的说话风格）
4. 设定要符合角色的原著/公众认知，如果是原创角色则自由发挥
5. 文档开头用一句话总结角色定位，作为系统提示词的核心指令

请直接输出 Markdown 文档内容，不要有任何额外说明。
""";

        var requestBody = new
        {
            model = provider.CurrentModel,
            max_tokens = 4096,
            messages = new[]
            {
                new { role = "user", content = prompt }
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
        if (!response.IsSuccessStatusCode)
        {
            var errMsg = doc.RootElement.TryGetProperty("error", out var errEl) &&
                         errEl.TryGetProperty("message", out var msgEl)
                ? msgEl.GetString() ?? $"HTTP {(int)response.StatusCode}"
                : $"HTTP {(int)response.StatusCode}";
            throw new Exception(errMsg);
        }

        var choices = doc.RootElement.GetProperty("choices");
        var message = choices[0].GetProperty("message");
        var content = message.GetProperty("content");

        return content.ValueKind == JsonValueKind.String
            ? content.GetString() ?? ""
            : content.ToString();
    }

    public async Task HandleGetSoulEntriesRequest(HttpListenerContext ctx)
    {
        var entries = _memoryService.GetAllSoulEntries();
        await SendJsonResponse(ctx, new { success = true, data = entries });
    }

    public async Task HandleAddSoulEntryRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<ContentRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Content))
        {
            await SendErrorResponse(ctx, 400, "Content is required");
            return;
        }

        _memoryService.AddSoulEntry(body.Content);
        ColorLog.Success("SOUL", "Soul 记录已添加");
        await SendJsonResponse(ctx, new { success = true, message = "Soul entry added" });
    }

    public async Task HandleUpdateSoulEntryRequest(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath;
        var idStr = path.Replace("/api/soul/", "");

        if (!int.TryParse(idStr, out var id))
        {
            await SendErrorResponse(ctx, 400, "Invalid soul entry ID");
            return;
        }

        var body = await ReadRequestBody<ContentRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Content))
        {
            await SendErrorResponse(ctx, 400, "Content is required");
            return;
        }

        _memoryService.UpdateSoulEntry(id, body.Content);
        ColorLog.Success("SOUL", $"Soul 记录已更新: {id}");
        await SendJsonResponse(ctx, new { success = true, message = "Soul entry updated" });
    }

    public async Task HandleDeleteSoulEntryRequest(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath;
        var idStr = path.Replace("/api/soul/", "");

        if (!int.TryParse(idStr, out var id))
        {
            await SendErrorResponse(ctx, 400, "Invalid soul entry ID");
            return;
        }

        _memoryService.DeleteSoulEntry(id);
        ColorLog.Success("SOUL", $"Soul 记录已删除: {id}");
        await SendJsonResponse(ctx, new { success = true, message = "Soul entry deleted" });
    }
}

public record AgentSwitchRequest
{
    public string? MemoryFile { get; init; }
}

public record AgentCreateUpdateRequest
{
    public string? Name { get; init; }
    public string? Content { get; init; }
}

public record AgentGenerateRequest
{
    public string? CharacterName { get; init; }
    public string? AgentName { get; init; }
}
