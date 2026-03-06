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
        const int limit = 4000;
        Console.WriteLine($"[BOT] {text}");

        for (int i = 0; i < text.Length; i += limit)
        {
            var part = text.Substring(i, Math.Min(limit, text.Length - i));
            foreach (var line in part.Split(Environment.NewLine))
            {
                await _bot.SendMessage(chatId, line.Trim(), parseMode: ParseMode.Html);
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
