using System.Net;
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
