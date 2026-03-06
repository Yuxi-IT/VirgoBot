using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using VirgoBot.Helpers;

var config = JsonSerializer.Deserialize<Config>(File.ReadAllText("config.json"))!;

var http = new HttpClient();
http.DefaultRequestHeaders.Add("x-api-key", config.ApiKey);
http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

var memoryService = new MemoryService();
var functionRegistry = new FunctionRegistry();
var systemMemory = File.Exists(config.MemoryFile) ? File.ReadAllText(config.MemoryFile) : "";

Console.WriteLine($"[MEMORY] 记忆已加载,[{systemMemory.Length}]Tokens");

var claudeService = new ClaudeService(http, config.BaseUrl, config.Model, memoryService, functionRegistry, systemMemory);

using var cts = new CancellationTokenSource();
var bot = new TelegramBotClient(config.BotToken, cancellationToken: cts.Token);
var messageHelper = new MessageHelper(bot);

var emailService = new EmailService(
    config.Email.ImapHost, config.Email.ImapPort,
    config.Email.SmtpHost, config.Email.SmtpPort,
    config.Email.Address, config.Email.Password);

await emailService.InitializeAsync();
var emailManager = new EmailManager(emailService, bot, config.AllowedUsers[0], claudeService);

var me = await bot.GetMe();
bot.OnMessage += async (msg, type) => await OnMessage(msg);
bot.OnUpdate += async (query) => await OnUpdate(query);

_ = Task.Run(() => emailManager.StartMonitoring());

Console.WriteLine($"@{me.Username} running");
Console.ReadLine();

async Task OnMessage(Message msg)
{
    if (msg.Text is null || !config.AllowedUsers.Contains(msg.From!.Id)) return;

    Console.WriteLine($"[MSG-{msg.Type}] [@{msg.From.Username}({msg.From.Id})] '{msg.Text}'");

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

    var reply = await claudeService.AskAsync(msg.From.Id, msg.Text);
    reply = messageHelper.ProcessThinkTags(reply);

    await messageHelper.SendLongMessage(msg.Chat.Id, reply);
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
