using System.Security.Cryptography;
using System.Text;
using Spectre.Console;
using VirgoBot.Channels;
using VirgoBot.Configuration;
using VirgoBot.Services;
using VirgoBot.Utilities;

var logService = new LogService();
ColorLog.SetLogService(logService);

Directory.CreateDirectory("memorys");

var config = ConfigLoader.Load();

// First-run auth setup: if JwtSecret is empty, prompt for username/password
if (string.IsNullOrWhiteSpace(config.Auth.JwtSecret))
{
    AnsiConsole.MarkupLine("[bold yellow]首次运行，请设置管理员账户[/]");

    var username = AnsiConsole.Ask<string>("[green]用户名:[/]");
    var password = AnsiConsole.Prompt(
        new TextPrompt<string>("[green]密码:[/]").Secret());
    var passwordConfirm = AnsiConsole.Prompt(
        new TextPrompt<string>("[green]确认密码:[/]").Secret());

    if (password != passwordConfirm)
    {
        AnsiConsole.MarkupLine("[bold red]两次密码不一致，程序退出[/]");
        return;
    }

    config.Auth.UsernameHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(username)));
    config.Auth.PasswordHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
    config.Auth.JwtSecret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    ConfigLoader.Save(config);
    AnsiConsole.MarkupLine("[bold green]管理员账户已设置[/]");
}

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
