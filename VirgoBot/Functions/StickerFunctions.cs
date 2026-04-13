using System.Text.Json;
using VirgoBot.Services;

namespace VirgoBot.Functions;

public static class StickerFunctions
{
    public static IEnumerable<FunctionDefinition> Register(StickerService stickerService)
    {
        yield return new FunctionDefinition("list_stickers", "浏览表情包列表", new
        {
            type = "object",
            properties = new
            {
                page = new { type = "number", description = "页码(1-5)" }
            },
            required = new[] { "page" }
        }, async input =>
        {
            var page = input.GetProperty("page").GetInt32();
            return stickerService.GetStickerList(page);
        });

        yield return new FunctionDefinition("send_sticker", "发送选中的表情包", new
        {
            type = "object",
            properties = new
            {
                filename = new { type = "string", description = "表情包文件名" }
            },
            required = new[] { "filename" }
        }, async input =>
        {
            var filename = input.GetProperty("filename").GetString() ?? "";
            var sticker = stickerService.GetStickerByFilename(filename);
            return sticker ?? "no_match";
        });
    }
}
