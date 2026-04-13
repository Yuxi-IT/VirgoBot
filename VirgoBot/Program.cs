using Telegram.Bot;
using VirgoBot.Channels;
using VirgoBot.Configuration;
using VirgoBot.Features.Email;
using VirgoBot.Functions;
using VirgoBot.Integrations.ILink;
using VirgoBot.Services;
using VirgoBot.Utilities;

// Create log service first so all logs are captured
var logService = new LogService();
ColorLog.SetLogService(logService);

// Load configuration
var config = ConfigLoader.Load();
var systemMemory = ConfigLoader.LoadSystemMemory(config);

// Create core services
var wsManager = new WebSocketClientManager();
var http = new HttpClient();
http.DefaultRequestHeaders.Authorization =
    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);

var memoryService = new MemoryService(messageLimit: config.Server.MessageLimit);
var functionRegistry = new FunctionRegistry(config);
var stickerService = new StickerService("stickers");
var contactService = new ContactService();
var llmService = new LLMService(http, config.BaseUrl, config.Model, memoryService, functionRegistry, systemMemory, config.Server.MaxTokens);
var iLinkBridge = new ILinkBridgeService(config.ILink);

// Initialize Telegram bot
using var cts = new CancellationTokenSource();
var bot = new TelegramBotClient(config.BotToken, cancellationToken: cts.Token);
var messageHelper = new MessageHelper(bot);

// Initialize email
var emailService = new EmailService(
    config.Email.ImapHost, config.Email.ImapPort,
    config.Email.SmtpHost, config.Email.SmtpPort,
    config.Email.Address, config.Email.Password);
await emailService.InitializeAsync();
functionRegistry.SetEmailService(emailService);
functionRegistry.SetStickerService(stickerService);
functionRegistry.SetContactService(contactService);

// Create channel handlers
var emailNotificationDispatcher = new EmailNotificationDispatcher(bot, config.AllowedUsers[0], wsManager, iLinkBridge);
var emailManager = new EmailManager(emailService, emailNotificationDispatcher, config.AllowedUsers[0], llmService);
var activityMonitor = new ActivityMonitor(llmService, bot, wsManager, config.AllowedUsers[0]);
var telegramHandler = new TelegramBotHandler(config, bot, llmService, memoryService, functionRegistry, messageHelper, emailManager, activityMonitor, iLinkBridge, cts.Token);
var httpServer = new HttpServerHost(config, llmService, wsManager, activityMonitor, iLinkBridge, telegramHandler.HandleILinkIncomingMessageAsync, memoryService, contactService, logService);

// Start all channels
_ = Task.Run(() => emailManager.StartMonitoring());
_ = Task.Run(() => httpServer.StartAsync());
activityMonitor.Start();
await iLinkBridge.StartAsync(telegramHandler.HandleILinkIncomingMessageAsync, cts.Token);

ColorLog.Success("HTTP", $"API: {config.Server.ListenUrl}chat");

try
{
    var me = await bot.GetMe();
    ColorLog.Success("BOT", $"@{me.Username} running");
}
catch (Exception ex)
{
    ColorLog.Error("ERR", ex.Message);
}

telegramHandler.Register();
Console.ReadLine();
