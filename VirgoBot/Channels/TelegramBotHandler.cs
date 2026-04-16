using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using VirgoBot.Configuration;
using VirgoBot.Features.Email;
using VirgoBot.Functions;
using VirgoBot.Integrations.ILink;
using VirgoBot.Services;
using VirgoBot.Utilities;

namespace VirgoBot.Channels;

public class TelegramBotHandler
{
    private readonly Config _config;
    private readonly TelegramBotClient _bot;
    private readonly LLMService _llmService;
    private readonly MemoryService _memoryService;
    private readonly FunctionRegistry _functionRegistry;
    private readonly MessageHelper _messageHelper;
    private readonly EmailManager _emailManager;
    private readonly ActivityMonitor _activityMonitor;
    private readonly ILinkBridgeService _iLinkBridge;
    private readonly CancellationToken _cancellationToken;

    public TelegramBotHandler(
        Config config,
        TelegramBotClient bot,
        LLMService llmService,
        MemoryService memoryService,
        FunctionRegistry functionRegistry,
        MessageHelper messageHelper,
        EmailManager emailManager,
        ActivityMonitor activityMonitor,
        ILinkBridgeService iLinkBridge,
        CancellationToken cancellationToken)
    {
        _config = config;
        _bot = bot;
        _llmService = llmService;
        _memoryService = memoryService;
        _functionRegistry = functionRegistry;
        _messageHelper = messageHelper;
        _emailManager = emailManager;
        _activityMonitor = activityMonitor;
        _iLinkBridge = iLinkBridge;
        _cancellationToken = cancellationToken;
    }

    public void Register()
    {
        _bot.OnMessage += async (msg, type) => await OnMessage(msg);
        _bot.OnUpdate += async (update) => await OnUpdate(update);
    }

    private async Task OnMessage(Message msg)
    {
        if (msg.Text is null || !_config.Channel.Telegram.AllowedUsers.Contains(msg.From!.Id)) return;

        _activityMonitor.UpdateActivity();
        ColorLog.Info($"MSG-{msg.Type}", $"[@{msg.From.Username}({msg.From.Id})] '{msg.Text}'");

        if (msg.Text == "/clear")
        {
            _memoryService.ClearAllMessages(msg.From.Id);
            await _bot.SendMessage(msg.Chat.Id, "✅ 聊天记录已清空");
            return;
        }

        if (msg.ReplyToMessage?.Text?.Contains("📧") == true)
        {
            var lines = msg.ReplyToMessage.Text.Split('\n');
            var uidLine = lines.FirstOrDefault(l => l.Contains("UID:"));
            if (uidLine != null)
            {
                var uid = uidLine.Split(':')[1].Trim();
                await _emailManager.HandleReply(uid, msg.Text);
                await _bot.SendMessage(msg.Chat.Id, "✅ 邮件发送咯~");
                await _bot.DeleteMessage(msg.Chat.Id, msg.ReplyToMessage.MessageId);
                return;
            }
        }

        await _bot.SendChatAction(msg.Chat.Id, ChatAction.Typing);

        _functionRegistry.SetTelegramBot(_bot, msg.Chat.Id);

        var reply = await _llmService.AskAsync(msg.From.Id, msg.Text, async (text) =>
        {
            var processed = _messageHelper.ProcessThinkTags(text);
            await _messageHelper.SendLongMessage(msg.Chat.Id, processed);
        }, async (stickerPath) =>
        {
            ColorLog.Debug("STICKER", stickerPath);
            if (File.Exists(stickerPath))
            {
                using var stream = File.OpenRead(stickerPath);
                await _bot.SendSticker(msg.Chat.Id, InputFile.FromStream(stream));
            }
        });

        if (!string.IsNullOrEmpty(reply))
        {
            reply = _messageHelper.ProcessThinkTags(reply);
            await _messageHelper.SendLongMessage(msg.Chat.Id, reply);
        }
    }

    private async Task OnUpdate(Update update)
    {
        if (update is { CallbackQuery: { } query })
        {
            if (!_config.Channel.Telegram.AllowedUsers.Contains(query.From.Id) || query.Data is null) return;

            if (query.Data.StartsWith("ignore_"))
            {
                var uid = query.Data.Replace("ignore_", "");
                _emailManager.IgnoreEmail(uid);
                await _bot.DeleteMessage(query.Message!.Chat.Id, query.Message.MessageId);
                await _bot.AnswerCallbackQuery(query.Id, "已忽略");
            }
        }
    }

    public async Task HandleILinkIncomingMessageAsync(ILinkIncomingMessage incoming)
    {
        var content = incoming.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        ColorLog.Info("ILINK-IN", $"[@{incoming.Username}] {content}");

        _activityMonitor.UpdateActivity();
        var reply = await _llmService.AskAsync(GetStableHashCode(incoming.UserId), content);

        if (string.IsNullOrWhiteSpace(reply))
        {
            return;
        }

        await _iLinkBridge.SendLongMessageAsync(reply, _cancellationToken);
    }

    /// <summary>
    /// Deterministic string hash that stays stable across process restarts.
    /// (Unlike string.GetHashCode() which is randomized per-process in .NET Core+)
    /// </summary>
    private static long GetStableHashCode(string str)
    {
        unchecked
        {
            long hash1 = 5381;
            long hash2 = hash1;

            for (var i = 0; i < str.Length && str[i] != '\0'; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ str[i];
                if (i == str.Length - 1) break;
                hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
            }

            return hash1 + hash2 * 1566083941;
        }
    }
}
