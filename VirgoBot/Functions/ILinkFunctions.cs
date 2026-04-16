using System.Text.Json;
using VirgoBot.Integrations.ILink;

namespace VirgoBot.Functions;

public static class ILinkFunctions
{
    public static IEnumerable<FunctionDefinition> Register(ILinkBridgeService iLinkBridge)
    {
        yield return new FunctionDefinition("send_ilink_image", "发送图片到iLink平台", new
        {
            type = "object",
            properties = new
            {
                image_path = new { type = "string", description = "图片本地路径或URL" }
            },
            required = new[] { "image_path" }
        }, async input =>
        {
            var imagePath = input.GetProperty("image_path").GetString() ?? "";
            // 注意：iLink 需要原始 IncomingMessage 才能发送图片，暂不支持
            return "iLink 发送图片功能暂不支持（需要原始消息上下文）";
        });
    }
}
