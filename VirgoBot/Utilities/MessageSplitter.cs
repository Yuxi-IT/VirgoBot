namespace VirgoBot.Utilities;

using System.Text.RegularExpressions;

public static class MessageSplitter
{
    private static readonly Regex CodeBlockRegex = new(@"```[\s\S]*?```", RegexOptions.Compiled);

    public static string[] SplitMessage(string text, string delimiters)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        if (string.IsNullOrWhiteSpace(delimiters))
        {
            return new[] { text };
        }

        // Skip splitting if text contains code blocks
        if (CodeBlockRegex.IsMatch(text))
        {
            return new[] { text };
        }

        var delimiterArray = delimiters.Split('|', StringSplitOptions.RemoveEmptyEntries);

        if (delimiterArray.Length == 0)
        {
            return new[] { text };
        }

        var parts = text.Split(delimiterArray, StringSplitOptions.RemoveEmptyEntries);

        return parts
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToArray();
    }
}
