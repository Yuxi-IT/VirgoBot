using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace VirgoBot.Utilities;

public class MessageHelper
{
    private readonly TelegramBotClient _bot;
    private readonly string _messageSplitDelimiters;

    public MessageHelper(TelegramBotClient bot, string messageSplitDelimiters)
    {
        _bot = bot;
        _messageSplitDelimiters = messageSplitDelimiters;
    }

    public async Task SendLongMessage(long chatId, string text)
    {
        var paragraphs = MessageSplitter.SplitMessage(text, _messageSplitDelimiters);

        foreach (var paragraph in paragraphs)
        {
            Console.WriteLine($"[BOT] {paragraph}");
            try
            {
                await _bot.SendMessage(chatId, paragraph, parseMode: ParseMode.Markdown);
            }
            catch
            {
                await _bot.SendMessage(chatId, paragraph);
            }
            await Task.Delay(300);
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
