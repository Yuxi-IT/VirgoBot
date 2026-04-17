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

    public ScheduledTaskService()
    {
        var configDir = AppConstants.ConfigDirectory;
        Directory.CreateDirectory(configDir);
        _tasksFilePath = Path.Combine(configDir, TasksFileName);
        LoadTasks();
        StartAllEnabledTasks();
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

        var interval = CalculateInterval(task);
        if (interval <= 0) return;

        var timer = new Timer(async _ => await ExecuteTask(task), null, interval, interval);
        _timers[task.Id] = timer;

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
            _ => 60 * 60 * 1000 // default 1 hour
        };
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
