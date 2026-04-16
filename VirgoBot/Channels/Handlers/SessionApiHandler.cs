using System.Net;
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
}

public record SessionSwitchRequest
{
    public string? Session { get; init; }
}
