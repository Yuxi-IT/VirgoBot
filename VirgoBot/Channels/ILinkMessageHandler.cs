using OpenILink.SDK;
using VirgoBot.Configuration;
using VirgoBot.Integrations.ILink;
using VirgoBot.Services;
using VirgoBot.Utilities;

namespace VirgoBot.Channels;

public class ILinkMessageHandler
{
    private readonly Config _config;
    private readonly LLMService _llmService;
    private readonly MemoryService _memoryService;
    private readonly ILinkBridgeService _iLinkBridge;
    private readonly CancellationToken _cancellationToken;

    public ILinkMessageHandler(
        Config config,
        LLMService llmService,
        MemoryService memoryService,
        ILinkBridgeService iLinkBridge,
        CancellationToken cancellationToken)
    {
        _config = config;
        _llmService = llmService;
        _memoryService = memoryService;
        _iLinkBridge = iLinkBridge;
        _cancellationToken = cancellationToken;
    }

    public async Task HandleIncomingMessageAsync(WeixinMessage incoming)
    {
        var content = incoming.ExtractText()?.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        ColorLog.Info("ILINK-IN", $"[@{incoming.FromUserId}] {content}");

        var reply = await _llmService.AskAsync(HashHelper.GetStableHashCode(incoming.FromUserId), content);

        if (string.IsNullOrWhiteSpace(reply))
        {
            return;
        }

        await _iLinkBridge.ReplyLongTextAsync(incoming, reply, _cancellationToken);
    }
}
