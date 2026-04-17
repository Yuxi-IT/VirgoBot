using System.Net;
using System.Text.Json;
using VirgoBot.Models;
using VirgoBot.Services;
using static VirgoBot.Channels.Handlers.HttpResponseHelper;

namespace VirgoBot.Channels.Handlers;

public class ScheduledTaskApiHandler
{
    private readonly ScheduledTaskService _taskService;

    public ScheduledTaskApiHandler(ScheduledTaskService taskService)
    {
        _taskService = taskService;
    }

    public async Task HandleGetTasksRequest(HttpListenerContext ctx)
    {
        var tasks = _taskService.GetAllTasks();
        await SendJsonResponse(ctx, new { success = true, data = tasks });
    }

    public async Task HandleGetTaskRequest(HttpListenerContext ctx)
    {
        var id = ctx.Request.Url!.AbsolutePath.Replace("/api/tasks/", "").Split('/')[0];
        var task = _taskService.GetTask(id);

        if (task == null)
        {
            await SendErrorResponse(ctx, 404, "Task not found");
            return;
        }

        await SendJsonResponse(ctx, new { success = true, data = task });
    }

    public async Task HandleCreateTaskRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<ScheduledTask>(ctx);
        if (body == null)
        {
            await SendErrorResponse(ctx, 400, "Invalid request body");
            return;
        }

        var task = _taskService.CreateTask(body);
        await SendJsonResponse(ctx, new { success = true, data = task });
    }

    public async Task HandleUpdateTaskRequest(HttpListenerContext ctx)
    {
        var id = ctx.Request.Url!.AbsolutePath.Replace("/api/tasks/", "").Split('/')[0];
        var body = await ReadRequestBody<ScheduledTask>(ctx);

        if (body == null)
        {
            await SendErrorResponse(ctx, 400, "Invalid request body");
            return;
        }

        var success = _taskService.UpdateTask(id, body);
        if (!success)
        {
            await SendErrorResponse(ctx, 404, "Task not found");
            return;
        }

        await SendJsonResponse(ctx, new { success = true, message = "Task updated" });
    }

    public async Task HandleDeleteTaskRequest(HttpListenerContext ctx)
    {
        var id = ctx.Request.Url!.AbsolutePath.Replace("/api/tasks/", "");
        var success = _taskService.DeleteTask(id);

        if (!success)
        {
            await SendErrorResponse(ctx, 404, "Task not found");
            return;
        }

        await SendJsonResponse(ctx, new { success = true, message = "Task deleted" });
    }

    public async Task HandleToggleTaskRequest(HttpListenerContext ctx)
    {
        var id = ctx.Request.Url!.AbsolutePath.Replace("/api/tasks/", "").Replace("/toggle", "");
        var body = await ReadRequestBody<ToggleTaskRequest>(ctx);

        if (body == null)
        {
            await SendErrorResponse(ctx, 400, "Invalid request body");
            return;
        }

        var success = _taskService.ToggleTask(id, body.Enabled);
        if (!success)
        {
            await SendErrorResponse(ctx, 404, "Task not found");
            return;
        }

        await SendJsonResponse(ctx, new { success = true, message = "Task toggled" });
    }
}

public record ToggleTaskRequest
{
    public bool Enabled { get; init; }
}
