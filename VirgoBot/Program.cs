using VirgoBot.Channels;
using VirgoBot.Configuration;
using VirgoBot.Services;
using VirgoBot.Utilities;

// Create log service first so all logs are captured
var logService = new LogService();
ColorLog.SetLogService(logService);

// Ensure memorys directory exists
Directory.CreateDirectory("memorys");

// Load config to determine current session
var config = ConfigLoader.Load();
var sessionDbName = config.CurrentSession;

// If no session configured or file doesn't exist, create a new one
if (string.IsNullOrWhiteSpace(sessionDbName) || !File.Exists(Path.Combine("memorys", sessionDbName)))
{
    sessionDbName = $"{Guid.NewGuid()}.db";
    config.CurrentSession = sessionDbName;
    ConfigLoader.Save(config);
    ColorLog.Info("SESSION", $"已创建新会话: {sessionDbName}");
}

// Create persistent services (process lifetime)
var wsManager = new WebSocketClientManager();
var memoryService = new MemoryService(dbFileName: sessionDbName, messageLimit: 50);

ColorLog.Info("SESSION", $"当前会话: {memoryService.CurrentDbName}");

// Create and start Gateway (manages all restartable services)
var gateway = new Gateway(logService, wsManager, memoryService);
await gateway.StartAsync();

// HttpServerHost needs Gateway reference to access runtime services
var httpServer = new HttpServerHost(gateway, wsManager, memoryService, logService);
_ = Task.Run(() => httpServer.StartAsync());

ColorLog.Success("GATEWAY", $"网关已启动 — API: {gateway.Config.Server.ListenUrl}");

Console.ReadLine();
await gateway.StopAsync();
gateway.Dispose();
