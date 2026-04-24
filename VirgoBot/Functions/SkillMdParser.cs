using System.Text.RegularExpressions;

namespace VirgoBot.Functions;

/// <summary>
/// 解析标准 SKILL.md 文件（兼容 OpenClaw / Claude Code 格式）
/// 格式：YAML frontmatter (name, description, allowed-tools, model) + Markdown 正文
/// </summary>
public static class SkillMdParser
{
    private static readonly Regex FrontmatterRegex = new(@"^---\s*\n(.*?)\n---\s*\n?(.*)", RegexOptions.Singleline | RegexOptions.Compiled);

    public record ParsedSkillMd(
        string Name,
        string Description,
        string Body,
        List<string> AllowedTools,
        string? Model
    );

    public static ParsedSkillMd? Parse(string content)
    {
        var match = FrontmatterRegex.Match(content.TrimStart());
        if (!match.Success)
            return null;

        var frontmatter = match.Groups[1].Value;
        var body = match.Groups[2].Value.Trim();

        var fields = ParseYamlFrontmatter(frontmatter);

        if (!fields.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
            return null;

        fields.TryGetValue("description", out var description);
        description ??= "";

        fields.TryGetValue("model", out var model);

        var allowedTools = new List<string>();
        if (fields.TryGetValue("allowed-tools", out var toolsRaw) && !string.IsNullOrWhiteSpace(toolsRaw))
        {
            // 逗号分隔的内联格式: "Read, Write, Bash"
            foreach (var tool in toolsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                allowedTools.Add(tool);
        }

        return new ParsedSkillMd(name, description, body, allowedTools, model);
    }

    /// <summary>
    /// 解析 YAML frontmatter，支持：
    /// - 简单 key: value
    /// - 多行折叠 (>) 和保留 (|) 标量
    /// - YAML 列表 (- item)
    /// </summary>
    private static Dictionary<string, string> ParseYamlFrontmatter(string frontmatter)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = frontmatter.Split('\n');

        string? currentKey = null;
        var currentValue = new List<string>();
        bool isMultiline = false;
        bool isList = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimEnd();

            // 新的顶级 key（非缩进行，包含冒号）
            var keyMatch = Regex.Match(trimmed, @"^(\w[\w-]*):\s*(.*)$");
            if (keyMatch.Success && !trimmed.StartsWith(" ") && !trimmed.StartsWith("\t"))
            {
                // 保存上一个 key
                if (currentKey != null)
                    fields[currentKey] = FinalizeValue(currentValue, isMultiline, isList);

                currentKey = keyMatch.Groups[1].Value;
                var valueStr = keyMatch.Groups[2].Value.Trim();

                currentValue.Clear();
                isMultiline = false;
                isList = false;

                if (valueStr == ">" || valueStr == "|")
                {
                    isMultiline = true;
                }
                else if (string.IsNullOrEmpty(valueStr))
                {
                    // 可能是列表或多行，看下一行
                }
                else
                {
                    // 去掉引号
                    currentValue.Add(valueStr.Trim('"', '\''));
                }
            }
            else if (currentKey != null)
            {
                // 缩进行 — 属于当前 key
                var content = trimmed;
                if (content.TrimStart().StartsWith("- "))
                {
                    isList = true;
                    currentValue.Add(content.TrimStart()[2..].Trim());
                }
                else
                {
                    currentValue.Add(content.TrimStart());
                }
            }
        }

        // 保存最后一个 key
        if (currentKey != null)
            fields[currentKey] = FinalizeValue(currentValue, isMultiline, isList);

        return fields;
    }

    private static string FinalizeValue(List<string> parts, bool isMultiline, bool isList)
    {
        if (parts.Count == 0) return "";

        if (isList)
            return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

        if (isMultiline)
            return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

        return string.Join(" ", parts).Trim();
    }

    /// <summary>
    /// 从 ParsedSkillMd 生成标准 SKILL.md 内容
    /// </summary>
    public static string Generate(string name, string description, string body, List<string>? allowedTools = null, string? model = null)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"name: {name}");

        if (!string.IsNullOrWhiteSpace(description))
        {
            if (description.Contains('\n'))
            {
                sb.AppendLine("description: >");
                foreach (var line in description.Split('\n'))
                    sb.AppendLine($"  {line.TrimEnd()}");
            }
            else
            {
                sb.AppendLine($"description: {description}");
            }
        }

        if (allowedTools != null && allowedTools.Count > 0)
        {
            sb.AppendLine("allowed-tools:");
            foreach (var tool in allowedTools)
                sb.AppendLine($"  - {tool}");
        }

        if (!string.IsNullOrWhiteSpace(model))
            sb.AppendLine($"model: {model}");

        sb.AppendLine("---");

        if (!string.IsNullOrWhiteSpace(body))
        {
            sb.AppendLine();
            sb.Append(body);
        }

        return sb.ToString();
    }
}
