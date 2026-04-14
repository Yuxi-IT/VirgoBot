using System.Net.Http.Headers;
using Telegram.Bot;
using VirgoBot.Channels;
using VirgoBot.Configuration;
using VirgoBot.Features.Email;
using VirgoBot.Functions;
using VirgoBot.Integrations.ILink;
using VirgoBot.Utilities;

namespace VirgoBot.Services;

public class Gateway
{
    // Persistent dependencies (process lifetime)
    private readonly LogService _logService;
    private readonly WebSocketClientManager _wsManager;
    private readonly MemoryService _memoryService;

    // Restartable services (managed by Gateway)
    private CancellationTokenSource? _cts;

    public Config Config { get; private set; } = null!;
    public LLMService LlmService { get; private set; } = null!;
    public FunctionRegistry FunctionRegistry { get; private set; } = null!;
    public EmailService? EmailService { get; private set; }
    public EmailManager? EmailManager { get; private set; }
    public ActivityMonitor? ActivityMonitor { get; private set; }
    public TelegramBotClient? Bot { get; private set; }
    public TelegramBotHandler? TelegramHandler { get; private set; }
    public ILinkBridgeService? ILinkBridge { get; private set; }
    public StickerService StickerService { get; private set; } = null!;
    public ContactService ContactService { get; private set; } = null!;

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
        BuildServices();
        await StartChannelsAsync();
        ColorLog.Success("GATEWAY", "网关服务已启动");
    }

    public async Task StopAsync()
    {
        await StopChannelsAsync();
        ColorLog.Info("GATEWAY", "网关服务已停止");
    }

    public async Task RestartAsync()
    {
        ColorLog.Info("GATEWAY", "正在重启所有服务...");
        await StopChannelsAsync();
        BuildServices();
        await StartChannelsAsync();
        ColorLog.Success("GATEWAY", "所有服务已重启");
    }

    private void BuildServices()
    {
        Config = ConfigLoader.Load();
        var systemMemory = ConfigLoader.LoadSystemMemory(Config);

        // Update memory service limit from config
        _memoryService.UpdateMessageLimit(Config.Server.MessageLimit);

        var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Config.ApiKey);

        FunctionRegistry = new FunctionRegistry(Config);
        StickerService = new StickerService("stickers");
        ContactService = new ContactService();
        LlmService = new LLMService(http, Config.BaseUrl, Config.Model, _memoryService, FunctionRegistry, systemMemory, Config.Server.MaxTokens);
        ILinkBridge = new ILinkBridgeService(Config.ILink);

        EmailService = new EmailService(
            Config.Email.ImapHost, Config.Email.ImapPort,
            Config.Email.SmtpHost, Config.Email.SmtpPort,
            Config.Email.Address, Config.Email.Password);

        FunctionRegistry.SetEmailService(EmailService);
        FunctionRegistry.SetStickerService(StickerService);
        FunctionRegistry.SetContactService(ContactService);
        FunctionRegistry.SetILinkBridgeService(ILinkBridge);

        _cts = new CancellationTokenSource();
        Bot = new TelegramBotClient(Config.BotToken, cancellationToken: _cts.Token);
        var messageHelper = new MessageHelper(Bot);
        var emailNotificationDispatcher = new EmailNotificationDispatcher(Bot, Config.AllowedUsers[0], _wsManager, ILinkBridge);
        EmailManager = new EmailManager(EmailService, emailNotificationDispatcher, Config.AllowedUsers[0], LlmService);
        ActivityMonitor = new ActivityMonitor(LlmService, Bot, _wsManager, Config.AllowedUsers[0]);
        TelegramHandler = new TelegramBotHandler(Config, Bot, LlmService, _memoryService, FunctionRegistry, messageHelper, EmailManager, ActivityMonitor, ILinkBridge, _cts.Token);

        // Initialize channel statuses
        ChannelStatuses["telegram"] = new ChannelStatus { Name = "telegram", Enabled = true, Status = "stopped" };
        ChannelStatuses["http"] = new ChannelStatus { Name = "http", Enabled = true, Status = "running" };
        ChannelStatuses["webSocket"] = new ChannelStatus { Name = "webSocket", Enabled = true, Status = "running" };
        ChannelStatuses["email"] = new ChannelStatus { Name = "email", Enabled = true, Status = "stopped" };
        ChannelStatuses["iLink"] = new ChannelStatus { Name = "iLink", Enabled = Config.ILink.Enabled, Status = "stopped" };
    }

    private async Task StartChannelsAsync()
    {
        var ct = _cts!.Token;

        // Initialize email service
        try
        {
            await EmailService!.InitializeAsync();
        }
        catch (Exception ex)
        {
            ColorLog.Error("GATEWAY", $"邮件服务初始化失败: {ex.Message}");
        }

        // Start email monitoring
        _ = Task.Run(() => EmailManager!.StartMonitoring(ct), ct);
        ChannelStatuses["email"].Status = "monitoring";

        // Start activity monitor
        ActivityMonitor!.Start(ct);
        ChannelStatuses["telegram"].Status = "running";

        // Start iLink
        if (Config.ILink.Enabled && TelegramHandler != null)
        {
            await ILinkBridge!.StartAsync(TelegramHandler.HandleILinkIncomingMessageAsync, ct);
            ChannelStatuses["iLink"].Status = "running";
        }
        else
        {
            ChannelStatuses["iLink"].Status = Config.ILink.Enabled ? "stopped" : "disabled";
        }

        // Register Telegram handler
        TelegramHandler!.Register();

        try
        {
            var me = await Bot!.GetMe();
            ColorLog.Success("BOT", $"@{me.Username} running");
        }
        catch (Exception ex)
        {
            ColorLog.Error("ERR", ex.Message);
        }

        IsRunning = true;
        ColorLog.Success("GATEWAY", "所有频道已启动");
    }

    private async Task StopChannelsAsync()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            // Give services a moment to process cancellation
            await Task.Delay(500);
            _cts.Dispose();
            _cts = null;
        }

        IsRunning = false;

        // Update channel statuses
        foreach (var status in ChannelStatuses.Values)
        {
            status.Status = "stopped";
        }

        ColorLog.Info("GATEWAY", "所有频道已停止");
    }
}

public class ChannelStatus
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; }
    public string Status { get; set; } = "stopped";  // "running" | "stopped" | "monitoring" | "disabled"
}
