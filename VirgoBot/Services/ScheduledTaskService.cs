using System.Diagnostics;
using System.Text;
using System.Text.Json;
using VirgoBot.Configuration;
using VirgoBot.Models;
using VirgoBot.Utilities;

namespace VirgoBot.Services;

public class ScheduledTaskService
{
    private const string TasksFileName = "scheduled_tasks.json";
    private readonly string _tasksFilePath;
    private readonly List<ScheduledTask> _tasks = new();
    private readonly Dictionary<string, Timer> _timers = new();
    private readonly HttpClient _httpClient = new();
    private readonly object _lock = new();
    private LLMService? _llmService;

    public ScheduledTaskService(LLMService? llmService = null)
    {
        _llmService = llmService;
        var configDir = AppConstants.ConfigDirectory;
        Directory.CreateDirectory(configDir);
        _tasksFilePath = Path.Combine(configDir, TasksFileName);
        LoadTasks();
        StartAllEnabledTasks();
    }

    public void SetLlmService(LLMService llmService)
    {
        _llmService = llmService;
    }

    private void LoadTasks()
    {
        if (!File.Exists(_tasksFilePath))
        {
            SaveTasks();
            return;
        }

        try
        {
            var json = File.ReadAllText(_tasksFilePath);
            var tasks = JsonSerializer.Deserialize<List<ScheduledTask>>(json);
            if (tasks != null)
            {
                _tasks.Clear();
                _tasks.AddRange(tasks);
            }
        }
        catch (Exception ex)
        {
            ColorLog.Error("TASK", $"加载定时任务失败: {ex.Message}");
        }
    }

    private void SaveTasks()
    {
        try
        {
            var json = JsonSerializer.Serialize(_tasks, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_tasksFilePath, json);
        }
        catch (Exception ex)
        {
            ColorLog.Error("TASK", $"保存定时任务失败: {ex.Message}");
        }
    }

    public List<ScheduledTask> GetAllTasks()
    {
        lock (_lock)
        {
            return _tasks.ToList();
        }
    }

    public ScheduledTask? GetTask(string id)
    {
        lock (_lock)
        {
            return _tasks.FirstOrDefault(t => t.Id == id);
        }
    }

    public ScheduledTask CreateTask(ScheduledTask task)
    {
        lock (_lock)
        {
            task.Id = Guid.NewGuid().ToString();
            task.CreatedAt = DateTime.UtcNow;
            CalculateNextRunTime(task);
            _tasks.Add(task);
            SaveTasks();

            if (task.Enabled)
            {
                StartTask(task);
            }

            ColorLog.Success("TASK", $"定时任务已创建: {task.Name}");
            return task;
        }
    }

    public bool UpdateTask(string id, ScheduledTask updatedTask)
    {
        lock (_lock)
        {
            var index = _tasks.FindIndex(t => t.Id == id);
            if (index == -1) return false;

            StopTask(id);

            updatedTask.Id = id;
            updatedTask.CreatedAt = _tasks[index].CreatedAt;
            CalculateNextRunTime(updatedTask);
            _tasks[index] = updatedTask;
            SaveTasks();

            if (updatedTask.Enabled)
            {
                StartTask(updatedTask);
            }

            ColorLog.Success("TASK", $"定时任务已更新: {updatedTask.Name}");
            return true;
        }
    }

    public bool DeleteTask(string id)
    {
        lock (_lock)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task == null) return false;

            StopTask(id);
            _tasks.Remove(task);
            SaveTasks();

            ColorLog.Success("TASK", $"定时任务已删除: {task.Name}");
            return true;
        }
    }

    public bool ToggleTask(string id, bool enabled)
    {
        lock (_lock)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task == null) return false;

            task.Enabled = enabled;
            SaveTasks();

            if (enabled)
            {
                StartTask(task);
                ColorLog.Success("TASK", $"定时任务已启用: {task.Name}");
            }
            else
            {
                StopTask(id);
                ColorLog.Info("TASK", $"定时任务已禁用: {task.Name}");
            }

            return true;
        }
    }

    private void StartAllEnabledTasks()
    {
        foreach (var task in _tasks.Where(t => t.Enabled))
        {
            StartTask(task);
        }
    }

    private void StartTask(ScheduledTask task)
    {
        StopTask(task.Id);

        // message_count 类型不需要 Timer，由 NotifyMessage 驱动
        if (task.ScheduleType == "message_count")
        {
            ColorLog.Info("TASK", $"消息计数任务已就绪: {task.Name} (每{task.MessageCountTarget}条{task.MessageCountRole}消息触发, 当前计数: {task.MessageCountCurrent})");
            return;
        }

        if (task.ScheduleType == "once")
        {
            var delay = CalculateOnceDelay(task);
            if (delay < 0) return; 

            // 一次性任务
            var timer = new Timer(async _ =>
            {
                await ExecuteTask(task);
                lock (_lock)
                {
                    task.Enabled = false;
                    StopTask(task.Id);
                    SaveTasks();
                }
                ColorLog.Info("TASK", $"一次性任务已完成并自动关闭: {task.Name}");
            }, null, (long)delay, Timeout.Infinite);
            _timers[task.Id] = timer;
            ColorLog.Info("TASK", $"一次性任务已调度: {task.Name} (延迟: {delay}ms)");
            return;
        }

        var interval = CalculateInterval(task);
        if (interval <= 0) return;

        var repeatingTimer = new Timer(async _ => await ExecuteTask(task), null, interval, interval);
        _timers[task.Id] = repeatingTimer;

        ColorLog.Info("TASK", $"定时任务已启动: {task.Name} (间隔: {interval}ms)");
    }

    private void StopTask(string id)
    {
        if (_timers.TryGetValue(id, out var timer))
        {
            timer.Dispose();
            _timers.Remove(id);
        }
    }

    private int CalculateInterval(ScheduledTask task)
    {
        return task.ScheduleType switch
        {
            "interval" => task.IntervalMinutes * 60 * 1000,
            "daily" => CalculateDailyInterval(task),
            _ => 60 * 60 * 1000
        };
    }

    private double CalculateOnceDelay(ScheduledTask task)
    {
        if (task.OnceAt.HasValue)
        {
            var delay = (task.OnceAt.Value.ToUniversalTime() - DateTime.UtcNow).TotalMilliseconds;
            return delay;
        }
        if (task.OnceDelayMinutes.HasValue)
        {
            return task.OnceDelayMinutes.Value * 60.0 * 1000;
        }
        return -1;
    }

    private int CalculateDailyInterval(ScheduledTask task)
    {
        if (!TimeSpan.TryParse(task.DailyTime, out var targetTime))
            return 24 * 60 * 60 * 1000;

        var now = DateTime.Now;
        var target = now.Date.Add(targetTime);
        if (target < now)
            target = target.AddDays(1);

        var delay = (int)(target - now).TotalMilliseconds;
        return delay > 0 ? delay : 24 * 60 * 60 * 1000;
    }

    private void CalculateNextRunTime(ScheduledTask task)
    {
        if (task.ScheduleType == "message_count")
        {
            // 消息计数任务没有固定的下次执行时间
            task.NextRunTime = null;
            return;
        }
        if (task.ScheduleType == "once")
        {
            if (task.OnceAt.HasValue)
                task.NextRunTime = task.OnceAt.Value.ToUniversalTime();
            else if (task.OnceDelayMinutes.HasValue)
                task.NextRunTime = DateTime.UtcNow.AddMinutes(task.OnceDelayMinutes.Value);
            return;
        }
        var interval = CalculateInterval(task);
        task.NextRunTime = DateTime.UtcNow.AddMilliseconds(interval);
    }

    private async Task ExecuteTask(ScheduledTask task)
    {
        try
        {
            ColorLog.Info("TASK", $"执行定时任务: {task.Name}");

            task.LastRunTime = DateTime.UtcNow;
            CalculateNextRunTime(task);
            SaveTasks();

            if (task.TaskType == "http")
            {
                await ExecuteHttpTask(task);
            }
            else if (task.TaskType == "shell")
            {
                await ExecuteShellTask(task);
            }
            else if (task.TaskType == "text")
            {
                await ExecuteTextTask(task);
            }

            ColorLog.Success("TASK", $"定时任务执行成功: {task.Name}");
        }
        catch (Exception ex)
        {
            ColorLog.Error("TASK", $"定时任务执行失败: {task.Name} - {ex.Message}");
        }
    }

    private async Task ExecuteHttpTask(ScheduledTask task)
    {
        var request = new HttpRequestMessage
        {
            Method = new HttpMethod(task.HttpMethod),
            RequestUri = new Uri(task.HttpUrl)
        };

        foreach (var header in task.HttpHeaders)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (!string.IsNullOrWhiteSpace(task.HttpBody) && task.HttpMethod != "GET")
        {
            request.Content = new StringContent(task.HttpBody, Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        ColorLog.Info("TASK", $"HTTP 响应: {response.StatusCode} - {content.Substring(0, Math.Min(100, content.Length))}");
    }

    private async Task ExecuteShellTask(ScheduledTask task)
    {
        var isWindows = OperatingSystem.IsWindows();
        var shell = isWindows ? "cmd.exe" : "/bin/bash";
        var shellArg = isWindows ? "/c" : "-c";

        var psi = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = $"{shellArg} {task.ShellCommand}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return;

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (!string.IsNullOrWhiteSpace(output))
            ColorLog.Info("TASK", $"Shell 输出: {output.Substring(0, Math.Min(200, output.Length))}");

        if (!string.IsNullOrWhiteSpace(error))
            ColorLog.Error("TASK", $"Shell 错误: {error.Substring(0, Math.Min(200, error.Length))}");
    }

    private async Task ExecuteTextTask(ScheduledTask task)
    {
        if (_llmService == null)
        {
            ColorLog.Error("TASK", "LLMService 未初始化，无法执行文本指令任务");
            return;
        }

        if (string.IsNullOrWhiteSpace(task.TextInstruction))
        {
            ColorLog.Error("TASK", "文本指令为空");
            return;
        }

        ColorLog.Info("TASK", $"执行文本指令: {task.TextInstruction}");

        try
        {
            var response = await _llmService.AskAsync($"系统消息：定时任务触发\n内容：{task.TextInstruction}", isSystemTask: true);
            ColorLog.Success("TASK", $"AI 响应: {response.Substring(0, Math.Min(200, response.Length))}");
        }
        catch (Exception ex)
        {
            ColorLog.Error("TASK", $"执行文本指令失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 消息通知：每次用户或助手发送消息时调用，驱动 message_count 类型的定时任务
    /// </summary>
    public void NotifyMessage(string role)
    {
        List<ScheduledTask> tasksToExecute;

        lock (_lock)
        {
            tasksToExecute = new List<ScheduledTask>();

            foreach (var task in _tasks)
            {
                if (!task.Enabled || task.ScheduleType != "message_count") continue;
                if (!string.Equals(task.MessageCountRole, role, StringComparison.OrdinalIgnoreCase)) continue;

                task.MessageCountCurrent++;

                if (task.MessageCountCurrent >= task.MessageCountTarget)
                {
                    task.MessageCountCurrent = 0;
                    tasksToExecute.Add(task);
                }
            }

            // 每次计数变化都持久化，避免重启丢失进度
            SaveTasks();
        }

        // 在锁外异步执行任务
        foreach (var task in tasksToExecute)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await ExecuteTask(task);
                }
                catch (Exception ex)
                {
                    ColorLog.Error("TASK", $"消息计数任务执行失败: {task.Name} - {ex.Message}");
                }
            });
        }
    }

    private void EnsureDefaultTasks()
    {
        if (_tasks.Count > 0) return;

        var defaultTask = new ScheduledTask
        {
            Name = "用户画像总结",
            Description = "每10轮用户对话自动总结用户特征并保存到Soul",
            Enabled = true,
            TaskType = "text",
            ScheduleType = "message_count",
            MessageCountTarget = 10,
            MessageCountRole = "user",
            TextInstruction = "请根据最近的对话内容，总结用户的性格特点、说话方式、最近在做的事情，以及你对用户的评价。" +
                "用简洁的条目形式写出来，然后调用 append_soul 工具将总结保存到 Soul 中。" +
                "注意：不要重复已有的 Soul 内容，只补充新的观察。如果没有新的发现则不需要保存。"
        };

        _tasks.Add(defaultTask);
        SaveTasks();
        ColorLog.Success("TASK", "已自动创建默认任务: 用户画像总结（每10轮用户对话）");
    }

    public void Dispose()
    {
        foreach (var timer in _timers.Values)
        {
            timer.Dispose();
        }
        _timers.Clear();
        _httpClient.Dispose();
    }
}
