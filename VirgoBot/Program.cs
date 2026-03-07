using System.Text.Json;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using VirgoBot.Helpers;

var configDir = "config";
var configPath = Path.Combine(configDir, "config.json");
var memoryPath = Path.Combine(configDir, "system_memory.md");

Directory.CreateDirectory(configDir);

if (!File.Exists(configPath))
{
    var defaultConfig = new Config
    {
        BotToken = "YOUR_BOT_TOKEN",
        ApiKey = "YOUR_API_KEY",
        BaseUrl = "https://api.anthropic.com/v1/messages",
        Model = "claude-haiku-4-5-20251001",
        AllowedUsers = Array.Empty<long>(),
        Email = new EmailConfig
        {
            ImapHost = "imap.example.com",
            ImapPort = 993,
            SmtpHost = "smtp.example.com",
            SmtpPort = 587,
            Address = "your@email.com",
            Password = "your_password"
        },
        MemoryFile = memoryPath
    };
    File.WriteAllText(configPath, JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));
    ColorLog.Info("CONFIG", $"已创建默认配置文件: {configPath}");
}

if (!File.Exists(memoryPath))
{
    File.WriteAllText(memoryPath, "# 系统记忆\n\n你是 Virgo，一个智能助手。\n\n## 能力\n- 邮件收发管理\n- 文件读写操作\n- Shell命令执行\n- 网页浏览\n- 通讯录管理\n");
    ColorLog.Info("MEMORY", $"已创建默认记忆文件: {memoryPath}");
}

var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath))!;

var wsClients = new List<WebSocket>();

var http = new HttpClient();
http.DefaultRequestHeaders.Add("x-api-key", config.ApiKey);
http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

var memoryService = new MemoryService();
var functionRegistry = new FunctionRegistry();
var stickerService = new StickerService("stickers");
var contactService = new ContactService();
var systemMemory = File.ReadAllText(Path.Combine("config", config.MemoryFile));
systemMemory = systemMemory.Replace("{{EMAIL}}", config.Email.Address);

ColorLog.Info("MEMORY", $"记忆已加载, [{systemMemory.Length}]Tokens");

var claudeService = new ClaudeService(http, config.BaseUrl, config.Model, memoryService, functionRegistry, systemMemory);

using var cts = new CancellationTokenSource();
var bot = new TelegramBotClient(config.BotToken, cancellationToken: cts.Token);
var messageHelper = new MessageHelper(bot);

var emailService = new EmailService(
    config.Email.ImapHost, config.Email.ImapPort,
    config.Email.SmtpHost, config.Email.SmtpPort,
    config.Email.Address, config.Email.Password);

await emailService.InitializeAsync();
functionRegistry.SetEmailService(emailService);
functionRegistry.SetPlaywrightService(new PlaywrightService());
functionRegistry.SetStickerService(stickerService);
functionRegistry.SetContactService(contactService);
var emailManager = new EmailManager(emailService, bot, config.AllowedUsers[0], claudeService, wsClients);

_ = Task.Run(() => emailManager.StartMonitoring());
_ = Task.Run(() => StartHttpServer());

ColorLog.Success("HTTP", "API: http://localhost:5000/chat");

try
{
    var me = await bot.GetMe();
    ColorLog.Success("BOT", $"@{me.Username} running");

} catch (Exception ex)
{
    ColorLog.Error("ERR", ex.Message);
}
bot.OnMessage += async (msg, type) => await OnMessage(msg);
bot.OnUpdate += async (query) => await OnUpdate(query);

Console.ReadLine();

async Task StartHttpServer()
{
    var listener = new HttpListener();
    listener.Prefixes.Add("http://localhost:5000/");
    listener.Start();

    while (true)
    {
        var ctx = await listener.GetContextAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                if (ctx.Request.IsWebSocketRequest)
                {
                    var wsCtx = await ctx.AcceptWebSocketAsync(null);
                    var ws = wsCtx.WebSocket;
                    wsClients.Add(ws);
                    ColorLog.Success("WS", "客户端已连接");

                    var buffer = new byte[1024 * 4];
                    while (ws.State == WebSocketState.Open)
                    {
                        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                            wsClients.Remove(ws);
                            ColorLog.Info("WS", "客户端已断开");
                        }
                        else if (result.MessageType == WebSocketMessageType.Text)
                        {
                            var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            var req = JsonSerializer.Deserialize<ChatRequest>(msg);
                            ColorLog.Info("MSG-WS", $"[@{req?.userId ?? ""}] '{req?.message ?? ""}'");

                            var reply = await claudeService.AskAsync(8216081829, req?.message ?? "");
                            var response = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "reply", content = reply }));
                            await ws.SendAsync(new ArraySegment<byte>(response), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }
                    return;
                }

                ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                ctx.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                ctx.Response.Headers.Add("Access-Control-Allow-Headers", "*");

                if (ctx.Request.HttpMethod == "OPTIONS")
                {
                    ctx.Response.StatusCode = 200;
                }
                else if (ctx.Request.Url?.AbsolutePath == "/chat" && ctx.Request.HttpMethod == "POST")
                {
                    using var reader = new StreamReader(ctx.Request.InputStream);
                    var body = await reader.ReadToEndAsync();
                    var req = JsonSerializer.Deserialize<ChatRequest>(body);

                    var reply = await claudeService.AskAsync(8216081829, req?.message ?? "");
                    var response = Encoding.UTF8.GetBytes(reply);

                    ColorLog.Info($"MSG-HTTP", $"[@{req?.userId ?? ""}] '{req?.message ?? ""}'");

                    ctx.Response.ContentType = "text/plain; charset=utf-8";
                    ctx.Response.ContentLength64 = response.Length;
                    await ctx.Response.OutputStream.WriteAsync(response);
                }
                else if (ctx.Request.Url?.AbsolutePath.StartsWith("/sticker/") == true && ctx.Request.HttpMethod == "GET")
                {
                    var filename = ctx.Request.Url.AbsolutePath.Replace("/sticker/", "");
                    var path = Path.Combine("stickers", filename);

                    if (File.Exists(path))
                    {
                        var ext = Path.GetExtension(filename).ToLower();
                        ctx.Response.ContentType = ext == ".gif" ? "image/gif" : "image/png";
                        var data = await File.ReadAllBytesAsync(path);
                        await ctx.Response.OutputStream.WriteAsync(data);
                    }
                    else ctx.Response.StatusCode = 404;
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                }
            }
            catch { ctx.Response.StatusCode = 500; }
            finally { ctx.Response.Close(); }
        });
    }
}

async Task OnMessage(Message msg)
{
    if (msg.Text is null || !config.AllowedUsers.Contains(msg.From!.Id)) return;

    ColorLog.Info($"MSG-{msg.Type}", $"[@{msg.From.Username}({msg.From.Id})] '{msg.Text}'");

    if (msg.Text == "/clear")
    {
        memoryService.ClearAllMessages(msg.From.Id);
        await bot.SendMessage(msg.Chat.Id, "✅ 聊天记录已清空");
        return;
    }

    if (msg.ReplyToMessage?.Text?.Contains("📧") == true)
    {
        var lines = msg.ReplyToMessage.Text.Split('\n');
        var uidLine = lines.FirstOrDefault(l => l.Contains("UID:"));
        if (uidLine != null)
        {
            var uid = uidLine.Split(':')[1].Trim();
            await emailManager.HandleReply(uid, msg.Text);
            await bot.SendMessage(msg.Chat.Id, "✅ 邮件发送咯~");
            await bot.DeleteMessage(msg.Chat.Id, msg.ReplyToMessage.MessageId);
            return;
        }
    }

    await bot.SendChatAction(msg.Chat.Id, ChatAction.Typing);

    functionRegistry.SetTelegramBot(bot, msg.Chat.Id);

    var reply = await claudeService.AskAsync(msg.From.Id, msg.Text, async (text) =>
    {
        var processed = messageHelper.ProcessThinkTags(text);
        await messageHelper.SendLongMessage(msg.Chat.Id, processed);
    }, async (stickerPath) =>
    {
        ColorLog.Debug("STICKER", stickerPath);
        if (File.Exists(stickerPath))
        {
            using var stream = File.OpenRead(stickerPath);
            await bot.SendSticker(msg.Chat.Id, InputFile.FromStream(stream));
        }
    });

    if (!string.IsNullOrEmpty(reply))
    {
        reply = messageHelper.ProcessThinkTags(reply);
        await messageHelper.SendLongMessage(msg.Chat.Id, reply);
    }
}

async Task OnUpdate(Update update)
{
    if (update is { CallbackQuery: { } query })
    {
        if (!config.AllowedUsers.Contains(query.From.Id) || query.Data is null) return;

        if (query.Data.StartsWith("ignore_"))
        {
            var uid = query.Data.Replace("ignore_", "");
            emailManager.IgnoreEmail(uid);
            await bot.DeleteMessage(query.Message!.Chat.Id, query.Message.MessageId);
            await bot.AnswerCallbackQuery(query.Id, "已忽略");
        }
    }
}

record ChatRequest(string? message, string? userId);
