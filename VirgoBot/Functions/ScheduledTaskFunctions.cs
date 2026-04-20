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
            "管理定时任务。支持的操作：list(列出所有任务)、get(获取指定任务)、create(创建任务)、update(更新任务)、delete(删除任务)、toggle(启用/禁用任务)。" +
            "scheduleType 支持：interval(按间隔重复)、daily(每天指定时间)、once(一次性任务，执行后自动关闭)。" +
            "一次性任务可通过 onceDelayMinutes(多少分钟后执行) 或 onceAt(指定 ISO 8601 时间) 设置执行时机。",
            new
            {
                type = "object",
                properties = new
                {
                    operation = new { type = "string", description = "操作类型：list | get | create | update | delete | toggle" },
                    task_id = new { type = "string", description = "任务 ID（get/update/delete/toggle 时必填）" },
                    enabled = new { type = "boolean", description = "toggle 操作时指定启用或禁用" },
                    task = new
                    {
                        type = "object",
                        description = "create/update 时的任务数据",
                        properties = new
                        {
                            name = new { type = "string" },
                            description = new { type = "string" },
                            enabled = new { type = "boolean" },
                            taskType = new { type = "string", description = "http | shell | text" },
                            scheduleType = new { type = "string", description = "interval | daily | once" },
                            intervalMinutes = new { type = "integer" },
                            dailyTime = new { type = "string", description = "HH:mm 格式" },
                            onceDelayMinutes = new { type = "integer", description = "一次性任务：多少分钟后执行" },
                            onceAt = new { type = "string", description = "一次性任务：ISO 8601 指定执行时间" },
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
                    _ => Task.FromResult("未知操作，支持：list | get | create | update | delete | toggle")
                };
            });
    }

    private static string HandleList(ScheduledTaskService svc)
    {
        var tasks = svc.GetAllTasks();
        if (tasks.Count == 0) return "当前没有定时任务。";
        var lines = tasks.Select(t =>
            $"- [{(t.Enabled ? "启用" : "禁用")}] {t.Name} (id={t.Id}, scheduleType={t.ScheduleType}, taskType={t.TaskType}, nextRun={t.NextRunTime?.ToLocalTime():yyyy-MM-dd HH:mm:ss})");
        return string.Join("\n", lines);
    }

    private static string HandleGet(ScheduledTaskService svc, JsonElement input)
    {
        var id = input.TryGetProperty("task_id", out var idEl) ? idEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(id)) return "缺少 task_id";
        var task = svc.GetTask(id);
        if (task == null) return $"未找到任务: {id}";
        return JsonSerializer.Serialize(task, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string HandleCreate(ScheduledTaskService svc, JsonElement input)
    {
        if (!input.TryGetProperty("task", out var taskEl)) return "缺少 task 字段";
        var task = ParseTask(taskEl);
        var created = svc.CreateTask(task);
        return $"任务已创建，id={created.Id}，名称={created.Name}，下次执行={created.NextRunTime?.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
    }

    private static string HandleUpdate(ScheduledTaskService svc, JsonElement input)
    {
        var id = input.TryGetProperty("task_id", out var idEl) ? idEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(id)) return "缺少 task_id";
        if (!input.TryGetProperty("task", out var taskEl)) return "缺少 task 字段";
        var task = ParseTask(taskEl);
        return svc.UpdateTask(id, task) ? $"任务已更新: {id}" : $"未找到任务: {id}";
    }

    private static string HandleDelete(ScheduledTaskService svc, JsonElement input)
    {
        var id = input.TryGetProperty("task_id", out var idEl) ? idEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(id)) return "缺少 task_id";
        return svc.DeleteTask(id) ? $"任务已删除: {id}" : $"未找到任务: {id}";
    }

    private static string HandleToggle(ScheduledTaskService svc, JsonElement input)
    {
        var id = input.TryGetProperty("task_id", out var idEl) ? idEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(id)) return "缺少 task_id";
        var enabled = input.TryGetProperty("enabled", out var enEl) && enEl.GetBoolean();
        return svc.ToggleTask(id, enabled) ? $"任务已{(enabled ? "启用" : "禁用")}: {id}" : $"未找到任务: {id}";
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
        if (el.TryGetProperty("taskRequirement", out var tr)) task.TaskRequirement = tr.GetString() ?? "";
        if (el.TryGetProperty("httpMethod", out var hm)) task.HttpMethod = hm.GetString() ?? "GET";
        if (el.TryGetProperty("httpUrl", out var hu)) task.HttpUrl = hu.GetString() ?? "";
        if (el.TryGetProperty("httpBody", out var hb)) task.HttpBody = hb.GetString() ?? "";
        if (el.TryGetProperty("shellCommand", out var sc)) task.ShellCommand = sc.GetString() ?? "";
        if (el.TryGetProperty("textInstruction", out var ti)) task.TextInstruction = ti.GetString() ?? "";
        return task;
    }
}
