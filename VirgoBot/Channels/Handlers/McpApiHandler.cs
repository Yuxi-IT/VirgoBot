using System.Net;
using VirgoBot.Configuration;
using VirgoBot.Services;
using VirgoBot.Utilities;
using static VirgoBot.Channels.Handlers.HttpResponseHelper;

namespace VirgoBot.Channels.Handlers;

public class McpApiHandler
{
    private readonly Gateway _gateway;

    public McpApiHandler(Gateway gateway)
    {
        _gateway = gateway;
    }

    public async Task HandleGetServersRequest(HttpListenerContext ctx)
    {
        var configs = McpConfigLoader.Load();
        var mcpService = _gateway.McpClientService;

        var servers = configs.Select(c =>
        {
            var status = mcpService?.GetStatus(c.Name);
            return new
            {
                c.Name,
                c.Transport,
                c.Command,
                c.Args,
                c.Env,
                c.Url,
                c.Enabled,
                Status = status?.Status ?? (c.Enabled ? "disconnected" : "disabled"),
                ToolCount = status?.ToolCount ?? 0,
                Error = status?.Error,
                Logs = status?.Logs ?? new List<string>(),
            };
        }).ToList();

        await SendJsonResponse(ctx, new { success = true, data = servers });
    }

    public async Task HandleCreateServerRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<McpServerConfig>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Name))
        {
            await SendErrorResponse(ctx, 400, "Name is required");
            return;
        }

        var configs = McpConfigLoader.Load();
        if (configs.Any(c => c.Name.Equals(body.Name, StringComparison.OrdinalIgnoreCase)))
        {
            await SendErrorResponse(ctx, 409, "Server name already exists");
            return;
        }

        configs.Add(body);
        McpConfigLoader.Save(configs);

        // Auto-connect if enabled
        if (body.Enabled && _gateway.McpClientService != null)
        {
            await _gateway.McpClientService.ReconnectServerAsync(body.Name, configs);
            // Re-register tools
            _gateway.FunctionRegistry.SetMcpService(_gateway.McpClientService);
        }

        ColorLog.Success("MCP", $"MCP 服务器已添加: {body.Name}");
        await SendJsonResponse(ctx, new { success = true, message = "Server added" });
    }

    public async Task HandleUpdateServerRequest(HttpListenerContext ctx)
    {
        var name = ctx.Request.Url!.AbsolutePath.Replace("/api/mcp/servers/", "").Split('/')[0];
        name = Uri.UnescapeDataString(name);

        var body = await ReadRequestBody<McpServerConfig>(ctx);
        if (body == null)
        {
            await SendErrorResponse(ctx, 400, "Invalid request body");
            return;
        }

        var configs = McpConfigLoader.Load();
        var index = configs.FindIndex(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            await SendErrorResponse(ctx, 404, "Server not found");
            return;
        }

        body.Name = string.IsNullOrWhiteSpace(body.Name) ? name : body.Name;
        configs[index] = body;
        McpConfigLoader.Save(configs);

        // Reconnect
        if (_gateway.McpClientService != null)
        {
            if (!name.Equals(body.Name, StringComparison.OrdinalIgnoreCase))
            {
                // Name changed — disconnect old, connect new
                await _gateway.McpClientService.ReconnectServerAsync(name, new List<McpServerConfig>());
            }
            await _gateway.McpClientService.ReconnectServerAsync(body.Name, configs);
            _gateway.FunctionRegistry.SetMcpService(_gateway.McpClientService);
        }

        ColorLog.Success("MCP", $"MCP 服务器已更新: {body.Name}");
        await SendJsonResponse(ctx, new { success = true, message = "Server updated" });
    }

    public async Task HandleDeleteServerRequest(HttpListenerContext ctx)
    {
        var name = ctx.Request.Url!.AbsolutePath.Replace("/api/mcp/servers/", "").Split('/')[0];
        name = Uri.UnescapeDataString(name);

        var configs = McpConfigLoader.Load();
        var removed = configs.RemoveAll(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
        {
            await SendErrorResponse(ctx, 404, "Server not found");
            return;
        }

        McpConfigLoader.Save(configs);

        // Disconnect
        if (_gateway.McpClientService != null)
        {
            await _gateway.McpClientService.ReconnectServerAsync(name, new List<McpServerConfig>());
        }

        ColorLog.Success("MCP", $"MCP 服务器已删除: {name}");
        await SendJsonResponse(ctx, new { success = true, message = "Server deleted" });
    }

    public async Task HandleRestartServerRequest(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath; // /api/mcp/servers/{name}/restart
        var segments = path.Replace("/api/mcp/servers/", "").Split('/');
        var name = Uri.UnescapeDataString(segments[0]);

        var configs = McpConfigLoader.Load();
        if (!configs.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            await SendErrorResponse(ctx, 404, "Server not found");
            return;
        }

        if (_gateway.McpClientService == null)
        {
            // Create new service if none exists
            var mcpService = new McpClientService();
            await mcpService.ReconnectServerAsync(name, configs);
            // Note: we can't easily set this back on gateway without restart
            await SendErrorResponse(ctx, 500, "MCP service not initialized, please restart gateway");
            return;
        }

        await _gateway.McpClientService.ReconnectServerAsync(name, configs);
        _gateway.FunctionRegistry.SetMcpService(_gateway.McpClientService);

        ColorLog.Success("MCP", $"MCP 服务器已重连: {name}");
        await SendJsonResponse(ctx, new { success = true, message = "Server restarted" });
    }

    public async Task HandleGetToolsRequest(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath; // /api/mcp/servers/{name}/tools
        var segments = path.Replace("/api/mcp/servers/", "").Split('/');
        var name = Uri.UnescapeDataString(segments[0]);

        var mcpService = _gateway.McpClientService;
        if (mcpService == null)
        {
            await SendJsonResponse(ctx, new { success = true, data = Array.Empty<object>() });
            return;
        }

        var tools = mcpService.GetTools(name).Select(t => new
        {
            t.Name,
            t.Description,
            t.InputSchema,
        }).ToList();

        await SendJsonResponse(ctx, new { success = true, data = tools });
    }
}
