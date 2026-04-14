using System.Text;
using System.Text.Json;
using Telegram.Bot;
using VirgoBot.Utilities;

namespace VirgoBot.Services;

public class ActivityMonitor
{
    private readonly LLMService _llmService;
    private readonly TelegramBotClient _bot;
    private readonly WebSocketClientManager _wsManager;
    private readonly long _userId;
    private DateTime _lastActivity = DateTime.Now;
    private Timer? _proactiveTimer;
    private readonly Random _random = new();

    public ActivityMonitor(LLMService llmService, TelegramBotClient bot, WebSocketClientManager wsManager, long userId)
    {
        _llmService = llmService;
        _bot = bot;
        _wsManager = wsManager;
        _userId = userId;
    }

    public void UpdateActivity()
    {
        _lastActivity = DateTime.Now;
        _proactiveTimer?.Dispose();
        _proactiveTimer = null;
    }

    public void Start(CancellationToken ct = default)
    {
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(TimeSpan.FromMinutes(1), ct); }
                catch (OperationCanceledException) { break; }

                var idle = DateTime.Now - _lastActivity;
                if (idle.TotalMinutes >= 30 && _proactiveTimer == null)
                {
                    var delay = _random.Next(30, 120);
                    ColorLog.Info("ACTIVITY", $"用户空闲 {idle.TotalMinutes:F0}分钟，将在 {delay}分钟后主动发消息");

                    _proactiveTimer = new Timer(async _ =>
                    {
                        try
                        {
                            var totalIdle = DateTime.Now - _lastActivity;
                            var prompt = $"服务提示：用户已经{totalIdle.TotalMinutes:F0}分钟没有给您发消息了";

                            var reply = await _llmService.AskAsync(_userId, prompt);

                            try
                            {
                                foreach (var line in reply.Split("\n\n"))
                                {
                                    await _bot.SendMessage(_userId, line);
                                }
                            }
                            catch (Exception ex) { ColorLog.Error("TG", $"发送失败: {ex.Message}"); }

                            var msg = JsonSerializer.Serialize(new { type = "proactive", content = reply });
                            await _wsManager.BroadcastAsync(msg);

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
