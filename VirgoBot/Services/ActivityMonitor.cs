using System.Text;
using System.Text.Json;
using Telegram.Bot;
using VirgoBot.Configuration;
using VirgoBot.Integrations.ILink;
using VirgoBot.Utilities;

namespace VirgoBot.Services;

public class ActivityMonitor
{
    private readonly LLMService _llmService;
    private readonly TelegramBotClient? _bot;
    private readonly WebSocketClientManager _wsManager;
    private readonly ILinkBridgeService? _iLinkBridge;
    private readonly long _userId;
    private readonly Config _config;
    private readonly MemoryService _memoryService;
    private DateTime _lastActivity = DateTime.Now;
    private Timer? _proactiveTimer;
    private readonly Random _random = new();
    private DateTime _lastScenarioCheck = DateTime.MinValue;

    // Default built-in scenarios
    private static readonly List<ScenarioConfig> DefaultScenarios = new()
    {
        new ScenarioConfig
        {
            Name = "morning_greeting",
            TimeRange = "06:00-09:00",
            Prompt = "现在是早上，用户可能刚开始新的一天。请根据用户的高权重记忆，发出一个温馨的早安问候，语气自然亲切。",
            Priority = 1,
            DayOfWeek = new List<string> { "mon", "tue", "wed", "thu", "fri" }
        },
        new ScenarioConfig
        {
            Name = "evening_checkin",
            TimeRange = "17:00-19:00",
            Prompt = "现在接近下班时间，用户可能结束了一天的工作。请根据用户的记忆，发出一句下班关怀或轻松问候。",
            Priority = 1,
            DayOfWeek = new List<string> { "mon", "tue", "wed", "thu", "fri" }
        },
        new ScenarioConfig
        {
            Name = "weekend_chat",
            TimeRange = "10:00-22:00",
            Prompt = "今天是周末，用户可能比较放松。请根据用户最近的记忆和兴趣爱好，发起一个轻松有趣的闲聊话题。",
            Priority = 2,
            DayOfWeek = new List<string> { "sat", "sun" }
        }
    };

    public ActivityMonitor(LLMService llmService, TelegramBotClient? bot, WebSocketClientManager wsManager, ILinkBridgeService? iLinkBridge, long userId, Config config, MemoryService memoryService)
    {
        _llmService = llmService;
        _bot = bot;
        _wsManager = wsManager;
        _iLinkBridge = iLinkBridge;
        _userId = userId;
        _config = config;
        _memoryService = memoryService;
    }

    public void UpdateActivity()
    {
        _lastActivity = DateTime.Now;
        _proactiveTimer?.Dispose();
        _proactiveTimer = null;
    }

    private ScenarioConfig? GetActiveScenario()
    {
        var now = DateTime.Now;
        var dow = now.DayOfWeek.ToString().ToLower()[..3]; // "mon", "tue", etc.

        // Check scenario cooldown (once per hour)
        if ((now - _lastScenarioCheck).TotalHours < 1)
            return null;

        var scenarios = _config.Server.AutoResponse.Scenarios;
        if (scenarios.Count == 0) scenarios = DefaultScenarios;

        foreach (var scenario in scenarios.OrderBy(s => s.Priority))
        {
            // Day of week filter
            if (scenario.DayOfWeek.Count > 0 && !scenario.DayOfWeek.Contains(dow))
                continue;

            // Time range check
            if (string.IsNullOrWhiteSpace(scenario.TimeRange))
                continue;

            var parts = scenario.TimeRange.Split('-');
            if (parts.Length != 2) continue;
            if (!TimeSpan.TryParse(parts[0], out var start) || !TimeSpan.TryParse(parts[1], out var end))
                continue;

            var currentTime = now.TimeOfDay;
            if (currentTime >= start && currentTime <= end)
                return scenario;
        }

        return null;
    }

    public void Start(CancellationToken ct = default)
    {
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(TimeSpan.FromMinutes(1), ct); }
                catch (OperationCanceledException) { break; }

                if (!_config.Server.AutoResponse.Enabled)
                {
                    continue;
                }

                var idle = DateTime.Now - _lastActivity;
                var minIdle = _config.Server.AutoResponse.MinIdleMinutes;

                if (idle.TotalMinutes >= minIdle && _proactiveTimer == null)
                {
                    var minDelay = _config.Server.AutoResponse.MinIdleMinutes;
                    var maxDelay = _config.Server.AutoResponse.MaxIdleMinutes;
                    var delay = _random.Next(minDelay, maxDelay);

                    ColorLog.Info("ACTIVITY", $"用户空闲 {idle.TotalMinutes:F0}分钟，将在 {delay}分钟后主动发消息");

                    _proactiveTimer = new Timer(async _ =>
                    {
                        try
                        {
                            var totalIdle = DateTime.Now - _lastActivity;

                            string prompt;
                            var scenario = GetActiveScenario();
                            if (scenario != null)
                            {
                                var sb = new StringBuilder();
                                sb.AppendLine($"System notification: The user hasn't sent a message for a while.");
                                sb.AppendLine();
                                sb.AppendLine($"Scenario: {scenario.Name}");
                                sb.AppendLine(scenario.Prompt);
                                sb.AppendLine();
                                var topMemories = _memoryService.GetTopSoulByWeight(5);
                                if (topMemories.Count > 0)
                                {
                                    sb.AppendLine("--- User's Important Memories ---");
                                    foreach (var m in topMemories)
                                        sb.AppendLine($"- {m.Content}");
                                }
                                var now = DateTime.Now;
                                sb.AppendLine();
                                sb.AppendLine($"Current time: {now:yyyy-MM-dd HH:mm}");
                                sb.AppendLine($"Day of week: {now.DayOfWeek}");
                                sb.AppendLine();
                                sb.AppendLine("Please respond naturally based on the above context. Keep it brief and warm.");
                                prompt = sb.ToString();
                                _lastScenarioCheck = now;
                                ColorLog.Info("ACTIVITY", $"场景触发: {scenario.Name}");
                            }
                            else
                            {
                                prompt = $"System notification: The user hasn't sent you a message for {totalIdle.TotalMinutes:F0} minutes.";
                            }

                            var reply = await _llmService.AskAsync(prompt, isSystemTask: true);

                            if (_bot != null)
                            {
                                try
                                {
                                    foreach (var line in reply.Split("\n\n"))
                                    {
                                        await _bot.SendMessage(_userId, line);
                                    }
                                }
                                catch (Exception ex) { ColorLog.Error("TG", $"发送失败: {ex.Message}"); }
                            }

                            var msg = JsonSerializer.Serialize(new { type = "proactive", content = reply });
                            await _wsManager.BroadcastAsync(msg);

                            if (_iLinkBridge != null)
                            {
                                try
                                {
                                    await _iLinkBridge.PushTextAsync(reply);
                                }
                                catch (Exception ex) { ColorLog.Error("ILINK", $"主动推送失败: {ex.Message}"); }
                            }

                            UpdateActivity();
                        }
                        catch (Exception ex) { ColorLog.Error("ACTIVITY", $"主动消息失败: {ex.Message}"); }
                    }, null, TimeSpan.FromMinutes(delay), Timeout.InfiniteTimeSpan);
                }
            }
            ColorLog.Info("ACTIVITY", "活动监控已停止");
        }, ct);
    }
}
