namespace VirgoBot.Utilities;

public static class MessageSplitter
{
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
