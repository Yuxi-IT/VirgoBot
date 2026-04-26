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
        var hasImage = incoming.ItemList?.Any(i => i.ImageItem != null) == true;

        if (string.IsNullOrWhiteSpace(content) && !hasImage)
            return;

        ColorLog.Info("ILINK-IN", $"[@{incoming.FromUserId}] {content}{(hasImage ? " [图片]" : "")}");

        List<ImageInput>? images = null;
        if (hasImage)
        {
            var imageBytes = await _iLinkBridge.DownloadImageAsync(incoming, _cancellationToken);
            if (imageBytes != null)
            {
                var base64 = Convert.ToBase64String(imageBytes);
                images = [ImageInput.FromBase64(base64, "image/jpeg")];
            }
        }

        var reply = await _llmService.AskAsync(
            string.IsNullOrWhiteSpace(content) ? null : content,
            images: images);

        if (string.IsNullOrWhiteSpace(reply))
            return;

        await _iLinkBridge.ReplyLongTextAsync(incoming, reply, _cancellationToken);
    }
}
