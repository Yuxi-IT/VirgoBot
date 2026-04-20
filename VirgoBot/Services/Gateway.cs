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
    private readonly LogService _logService;
    private readonly WebSocketClientManager _wsManager;
    private readonly MemoryService _memoryService;
    private readonly ShellSessionService _shellSessionService = new();
    private readonly HttpClient _httpClient = new();

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
    public ILinkMessageHandler? ILinkHandler { get; private set; }
    public StickerService StickerService { get; private set; } = null!;
    public ContactService ContactService { get; private set; } = null!;
    public ScheduledTaskService ScheduledTaskService { get; private set; } = null!;

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
    }

    public async Task SwitchSession(string dbFileName)
    {
        ColorLog.Info("SESSION", $"正在切换会话: {dbFileName}");
        _memoryService.SwitchDatabase(dbFileName);
        SoulFunctions.ClearCache();

        Config.CurrentSession = dbFileName;
        ConfigLoader.Save(Config);

        await StopChannelsAsync();
        BuildServices();
        await StartChannelsAsync();
        ColorLog.Success("SESSION", $"会话已切换: {dbFileName}");
    }

    private void BuildServices()
    {
        Config = ConfigLoader.Load();
        var systemMemory = ConfigLoader.LoadSystemMemory(Config, _memoryService);

        _memoryService.UpdateMessageLimit(Config.Server.MessageLimit);

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Config.ApiKey);

        ScheduledTaskService = new ScheduledTaskService();

        FunctionRegistry = new FunctionRegistry(Config, _memoryService, ScheduledTaskService);
        StickerService = new StickerService("stickers");
        ContactService = new ContactService();
        LlmService = new LLMService(_httpClient, Config.BaseUrl, Config.Model, _memoryService, FunctionRegistry, systemMemory, Config.Server.MaxTokens);
        ScheduledTaskService.SetLlmService(LlmService);

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

        if (Config.Channel.ILink.Enabled)
        {
            ILinkBridge = new ILinkBridgeService(Config.Channel.ILink, Config.Server.MessageSplitDelimiters);
            FunctionRegistry.SetILinkBridgeService(ILinkBridge);
        }

        _cts = new CancellationTokenSource();

        if (Config.Channel.Telegram.Enabled)
        {
            Bot = new TelegramBotClient(Config.Channel.Telegram.BotToken, cancellationToken: _cts.Token);
            var messageHelper = new MessageHelper(Bot, Config.Server.MessageSplitDelimiters);
            ActivityMonitor = new ActivityMonitor(LlmService, Bot, _wsManager, Config.Channel.Telegram.AllowedUsers[0], Config);

            var emailNotificationDispatcher = new EmailNotificationDispatcher(
                Config.Channel.Email.Notification,
                _wsManager,
                Bot,
                Config.Channel.Telegram.AllowedUsers[0],
                ILinkBridge);


            if (EmailService != null)
            {
                EmailManager = new EmailManager(EmailService, emailNotificationDispatcher, Config.Channel.Telegram.AllowedUsers[0], LlmService);
            }

            TelegramHandler = new TelegramBotHandler(Config, Bot, LlmService, _memoryService, FunctionRegistry, messageHelper, EmailManager, ActivityMonitor, ILinkBridge, _cts.Token);
        }
        else
        {
            var emailNotificationDispatcher = new EmailNotificationDispatcher(
                Config.Channel.Email.Notification,
                _wsManager,
                null,
                0,
                ILinkBridge);


            if (EmailService != null)
            {
                EmailManager = new EmailManager(EmailService, emailNotificationDispatcher, 0, LlmService);
            }
        }

        if (Config.Channel.ILink.Enabled && ILinkBridge != null)
        {
            ILinkHandler = new ILinkMessageHandler(Config, LlmService, _memoryService, ILinkBridge, _cts.Token);
        }

        ChannelStatuses["telegram"] = new ChannelStatus { Name = "telegram", Enabled = Config.Channel.Telegram.Enabled, Status = "stopped" };
        ChannelStatuses["http"] = new ChannelStatus { Name = "http", Enabled = true, Status = "running" };
        ChannelStatuses["webSocket"] = new ChannelStatus { Name = "webSocket", Enabled = true, Status = "running" };
        ChannelStatuses["email"] = new ChannelStatus { Name = "email", Enabled = Config.Channel.Email.Enabled, Status = "stopped" };
        ChannelStatuses["iLink"] = new ChannelStatus { Name = "iLink", Enabled = Config.Channel.ILink.Enabled, Status = "stopped" };
    }

    private async Task StartChannelsAsync()
    {
        var ct = _cts!.Token;

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
                {
                    ColorLog.Warning("EMAIL", "邮件频道未启动: EmailService 未初始化");
                }
                else if (EmailManager == null)
                {
                    ColorLog.Warning("EMAIL", "邮件频道未启动: EmailManager 未初始化");
                }
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
                {
                    ColorLog.Warning("ILINK", "iLink 频道未启动: ILinkBridge 未初始化");
                }
                else if (ILinkHandler == null)
                {
                    ColorLog.Warning("ILINK", "iLink 频道未启动: ILinkHandler 未初始化");
                }
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
        if (_cts != null)
        {
            await _cts.CancelAsync();
            await Task.Delay(500);
            _cts.Dispose();
            _cts = null;
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
        _httpClient?.Dispose();
        ILinkBridge?.Dispose();
    }
}

public class ChannelStatus
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; }
    public string Status { get; set; } = "stopped";
}
