using System.Net.Http.Headers;
using Telegram.Bot;
using VirgoBot.Channels;
using VirgoBot.Configuration;
using VirgoBot.Features.Email;
using VirgoBot.Functions;
using VirgoBot.Integrations.ILink;
using VirgoBot.Utilities;

namespace VirgoBot.Services;

/// <summary>
/// Holds all service instances. Created/rebuilt on each gateway start/restart.
/// </summary>
public class ServiceContainer : IDisposable
{
    public Config Config { get; set; } = null!;
    public LLMService LlmService { get; set; } = null!;
    public FunctionRegistry FunctionRegistry { get; set; } = null!;
    public ContactService ContactService { get; set; } = null!;
    public ScheduledTaskService ScheduledTaskService { get; set; } = null!;
    public TokenStatsService TokenStatsService { get; }
    public McpClientService? McpClientService { get; set; }
    public EmailService? EmailService { get; set; }
    public EmailManager? EmailManager { get; set; }
    public ActivityMonitor? ActivityMonitor { get; set; }
    public TelegramBotClient? Bot { get; set; }
    public TelegramBotHandler? TelegramHandler { get; set; }
    public ILinkBridgeService? ILinkBridge { get; set; }
    public ILinkMessageHandler? ILinkHandler { get; set; }
    public CancellationTokenSource? Cts { get; set; }

    public ServiceContainer(TokenStatsService tokenStatsService)
    {
        TokenStatsService = tokenStatsService;
    }

    public static ServiceContainer Build(
        MemoryService memoryService,
        WebSocketClientManager wsManager,
        ShellSessionService shellSessionService,
        HttpClient httpClient,
        TokenStatsService tokenStatsService)
    {
        var container = new ServiceContainer(tokenStatsService);
        container.Config = ConfigLoader.Load();
        var systemMemory = ConfigLoader.LoadSystemMemory(container.Config, memoryService);

        memoryService.UpdateMessageLimit(container.Config.Server.MessageLimit);

        var provider = ConfigLoader.GetCurrentProvider(container.Config);
        var apiKey = provider?.ApiKey ?? "";
        var baseUrl = provider?.BaseUrl ?? "";
        var model = provider?.CurrentModel ?? "";

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        container.ScheduledTaskService = new ScheduledTaskService();
        container.FunctionRegistry = new FunctionRegistry(container.Config, memoryService, container.ScheduledTaskService);

        // MCP — connect in background to avoid blocking startup
        try
        {
            var mcpConfigs = McpConfigLoader.Load();
            if (mcpConfigs.Any(c => c.Enabled))
            {
                container.McpClientService = new McpClientService();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await container.McpClientService.ConnectAllAsync(mcpConfigs);
                        container.FunctionRegistry.SetMcpService(container.McpClientService);
                    }
                    catch (Exception ex)
                    {
                        ColorLog.Error("MCP", $"MCP 后台连接失败: {ex.Message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            ColorLog.Error("MCP", $"MCP 初始化失败: {ex.Message}");
        }

        container.ContactService = new ContactService();
        container.LlmService = new LLMService(httpClient, baseUrl, model, memoryService, container.FunctionRegistry, systemMemory, container.Config.Server.MaxTokens, tokenStatsService);
        container.ScheduledTaskService.SetLlmService(container.LlmService);
        container.LlmService.SetScheduledTaskService(container.ScheduledTaskService);

        if (container.Config.Channel.Email.Enabled)
        {
            container.EmailService = new EmailService(
                container.Config.Channel.Email.ImapHost, container.Config.Channel.Email.ImapPort,
                container.Config.Channel.Email.SmtpHost, container.Config.Channel.Email.SmtpPort,
                container.Config.Channel.Email.Address, container.Config.Channel.Email.Password);
            container.FunctionRegistry.SetEmailService(container.EmailService);
        }

        container.FunctionRegistry.SetShellSessionService(shellSessionService);
        container.FunctionRegistry.SetContactService(container.ContactService);

        if (container.Config.Channel.ILink.Enabled)
        {
            container.ILinkBridge = new ILinkBridgeService(container.Config.Channel.ILink.Token, container.Config.Server.MessageSplitDelimiters);
            container.FunctionRegistry.SetILinkBridgeService(container.ILinkBridge);
        }

        container.Cts = new CancellationTokenSource();
        var ct = container.Cts.Token;

        if (container.Config.Channel.Telegram.Enabled)
        {
            container.Bot = new TelegramBotClient(container.Config.Channel.Telegram.BotToken, cancellationToken: ct);
            var messageHelper = new MessageHelper(container.Bot, container.Config.Server.MessageSplitDelimiters);
            container.ActivityMonitor = new ActivityMonitor(container.LlmService, container.Bot, wsManager, container.Config.Channel.Telegram.AllowedUsers[0], container.Config);

            var emailNotificationDispatcher = new EmailNotificationDispatcher(
                container.Config.Channel.Email.Notification,
                wsManager, container.Bot, container.Config.Channel.Telegram.AllowedUsers[0], container.ILinkBridge);

            if (container.EmailService != null)
                container.EmailManager = new EmailManager(container.EmailService, emailNotificationDispatcher, container.LlmService);

            container.TelegramHandler = new TelegramBotHandler(container.Config, container.Bot, container.LlmService, memoryService, container.FunctionRegistry, messageHelper, container.EmailManager, container.ActivityMonitor, container.ILinkBridge, ct);
        }
        else
        {
            var emailNotificationDispatcher = new EmailNotificationDispatcher(
                container.Config.Channel.Email.Notification,
                wsManager, null, 0, container.ILinkBridge);

            if (container.EmailService != null)
                container.EmailManager = new EmailManager(container.EmailService, emailNotificationDispatcher, container.LlmService);
        }

        if (container.Config.Channel.ILink.Enabled && container.ILinkBridge != null)
        {
            container.ILinkHandler = new ILinkMessageHandler(container.Config, container.LlmService, memoryService, container.ILinkBridge, ct);
        }

        return container;
    }

    public void Dispose()
    {
        ILinkBridge?.Dispose();
        McpClientService?.Dispose();
    }
}
