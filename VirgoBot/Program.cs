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
        BaseUrl = "https://localhost/",
        Model = "gpt-4.5",
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
        MemoryFile = memoryPath,
        ILink = new ILinkConfig
        {
            Enabled = false,
            Token = "YOUR_ILINK_TOKEN",
            WebSocketUrl = "wss://localhost/bot/v1/ws?token=YOUR_ILINK_TOKEN",
            SendUrl = "http:/localhost/bot/v1/message/send",
            WebhookPath = "/ilink/webhook",
            DefaultUserId = "ilink"
        }
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
http.DefaultRequestHeaders.Authorization =
    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);

var memoryService = new MemoryService();
var functionRegistry = new FunctionRegistry();
var stickerService = new StickerService("stickers");
var contactService = new ContactService();
var systemMemory = File.ReadAllText(Path.Combine("config", config.MemoryFile));
systemMemory = systemMemory.Replace("{{EMAIL}}", config.Email.Address);

ColorLog.Info("MEMORY", $"记忆已加载, [{systemMemory.Length}]Tokens");

var llmService = new LLMService(http, config.BaseUrl, config.Model, memoryService, functionRegistry, systemMemory);
var iLinkBridge = new ILinkBridgeService(config.ILink);

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
var emailManager = new EmailManager(emailService, bot, config.AllowedUsers[0], llmService, wsClients);
var activityMonitor = new ActivityMonitor(llmService, bot, wsClients, config.AllowedUsers[0]);

_ = Task.Run(() => emailManager.StartMonitoring());
_ = Task.Run(() => StartHttpServer());
activityMonitor.Start();
await iLinkBridge.StartAsync(HandleILinkIncomingMessageAsync, cts.Token);

ColorLog.Success("HTTP", "API: http://localhost:5000/chat");
if (config.ILink.Enabled)
{
    ColorLog.Success("ILINK", $"Bridge enabled: {config.ILink.WebhookPath}");
}

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
                            var req = JsonSerializer.Deserialize<JsonElement>(msg);

                            if (req.TryGetProperty("type", out var typeEl))
                            {
                                var type = typeEl.GetString();

                                if (type == "newMessage")
                                {
                                    var username = req.GetProperty("username").GetString();
                                    var queueLength = req.GetProperty("queueLength").GetInt32();
                                    ColorLog.Info("NEW-MSG", $"来自 {username}, 队列: {queueLength}");

                                    var prompt = $"系统提示：用户 {username} 发来了新消息，请使用 switch_douyin_chat 函数回复";
                                    ColorLog.Info("→AI", prompt);
                                    var reply = await llmService.AskAsync(username.GetHashCode(), prompt, null, null, async (targetUser) =>
                                    {
                                        var response = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "switchChat", username = targetUser }));
                                        await ws.SendAsync(new ArraySegment<byte>(response), WebSocketMessageType.Text, true, CancellationToken.None);
                                        ColorLog.Success("SWITCH", $"切换到 {targetUser}");
                                    });
                                    ColorLog.Success("AI→", reply);
                                }
                                else if (type == "message")
                                {
                                    var message = req.GetProperty("message").GetString();
                                    var userId = req.GetProperty("userId").GetString();
                                    ColorLog.Info("→AI", $"[@{userId}] {message}");

                                    activityMonitor.UpdateActivity();
                                    var reply = await llmService.AskAsync(userId.GetHashCode(), message ?? "");
                                    ColorLog.Success("AI→", reply);

                                    var response = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "sendMessage", content = reply }));
                                    await ws.SendAsync(new ArraySegment<byte>(response), WebSocketMessageType.Text, true, CancellationToken.None);
                                }
                                else if (type == "aiSwitchChat")
                                {
                                    var username = req.GetProperty("username").GetString();
                                    var response = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "switchChat", username }));
                                    await ws.SendAsync(new ArraySegment<byte>(response), WebSocketMessageType.Text, true, CancellationToken.None);
                                    ColorLog.Success("SWITCH", $"切换到 {username}");
                                }
                            }
                            else if (req.TryGetProperty("message", out var msgEl))
                            {
                                var chatReq = JsonSerializer.Deserialize<ChatRequest>(msg);
                                ColorLog.Info("MSG-WS", $"[@{chatReq?.userId ?? ""}] '{chatReq?.message ?? ""}'");

                                activityMonitor.UpdateActivity();
                                var reply = await llmService.AskAsync(config.AllowedUsers[0], chatReq?.message ?? "");

                                var response = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "reply", content = reply }));
                                await ws.SendAsync(new ArraySegment<byte>(response), WebSocketMessageType.Text, true, CancellationToken.None);
                            }
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

                    var reply = await llmService.AskAsync(8216081829, req?.message ?? "");
                    var response = Encoding.UTF8.GetBytes(reply);

                    ColorLog.Info($"MSG-HTTP", $"[@{req?.userId ?? ""}] '{req?.message ?? ""}'");

                    ctx.Response.ContentType = "text/plain; charset=utf-8";
                    ctx.Response.ContentLength64 = response.Length;
                    await ctx.Response.OutputStream.WriteAsync(response);
                }
                else if (config.ILink.Enabled &&
                         ctx.Request.Url?.AbsolutePath == config.ILink.WebhookPath &&
                         ctx.Request.HttpMethod == "POST")
                {
                    using var reader = new StreamReader(ctx.Request.InputStream);
                    var body = await reader.ReadToEndAsync();

                    if (iLinkBridge.TryParseIncoming(body, out var incoming) && incoming is not null)
                    {
                        await HandleILinkIncomingMessageAsync(incoming);
                    }

                    ctx.Response.StatusCode = 200;
                    var ok = Encoding.UTF8.GetBytes("ok");
                    await ctx.Response.OutputStream.WriteAsync(ok);
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

    activityMonitor.UpdateActivity();
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

    var reply = await llmService.AskAsync(msg.From.Id, msg.Text, async (text) =>
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

async Task HandleILinkIncomingMessageAsync(ILinkIncomingMessage incoming)
{
    var content = incoming.Content?.Trim();
    if (string.IsNullOrWhiteSpace(content))
    {
        return;
    }

    ColorLog.Info("ILINK-IN", $"[@{incoming.Username}] {content}");

    activityMonitor.UpdateActivity();
    var reply = await llmService.AskAsync(incoming.UserId.GetHashCode(), content);

    if (string.IsNullOrWhiteSpace(reply))
    {
        return;
    }

    await iLinkBridge.SendLongMessageAsync(reply, cts.Token);
    ColorLog.Success("ILINK-OUT", reply);
}

record ChatRequest(string? message, string? userId);
