using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace VirgoBot.Functions;

public static class TelegramFunctions
{
    public static IEnumerable<FunctionDefinition> Register(TelegramBotClient bot, long chatId)
    {
        yield return new FunctionDefinition("send_photo", "Send photo to Telegram", new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "Image path (local path or URL)" },
                caption = new { type = "string", description = "Photo caption (optional)" }
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
                if (!File.Exists(path)) return "File not found";
                using var stream = File.OpenRead(path);
                await bot.SendPhoto(chatId, InputFile.FromStream(stream, Path.GetFileName(path)), caption: caption);
            }
            return "Photo sent";
        });

        yield return new FunctionDefinition("send_voice", "Send voice message to Telegram", new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "Voice file path (local path or URL, supports .ogg/.mp3)" },
                caption = new { type = "string", description = "Voice caption (optional)" }
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
                if (!File.Exists(path)) return "File not found";
                using var stream = File.OpenRead(path);
                await bot.SendVoice(chatId, InputFile.FromStream(stream, Path.GetFileName(path)), caption: caption);
            }
            return "Voice sent";
        });
    }
}
