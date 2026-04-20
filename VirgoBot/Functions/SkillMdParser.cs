using System.Text.RegularExpressions;

namespace VirgoBot.Functions;

/// <summary>
/// 解析 OpenClaw 格式的 SKILL.md 文件
/// 格式：YAML frontmatter (name + description) + Markdown 正文
/// </summary>
public static class SkillMdParser
{
    private static readonly Regex FrontmatterRegex = new(@"^---\s*\n(.*?)\n---\s*\n?(.*)", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex YamlFieldRegex = new(@"^(\w+):\s*(.+)$", RegexOptions.Multiline | RegexOptions.Compiled);

    public record ParsedSkillMd(string Name, string Description, string Body);

    public static ParsedSkillMd? Parse(string content)
    {
        var match = FrontmatterRegex.Match(content.TrimStart());
        if (!match.Success)
            return null;

        var frontmatter = match.Groups[1].Value;
        var body = match.Groups[2].Value.Trim();

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match fieldMatch in YamlFieldRegex.Matches(frontmatter))
        {
            fields[fieldMatch.Groups[1].Value] = fieldMatch.Groups[2].Value.Trim().Trim('"', '\'');
        }

        if (!fields.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
            return null;

        if (!fields.TryGetValue("description", out var description))
            description = "";

        return new ParsedSkillMd(name, description, body);
    }
}
