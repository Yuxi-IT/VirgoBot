using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace VirgoBot.Functions;

public static class TelegramFunctions
{
    public static IEnumerable<FunctionDefinition> Register(TelegramBotClient bot, long chatId)
    {
        yield return new FunctionDefinition("send_photo", "发送图片到Telegram", new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "图片路径(本地路径或URL)" },
                caption = new { type = "string", description = "图片说明(可选)" }
            },
            required = new[] { "path" }
        }, async input =>
        {
            var path = input.GetProperty("path").GetString() ?? "";
            var caption = input.TryGetProperty("caption", out var c) ? c.GetString() : null;

            if (path.StartsWith("http://") || path.StartsWith("https://"))
            {
                await bot.SendPhoto(chatId, InputFile.FromUri(path), caption: caption);
            }
            else
            {
                if (!File.Exists(path)) return "文件不存在";
                using var stream = File.OpenRead(path);
                await bot.SendPhoto(chatId, InputFile.FromStream(stream, Path.GetFileName(path)), caption: caption);
            }
            return "图片已发送";
        });

        yield return new FunctionDefinition("send_voice", "发送语音到Telegram", new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "语音文件路径(本地路径或URL,支持.ogg/.mp3)" },
                caption = new { type = "string", description = "语音说明(可选)" }
            },
            required = new[] { "path" }
        }, async input =>
        {
            var path = input.GetProperty("path").GetString() ?? "";
            var caption = input.TryGetProperty("caption", out var c) ? c.GetString() : null;

            if (path.StartsWith("http://") || path.StartsWith("https://"))
            {
                await bot.SendVoice(chatId, InputFile.FromUri(path), caption: caption);
            }
            else
            {
                if (!File.Exists(path)) return "文件不存在";
                using var stream = File.OpenRead(path);
                await bot.SendVoice(chatId, InputFile.FromStream(stream, Path.GetFileName(path)), caption: caption);
            }
            return "语音已发送";
        });
    }
}
