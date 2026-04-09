using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace VirgoBot.Helpers;

public class MessageHelper
{
    private readonly TelegramBotClient _bot;

    public MessageHelper(TelegramBotClient bot)
    {
        _bot = bot;
    }

    public async Task SendLongMessage(long chatId, string text)
    {
        var paragraphs = text.Split(["\n\n", "\r\n\r\n", "？", "?", "。"], StringSplitOptions.RemoveEmptyEntries);

        foreach (var paragraph in paragraphs)
        {
            var trimmed = paragraph.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                Console.WriteLine($"[BOT] {trimmed}");
                try
                {
                    await _bot.SendMessage(chatId, trimmed, parseMode: ParseMode.Markdown);
                }
                catch
                {
                    await _bot.SendMessage(chatId, trimmed);
                }
                await Task.Delay(300);
            }
        }
    }

    public string ProcessThinkTags(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var pattern = @"<think>(.*?)</think>";

        return Regex.Replace(input, pattern, match =>
        {
            var content = match.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(content)) return "";
            return $"<pre>{System.Net.WebUtility.HtmlEncode(content.Trim())}</pre>";
        }, RegexOptions.Singleline);
    }
}
