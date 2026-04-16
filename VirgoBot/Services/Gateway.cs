using System.Net.Http.Headers;
using Telegram.Bot;
using VirgoBot.Channels;
using VirgoBot.Configuration;
using VirgoBot.Features.Email;
using VirgoBot.Functions;
using VirgoBot.Integrations.ILink;
using VirgoBot.Utilities;
using VirgoBot.Services;

namespace VirgoBot.Services;

public class Gateway : IDisposable
{
    // Persistent dependencies (process lifetime)
    private readonly LogService _logService;
    private readonly WebSocketClientManager _wsManager;
    private readonly MemoryService _memoryService;
    private readonly ShellSessionService _shellSessionService = new();

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

    public async Task SwitchSession(string dbFileName)
    {
        ColorLog.Info("SESSION", $"正在切换会话: {dbFileName}");
        _memoryService.SwitchDatabase(dbFileName);
        SoulFunctions.ClearCache();

        // Update config
        Config.CurrentSession = dbFileName;
        ConfigLoader.Save(Config);

        // Rebuild services to pick up new soul content
        await StopChannelsAsync();
        BuildServices();
        await StartChannelsAsync();
        ColorLog.Success("SESSION", $"会话已切换: {dbFileName}");
    }

    private void BuildServices()
    {
        Config = ConfigLoader.Load();
        var systemMemory = ConfigLoader.LoadSystemMemory(Config, _memoryService);

        // Update memory service limit from config
        _memoryService.UpdateMessageLimit(Config.Server.MessageLimit);

        var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Config.ApiKey);

        FunctionRegistry = new FunctionRegistry(Config, _memoryService);
        StickerService = new StickerService("stickers");
        ContactService = new ContactService();
        LlmService = new LLMService(http, Config.BaseUrl, Config.Model, _memoryService, FunctionRegistry, systemMemory, Config.Server.MaxTokens);

        // Email services - only if enabled
        if (Config.Channel.Email.Enabled)
        {
            EmailService = new EmailService(
                Config.Channel.Email.ImapHost, Config.Channel.Email.ImapPort,
                Config.Channel.Email.SmtpHost, Config.Channel.Email.SmtpPort,
                Config.Channel.Email.Address, Config.Channel.Email.Password);
            FunctionRegistry.SetEmailService(EmailService);
        }

        FunctionRegistry.SetShellSessionService(_shellSessionService);
        FunctionRegistry.SetStickerService(StickerService);
        FunctionRegistry.SetContactService(ContactService);

        // ILink services - only if enabled
        if (Config.Channel.ILink.Enabled)
        {
            ILinkBridge = new ILinkBridgeService(Config.Channel.ILink);
            FunctionRegistry.SetILinkBridgeService(ILinkBridge);
        }

        _cts = new CancellationTokenSource();

        // Telegram services - only if enabled
        if (Config.Channel.Telegram.Enabled)
        {
            Bot = new TelegramBotClient(Config.Channel.Telegram.BotToken, cancellationToken: _cts.Token);
            var messageHelper = new MessageHelper(Bot);
            var emailNotificationDispatcher = new EmailNotificationDispatcher(Bot, Config.Channel.Telegram.AllowedUsers[0], _wsManager, ILinkBridge);
            if (EmailService != null)
            {
                EmailManager = new EmailManager(EmailService, emailNotificationDispatcher, Config.Channel.Telegram.AllowedUsers[0], LlmService);
            }
            ActivityMonitor = new ActivityMonitor(LlmService, Bot, _wsManager, Config.Channel.Telegram.AllowedUsers[0]);
            TelegramHandler = new TelegramBotHandler(Config, Bot, LlmService, _memoryService, FunctionRegistry, messageHelper, EmailManager, ActivityMonitor, ILinkBridge, _cts.Token);
        }

        // Initialize channel statuses
        ChannelStatuses["telegram"] = new ChannelStatus { Name = "telegram", Enabled = Config.Channel.Telegram.Enabled, Status = "stopped" };
        ChannelStatuses["http"] = new ChannelStatus { Name = "http", Enabled = true, Status = "running" };
        ChannelStatuses["webSocket"] = new ChannelStatus { Name = "webSocket", Enabled = true, Status = "running" };
        ChannelStatuses["email"] = new ChannelStatus { Name = "email", Enabled = Config.Channel.Email.Enabled, Status = "stopped" };
        ChannelStatuses["iLink"] = new ChannelStatus { Name = "iLink", Enabled = Config.Channel.ILink.Enabled, Status = "stopped" };
    }

    private async Task StartChannelsAsync()
    {
        var ct = _cts!.Token;

        // Email channel - conditional start
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
            ChannelStatuses["email"].Status = "disabled";
        }

        // Telegram channel - conditional start
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

        // iLink channel - conditional start
        if (Config.Channel.ILink.Enabled && ILinkBridge != null && TelegramHandler != null)
        {
            try
            {
                await ILinkBridge.StartAsync(TelegramHandler.HandleILinkIncomingMessageAsync, ct);
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
            ChannelStatuses["iLink"].Status = Config.Channel.ILink.Enabled ? "stopped" : "disabled";
        }

        IsRunning = true;
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

    public void Dispose()
    {
        _shellSessionService.Dispose();
    }
}

public class ChannelStatus
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; }
    public string Status { get; set; } = "stopped";  // "running" | "stopped" | "monitoring" | "disabled"
}
