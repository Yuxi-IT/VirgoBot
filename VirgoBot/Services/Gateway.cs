using VirgoBot.Channels;
using VirgoBot.Configuration;
using VirgoBot.Features.Email;
using VirgoBot.Functions;
using VirgoBot.Integrations.ILink;
using VirgoBot.Utilities;
using Telegram.Bot;

namespace VirgoBot.Services;

public class Gateway : IDisposable
{
    private readonly LogService _logService;
    private readonly WebSocketClientManager _wsManager;
    private readonly MemoryService _memoryService;
    private readonly ShellSessionService _shellSessionService = new();
    private readonly HttpClient _httpClient = new();
    private readonly TokenStatsService _tokenStatsService = new();

    private ServiceContainer? _services;

    // Public property proxies — handlers continue using gateway.Xxx
    public Config Config => _services!.Config;
    public LLMService LlmService => _services!.LlmService;
    public FunctionRegistry FunctionRegistry => _services!.FunctionRegistry;
    public EmailService? EmailService => _services?.EmailService;
    public EmailManager? EmailManager => _services?.EmailManager;
    public ActivityMonitor? ActivityMonitor => _services?.ActivityMonitor;
    public TelegramBotClient? Bot => _services?.Bot;
    public TelegramBotHandler? TelegramHandler => _services?.TelegramHandler;
    public ILinkBridgeService? ILinkBridge => _services?.ILinkBridge;
    public ILinkMessageHandler? ILinkHandler => _services?.ILinkHandler;
    public ContactService ContactService => _services!.ContactService;
    public ScheduledTaskService ScheduledTaskService => _services!.ScheduledTaskService;
    public TokenStatsService TokenStatsService => _tokenStatsService;
    public McpClientService? McpClientService => _services?.McpClientService;

    public bool IsRunning { get; private set; }
    public Dictionary<string, ChannelStatus> ChannelStatuses { get; } = new();

    public Gateway(LogService logService, WebSocketClientManager wsManager, MemoryService memoryService)
    {
        _logService = logService;
        _wsManager = wsManager;
        _memoryService = memoryService;
    }

    public async Task StartAsync()
    {
        _services = ServiceContainer.Build(_memoryService, _wsManager, _shellSessionService, _httpClient, _tokenStatsService);
        InitChannelStatuses();
        await StartChannelsAsync();
        ColorLog.Success("GATEWAY", "网关服务已启动");
    }

    public async Task StopAsync()
    {
        await StopChannelsAsync();
        _services?.Dispose();
        ColorLog.Info("GATEWAY", "网关服务已停止");
    }

    public async Task RestartAsync()
    {
        ColorLog.Info("GATEWAY", "正在重启所有服务...");
        await StopChannelsAsync();
        _services?.Dispose();
        _services = ServiceContainer.Build(_memoryService, _wsManager, _shellSessionService, _httpClient, _tokenStatsService);
        InitChannelStatuses();
        await StartChannelsAsync();
    }

    public async Task SwitchSession(string dbFileName)
    {
        ColorLog.Info("SESSION", $"正在切换会话: {dbFileName}");
        _memoryService.SwitchDatabase(dbFileName);
        SoulFunctions.ClearCache();

        var config = ConfigLoader.Load();
        config.CurrentSession = dbFileName;
        ConfigLoader.Save(config);

        await StopChannelsAsync();
        _services?.Dispose();
        _services = ServiceContainer.Build(_memoryService, _wsManager, _shellSessionService, _httpClient, _tokenStatsService);
        InitChannelStatuses();
        await StartChannelsAsync();
        ColorLog.Success("SESSION", $"会话已切换: {dbFileName}");
    }

    public ProviderConfig? GetCurrentProvider()
    {
        return ConfigLoader.GetCurrentProvider(Config);
    }

    private void InitChannelStatuses()
    {
        ChannelStatuses["telegram"] = new ChannelStatus { Name = "telegram", Enabled = Config.Channel.Telegram.Enabled, Status = "stopped" };
        ChannelStatuses["http"] = new ChannelStatus { Name = "http", Enabled = true, Status = "running" };
        ChannelStatuses["webSocket"] = new ChannelStatus { Name = "webSocket", Enabled = true, Status = "running" };
        ChannelStatuses["email"] = new ChannelStatus { Name = "email", Enabled = Config.Channel.Email.Enabled, Status = "stopped" };
        ChannelStatuses["iLink"] = new ChannelStatus { Name = "iLink", Enabled = Config.Channel.ILink.Enabled, Status = "stopped" };
    }

    private async Task StartChannelsAsync()
    {
        var ct = _services!.Cts!.Token;

        if (Config.Channel.Email.Enabled && EmailService != null && EmailManager != null)
        {
            try
            {
                await EmailService.InitializeAsync();
                _ = Task.Run(() => EmailManager.StartMonitoring(ct), ct);
                ChannelStatuses["email"].Status = "monitoring";
                ColorLog.Success("EMAIL", "邮件频道已启动");
            }
            catch (Exception ex)
            {
                ColorLog.Error("EMAIL", $"邮件服务初始化失败: {ex.Message}");
                ChannelStatuses["email"].Status = "error";
            }
        }
        else
        {
            if (Config.Channel.Email.Enabled)
            {
                if (EmailService == null)
                    ColorLog.Warning("EMAIL", "邮件频道未启动: EmailService 未初始化");
                else if (EmailManager == null)
                    ColorLog.Warning("EMAIL", "邮件频道未启动: EmailManager 未初始化");
                ChannelStatuses["email"].Status = "stopped";
            }
            else
            {
                ChannelStatuses["email"].Status = "disabled";
            }
        }

        if (Config.Channel.Telegram.Enabled && Bot != null && TelegramHandler != null && ActivityMonitor != null)
        {
            try
            {
                ActivityMonitor.Start(ct);
                TelegramHandler.Register();
                var me = await Bot.GetMe();
                ColorLog.Success("TELEGRAM", $"Bot 已连接: @{me.Username}");
                ChannelStatuses["telegram"].Status = "running";
            }
            catch (Exception ex)
            {
                ColorLog.Error("TELEGRAM", $"Telegram 服务启动失败: {ex.Message}");
                ChannelStatuses["telegram"].Status = "error";
            }
        }
        else
        {
            ChannelStatuses["telegram"].Status = "disabled";
        }

        if (Config.Channel.ILink.Enabled && ILinkBridge != null && ILinkHandler != null)
        {
            try
            {
                await ILinkBridge.StartAsync(ILinkHandler.HandleIncomingMessageAsync, ct);
                ChannelStatuses["iLink"].Status = "running";
                ColorLog.Success("ILINK", "iLink 频道已启动");
            }
            catch (Exception ex)
            {
                ColorLog.Error("ILINK", $"iLink 服务启动失败: {ex.Message}");
                ChannelStatuses["iLink"].Status = "error";
            }
        }
        else
        {
            if (Config.Channel.ILink.Enabled)
            {
                if (ILinkBridge == null)
                    ColorLog.Warning("ILINK", "iLink 频道未启动: ILinkBridge 未初始化");
                else if (ILinkHandler == null)
                    ColorLog.Warning("ILINK", "iLink 频道未启动: ILinkHandler 未初始化");
                ChannelStatuses["iLink"].Status = "stopped";
            }
            else
            {
                ChannelStatuses["iLink"].Status = "disabled";
            }
        }

        IsRunning = true;
    }

    private async Task StopChannelsAsync()
    {
        if (_services?.McpClientService != null)
        {
            try { await _services.McpClientService.DisconnectAllAsync(); } catch { }
        }

        if (_services?.Cts != null)
        {
            await _services.Cts.CancelAsync();
            await Task.Delay(500);
            _services.Cts.Dispose();
            _services.Cts = null;
        }

        IsRunning = false;

        foreach (var status in ChannelStatuses.Values)
        {
            if (status.Status == "running" || status.Status == "monitoring")
            {
                status.Status = "stopped";
                ColorLog.Info(status.Name.ToUpper(), $"{status.Name} 频道已停止");
            }
            else
            {
                status.Status = "stopped";
            }
        }
    }

    public void Dispose()
    {
        _shellSessionService.Dispose();
        _httpClient.Dispose();
        _services?.Dispose();
    }
}

public class ChannelStatus
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; }
    public string Status { get; set; } = "stopped";
}
