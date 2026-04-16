using VirgoBot.Configuration;
using VirgoBot.Functions;
using VirgoBot.Integrations.ILink;
using VirgoBot.Services;
using VirgoBot.Utilities;
using ILink4NET.Models;

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

    public async Task HandleIncomingMessageAsync(IncomingMessage incoming)
    {
        var content = incoming.Text?.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        ColorLog.Info("ILINK-IN", $"[@{incoming.UserId}] {content}");

        var reply = await _llmService.AskAsync(HashHelper.GetStableHashCode(incoming.UserId), content);

        if (string.IsNullOrWhiteSpace(reply))
        {
            return;
        }

        await _iLinkBridge.ReplyLongTextAsync(incoming, reply, _cancellationToken);
    }
}
