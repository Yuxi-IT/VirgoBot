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

        // 将分隔符字符串按 | 分割成数组
        var delimiterArray = delimiters.Split('|', StringSplitOptions.RemoveEmptyEntries);

        if (delimiterArray.Length == 0)
        {
            return new[] { text };
        }

        // 使用所有分隔符分割文本
        var parts = text.Split(delimiterArray, StringSplitOptions.RemoveEmptyEntries);

        // 过滤掉空白段落
        return parts
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToArray();
    }
}
