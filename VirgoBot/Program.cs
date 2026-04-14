using VirgoBot.Channels;
using VirgoBot.Services;
using VirgoBot.Utilities;

// Create log service first so all logs are captured
var logService = new LogService();
ColorLog.SetLogService(logService);

// Create persistent services (process lifetime)
var wsManager = new WebSocketClientManager();
var memoryService = new MemoryService(messageLimit: 50);

// Create and start Gateway (manages all restartable services)
var gateway = new Gateway(logService, wsManager, memoryService);
await gateway.StartAsync();

// HttpServerHost needs Gateway reference to access runtime services
var httpServer = new HttpServerHost(gateway, wsManager, memoryService, logService);
_ = Task.Run(() => httpServer.StartAsync());

ColorLog.Success("GATEWAY", $"网关已启动 — API: {gateway.Config.Server.ListenUrl}");

Console.ReadLine();
await gateway.StopAsync();
