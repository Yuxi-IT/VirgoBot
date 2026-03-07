using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Telegram.Bot;

namespace VirgoBot.Helpers;

public class ActivityMonitor
{
    private readonly ClaudeService _claudeService;
    private readonly TelegramBotClient _bot;
    private readonly List<WebSocket> _wsClients;
    private readonly long _userId;
    private DateTime _lastActivity = DateTime.Now;
    private Timer? _proactiveTimer;
    private readonly Random _random = new();

    public ActivityMonitor(ClaudeService claudeService, TelegramBotClient bot, List<WebSocket> wsClients, long userId)
    {
        _claudeService = claudeService;
        _bot = bot;
        _wsClients = wsClients;
        _userId = userId;
    }

    public void UpdateActivity()
    {
        _lastActivity = DateTime.Now;
        _proactiveTimer?.Dispose();
        _proactiveTimer = null;
    }

    public void Start()
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(1));

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

                            var reply = await _claudeService.AskAsync(_userId, prompt);

                            try
                            {
                                foreach (var line in reply.Split("\n\n"))
                                {
                                    await _bot.SendMessage(_userId, line);
                                }
                            }
                            catch (Exception ex) { ColorLog.Error("TG", $"发送失败: {ex.Message}"); }

                            var msg = JsonSerializer.Serialize(new { type = "proactive", content = reply });
                            var buffer = Encoding.UTF8.GetBytes(msg);

                            foreach (var ws in _wsClients.ToList())
                            {
                                if (ws.State == WebSocketState.Open)
                                {
                                    try { await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None); }
                                    catch { _wsClients.Remove(ws); }
                                }
                            }

                            UpdateActivity();
                        }
                        catch (Exception ex) { ColorLog.Error("ACTIVITY", $"主动消息失败: {ex.Message}"); }
                    }, null, TimeSpan.FromMinutes(delay), Timeout.InfiniteTimeSpan);
                }
            }
        });
    }
}
