using System.Text.Json.Serialization;

namespace VirgoBot.Models;

public class ScheduledTask
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("taskType")]
    public string TaskType { get; set; } = "http"; // "http", "shell", or "text"

    [JsonPropertyName("scheduleType")]
    public string ScheduleType { get; set; } = "interval"; // "interval", "daily", "once", "message_count"

    [JsonPropertyName("intervalMinutes")]
    public int IntervalMinutes { get; set; } = 60;

    [JsonPropertyName("dailyTime")]
    public string DailyTime { get; set; } = "09:00"; // HH:mm format

    [JsonPropertyName("cronExpression")]
    public string CronExpression { get; set; } = "";

    // 一次性任务：延迟多少分钟后执行（与 onceAt 二选一）
    [JsonPropertyName("onceDelayMinutes")]
    public int? OnceDelayMinutes { get; set; }

    // 一次性任务：指定执行时间（ISO 8601）
    [JsonPropertyName("onceAt")]
    public DateTime? OnceAt { get; set; }

    // 按对话轮数触发
    [JsonPropertyName("messageCountTarget")]
    public int MessageCountTarget { get; set; } = 10;

    [JsonPropertyName("messageCountRole")]
    public string MessageCountRole { get; set; } = "user"; // "user" or "assistant"

    [JsonPropertyName("messageCountCurrent")]
    public int MessageCountCurrent { get; set; } = 0;
    [JsonPropertyName("taskRequirement")]
    public string TaskRequirement { get; set; } = "";

    [JsonPropertyName("httpMethod")]
    public string HttpMethod { get; set; } = "GET";

    [JsonPropertyName("httpUrl")]
    public string HttpUrl { get; set; } = "";

    [JsonPropertyName("httpHeaders")]
    public Dictionary<string, string> HttpHeaders { get; set; } = new();

    [JsonPropertyName("httpBody")]
    public string HttpBody { get; set; } = "";

    [JsonPropertyName("shellCommand")]
    public string ShellCommand { get; set; } = "";

    [JsonPropertyName("textInstruction")]
    public string TextInstruction { get; set; } = "";

    [JsonPropertyName("lastRunTime")]
    public DateTime? LastRunTime { get; set; }

    [JsonPropertyName("nextRunTime")]
    public DateTime? NextRunTime { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
