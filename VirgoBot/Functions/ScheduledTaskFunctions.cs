using System.Text.Json;
using VirgoBot.Models;
using VirgoBot.Services;

namespace VirgoBot.Functions;

public static class ScheduledTaskFunctions
{
    public static IEnumerable<FunctionDefinition> Register(ScheduledTaskService taskService)
    {
        yield return new FunctionDefinition(
            "manage_scheduled_tasks",
            "Manage scheduled tasks. Supported operations: list, get, create, update, delete, toggle." +
            "scheduleType values: interval (repeat at intervals), daily (at specified time each day), once (one-time task, auto-disables after execution), message_count (trigger by conversation turn count)." +
            "message_count type requires messageCountTarget (trigger count) and messageCountRole (user or assistant)." +
            "One-time tasks can set onceDelayMinutes (execute after N minutes) or onceAt (ISO 8601 timestamp) to specify execution time.",
            new
            {
                type = "object",
                properties = new
                {
                    operation = new { type = "string", description = "Operation: list | get | create | update | delete | toggle" },
                    task_id = new { type = "string", description = "Task ID (required for get/update/delete/toggle)" },
                    enabled = new { type = "boolean", description = "Enable or disable for toggle operation" },
                    task = new
                    {
                        type = "object",
                        description = "Task data for create/update",
                        properties = new
                        {
                            name = new { type = "string" },
                            description = new { type = "string" },
                            enabled = new { type = "boolean" },
                            taskType = new { type = "string", description = "http | shell | text" },
                            scheduleType = new { type = "string", description = "interval | daily | once | message_count" },
                            intervalMinutes = new { type = "integer" },
                            dailyTime = new { type = "string", description = "HH:mm format" },
                            onceDelayMinutes = new { type = "integer", description = "One-time task: execute after N minutes" },
                            onceAt = new { type = "string", description = "One-time task: ISO 8601 execution time" },
                            messageCountTarget = new { type = "integer", description = "message_count type: trigger every N messages" },
                            messageCountRole = new { type = "string", description = "message_count type: count role user | assistant" },
                            taskRequirement = new { type = "string" },
                            httpMethod = new { type = "string" },
                            httpUrl = new { type = "string" },
                            httpBody = new { type = "string" },
                            shellCommand = new { type = "string" },
                            textInstruction = new { type = "string" },
                        }
                    }
                },
                required = new[] { "operation" }
            },
            input =>
            {
                var operation = input.TryGetProperty("operation", out var op) ? op.GetString() : null;
                return operation switch
                {
                    "list" => Task.FromResult(HandleList(taskService)),
                    "get" => Task.FromResult(HandleGet(taskService, input)),
                    "create" => Task.FromResult(HandleCreate(taskService, input)),
                    "update" => Task.FromResult(HandleUpdate(taskService, input)),
                    "delete" => Task.FromResult(HandleDelete(taskService, input)),
                    "toggle" => Task.FromResult(HandleToggle(taskService, input)),
                    _ => Task.FromResult("Unknown operation, supported: list | get | create | update | delete | toggle")
                };
            });
    }

    private static string HandleList(ScheduledTaskService svc)
    {
        var tasks = svc.GetAllTasks();
        if (tasks.Count == 0) return "No scheduled tasks.";
        var lines = tasks.Select(t =>
        {
            var schedule = t.ScheduleType == "message_count"
                ? $"every {t.MessageCountTarget} {t.MessageCountRole} messages, current {t.MessageCountCurrent}/{t.MessageCountTarget}"
                : $"nextRun={t.NextRunTime?.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
            return $"- [{(t.Enabled ? "enabled" : "disabled")}] {t.Name} (id={t.Id}, scheduleType={t.ScheduleType}, taskType={t.TaskType}, {schedule})";
        });
        return string.Join("\n", lines);
    }

    private static string HandleGet(ScheduledTaskService svc, JsonElement input)
    {
        var id = input.TryGetProperty("task_id", out var idEl) ? idEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(id)) return "Missing task_id";
        var task = svc.GetTask(id);
        if (task == null) return $"Task not found: {id}";
        return JsonSerializer.Serialize(task, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string HandleCreate(ScheduledTaskService svc, JsonElement input)
    {
        if (!input.TryGetProperty("task", out var taskEl)) return "Missing task field";
        var task = ParseTask(taskEl);
        var created = svc.CreateTask(task);
        return $"Task created. id={created.Id}, name={created.Name}, next run={created.NextRunTime?.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
    }

    private static string HandleUpdate(ScheduledTaskService svc, JsonElement input)
    {
        var id = input.TryGetProperty("task_id", out var idEl) ? idEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(id)) return "Missing task_id";
        if (!input.TryGetProperty("task", out var taskEl)) return "Missing task field";
        var task = ParseTask(taskEl);
        return svc.UpdateTask(id, task) ? $"Task updated: {id}" : $"Task not found: {id}";
    }

    private static string HandleDelete(ScheduledTaskService svc, JsonElement input)
    {
        var id = input.TryGetProperty("task_id", out var idEl) ? idEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(id)) return "Missing task_id";
        return svc.DeleteTask(id) ? $"Task deleted: {id}" : $"Task not found: {id}";
    }

    private static string HandleToggle(ScheduledTaskService svc, JsonElement input)
    {
        var id = input.TryGetProperty("task_id", out var idEl) ? idEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(id)) return "Missing task_id";
        var enabled = input.TryGetProperty("enabled", out var enEl) && enEl.GetBoolean();
        return svc.ToggleTask(id, enabled) ? $"Task {(enabled ? "enabled" : "disabled")}: {id}" : $"Task not found: {id}";
    }

    private static ScheduledTask ParseTask(JsonElement el)
    {
        var task = new ScheduledTask();
        if (el.TryGetProperty("name", out var n)) task.Name = n.GetString() ?? "";
        if (el.TryGetProperty("description", out var d)) task.Description = d.GetString() ?? "";
        if (el.TryGetProperty("enabled", out var en)) task.Enabled = en.GetBoolean();
        if (el.TryGetProperty("taskType", out var tt)) task.TaskType = tt.GetString() ?? "text";
        if (el.TryGetProperty("scheduleType", out var st)) task.ScheduleType = st.GetString() ?? "interval";
        if (el.TryGetProperty("intervalMinutes", out var im)) task.IntervalMinutes = im.GetInt32();
        if (el.TryGetProperty("dailyTime", out var dt)) task.DailyTime = dt.GetString() ?? "09:00";
        if (el.TryGetProperty("onceDelayMinutes", out var odm) && odm.ValueKind != JsonValueKind.Null)
            task.OnceDelayMinutes = odm.GetInt32();
        if (el.TryGetProperty("onceAt", out var oa) && oa.ValueKind != JsonValueKind.Null)
            task.OnceAt = DateTime.Parse(oa.GetString()!).ToUniversalTime();
        if (el.TryGetProperty("messageCountTarget", out var mct)) task.MessageCountTarget = mct.GetInt32();
        if (el.TryGetProperty("messageCountRole", out var mcr)) task.MessageCountRole = mcr.GetString() ?? "user";
        if (el.TryGetProperty("taskRequirement", out var tr)) task.TaskRequirement = tr.GetString() ?? "";
        if (el.TryGetProperty("httpMethod", out var hm)) task.HttpMethod = hm.GetString() ?? "GET";
        if (el.TryGetProperty("httpUrl", out var hu)) task.HttpUrl = hu.GetString() ?? "";
        if (el.TryGetProperty("httpBody", out var hb)) task.HttpBody = hb.GetString() ?? "";
        if (el.TryGetProperty("shellCommand", out var sc)) task.ShellCommand = sc.GetString() ?? "";
        if (el.TryGetProperty("textInstruction", out var ti)) task.TextInstruction = ti.GetString() ?? "";
        return task;
    }
}
