using VirgoBot.Channels;
using VirgoBot.Configuration;
using VirgoBot.Services;
using VirgoBot.Utilities;

var logService = new LogService();
ColorLog.SetLogService(logService);

Directory.CreateDirectory("memorys");

var config = ConfigLoader.Load();
var sessionDbName = config.CurrentSession;

if (string.IsNullOrWhiteSpace(sessionDbName) || !File.Exists(Path.Combine("memorys", sessionDbName)))
{
    sessionDbName = $"{Guid.NewGuid()}.db";
    config.CurrentSession = sessionDbName;
    ConfigLoader.Save(config);
    ColorLog.Info("SESSION", $"已创建新会话: {sessionDbName}");
}

var wsManager = new WebSocketClientManager();
var memoryService = new MemoryService(dbFileName: sessionDbName, messageLimit: 50);

ColorLog.Info("SESSION", $"当前会话: {memoryService.CurrentDbName}");

var gateway = new Gateway(logService, wsManager, memoryService);
await gateway.StartAsync();


var httpServer = new HttpServerHost(gateway, wsManager, memoryService, logService);
_ = Task.Run(() => httpServer.StartAsync());

ColorLog.Success("GATEWAY", $"网关已启动 — API: {gateway.Config.Server.ListenUrl}");

Console.ReadLine();
await gateway.StopAsync();
gateway.Dispose();
