using System.Text.Json;
using VirgoBot.Configuration;

namespace VirgoBot.Functions;

public static class SkillManagementFunctions
{
    public static IEnumerable<FunctionDefinition> Register()
    {
        yield return new FunctionDefinition("manage_skills", "管理 Skills 的内置工具。支持两种格式：\n1) JSON skill：文件名.json，包含 name、description、parameters、command/http 字段，支持 subSkills 多子功能；格式：{\r\n  \"name\": \"example_skill\",\r\n  \"description\": \"这是一个示例 Skill，以下划线开头的文件不会被加载\",\r\n  \"parameters\": [\r\n    {\r\n      \"name\": \"arg1\",\r\n      \"type\": \"string\",\r\n      \"description\": \"参数1\",\r\n      \"required\": true\r\n    },\r\n    {\r\n      \"name\": \"arg2\",\r\n      \"type\": \"string\",\r\n      \"description\": \"参数2(可选)\",\r\n      \"required\": false\r\n    }\r\n  ],\r\n  \"command\": \"echo {{arg1}} {{arg2}}\"\r\n}\n(2) SKILL.md 标准格式（兼容 OpenClaw / Claude Code）：目录型 skills/名称/SKILL.md，YAML frontmatter 含 name、description、allowed-tools、model，Markdown 正文作为指令，支持 $ARGUMENTS 参数替换。\n创建 SKILL.md 时 skill_name 为目录名，skill_content 为完整 SKILL.md 内容。\nJSON skill 中参数用 {{参数名}} 双大括号引用。", new
        {
            type = "object",
            properties = new
            {
                action = new { type = "string", description = "操作类型：list、get、create、update、delete" },
                skill_name = new { type = "string", description = "skill 名称（JSON 不含.json后缀，SKILL.md 为目录名），用于 get/create/update/delete 操作" },
                skill_content = new { type = "string", description = "完整的 skill 内容（JSON 字符串或 SKILL.md Markdown），用于 create 和 update 操作" },
                skill_type = new { type = "string", description = "skill 类型：json（默认）或 skill.md" }
            },
            required = new[] { "action" }
        }, async input =>
        {
            try
            {
                var action = input.TryGetProperty("action", out var a) ? a.GetString() ?? "list" : "list";
                var skillName = input.TryGetProperty("skill_name", out var sn) ? sn.GetString() ?? "" : "";
                var skillContent = input.TryGetProperty("skill_content", out var sc) ? sc.GetString() ?? "" : "";
                var skillType = input.TryGetProperty("skill_type", out var st) ? st.GetString() ?? "json" : "json";

                var dir = AppConstants.SkillsDirectory;
                Directory.CreateDirectory(dir);

                return action.ToLower() switch
                {
                    "list" => ListSkills(dir),
                    "get" => GetSkill(dir, skillName, skillType),
                    "create" => CreateSkill(dir, skillName, skillContent, skillType),
                    "update" => UpdateSkill(dir, skillName, skillContent, skillType),
                    "delete" => DeleteSkill(dir, skillName),
                    _ => "无效的操作类型，支持: list, get, create, update, delete"
                };
            }
            catch (Exception ex)
            {
                return $"执行失败: {ex.Message}";
            }
        });
    }

    private static string ListSkills(string dir)
    {
        var skills = new List<object>();

        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.StartsWith("_")) continue;

            try
            {
                var json = File.ReadAllText(file);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("subSkills", out var subSkillsEl) && subSkillsEl.ValueKind == JsonValueKind.Array)
                {
                    var subNames = subSkillsEl.EnumerateArray()
                        .Select(s => s.TryGetProperty("name", out var sn) ? sn.GetString() : null)
                        .Where(n => n != null)
                        .ToList();
                    skills.Add(new
                    {
                        fileName,
                        name = root.TryGetProperty("name", out var n) ? n.GetString() : fileName,
                        description = root.TryGetProperty("description", out var d) ? d.GetString() : "",
                        mode = "multi",
                        skillType = "json",
                        subSkills = subNames
                    });
                    continue;
                }

                skills.Add(new
                {
                    fileName,
                    name = root.TryGetProperty("name", out var n2) ? n2.GetString() : fileName,
                    description = root.TryGetProperty("description", out var d2) ? d2.GetString() : "",
                    mode = root.TryGetProperty("mode", out var m) ? m.GetString() : "command",
                    skillType = "json",
                    subSkills = (List<string?>)null!
                });
            }
            catch
            {
                skills.Add(new { fileName, name = fileName, description = "解析失败", mode = "unknown", skillType = "json" });
            }
        }

        foreach (var subDir in Directory.GetDirectories(dir))
        {
            var skillMdPath = Path.Combine(subDir, "SKILL.md");
            if (!File.Exists(skillMdPath)) continue;

            try
            {
                var content = File.ReadAllText(skillMdPath);
                var parsed = SkillMdParser.Parse(content);
                if (parsed == null) continue;

                skills.Add(new
                {
                    fileName = Path.GetFileName(subDir) + "/SKILL.md",
                    name = parsed.Name,
                    description = parsed.Description,
                    mode = "skill.md",
                    skillType = "skill.md"
                });
            }
            catch { }
        }

        return JsonSerializer.Serialize(new { success = true, count = skills.Count, skills }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string GetSkill(string dir, string skillName, string skillType)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return "错误: skill_name 参数不能为空";

        // 先检查 SKILL.md 目录
        var skillMdPath = Path.Combine(dir, skillName, "SKILL.md");
        if (File.Exists(skillMdPath))
        {
            var content = File.ReadAllText(skillMdPath);
            return JsonSerializer.Serialize(new { success = true, fileName = $"{skillName}/SKILL.md", content, skillType = "skill.md" }, new JsonSerializerOptions { WriteIndented = true });
        }

        // 再检查 JSON
        var filePath = Path.Combine(dir, $"{skillName}.json");
        if (!File.Exists(filePath))
            return $"错误: Skill '{skillName}' 不存在";

        var jsonContent = File.ReadAllText(filePath);
        return JsonSerializer.Serialize(new { success = true, fileName = $"{skillName}.json", content = jsonContent, skillType = "json" }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string CreateSkill(string dir, string skillName, string skillContent, string skillType)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return "错误: skill_name 参数不能为空";

        if (string.IsNullOrWhiteSpace(skillContent))
            return "错误: skill_content 参数不能为空";

        if (skillType == "skill.md")
        {
            var skillDir = Path.Combine(dir, skillName);
            if (Directory.Exists(skillDir) && File.Exists(Path.Combine(skillDir, "SKILL.md")))
                return $"错误: Skill '{skillName}' 已存在，请使用 update 操作";

            // 验证 SKILL.md 格式
            var parsed = SkillMdParser.Parse(skillContent);
            if (parsed == null)
                return "错误: SKILL.md 格式无效，需要包含 YAML frontmatter (---) 和至少 name 字段";

            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), skillContent);
            return JsonSerializer.Serialize(new { success = true, message = $"Skill '{skillName}' (SKILL.md) 创建成功" });
        }

        var filePath = Path.Combine(dir, $"{skillName}.json");
        if (File.Exists(filePath))
            return $"错误: Skill '{skillName}' 已存在，请使用 update 操作";

        try
        {
            JsonDocument.Parse(skillContent);
            File.WriteAllText(filePath, skillContent);
            return JsonSerializer.Serialize(new { success = true, message = $"Skill '{skillName}' 创建成功" });
        }
        catch (JsonException ex)
        {
            return $"错误: skill_content 不是有效的 JSON 格式 - {ex.Message}";
        }
    }

    private static string UpdateSkill(string dir, string skillName, string skillContent, string skillType)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return "错误: skill_name 参数不能为空";

        if (string.IsNullOrWhiteSpace(skillContent))
            return "错误: skill_content 参数不能为空";

        // 先检查 SKILL.md 目录
        var skillMdPath = Path.Combine(dir, skillName, "SKILL.md");
        if (File.Exists(skillMdPath))
        {
            var parsed = SkillMdParser.Parse(skillContent);
            if (parsed == null)
                return "错误: SKILL.md 格式无效，需要包含 YAML frontmatter (---) 和至少 name 字段";

            File.WriteAllText(skillMdPath, skillContent);
            return JsonSerializer.Serialize(new { success = true, message = $"Skill '{skillName}' (SKILL.md) 更新成功" });
        }

        var filePath = Path.Combine(dir, $"{skillName}.json");
        if (!File.Exists(filePath))
            return $"错误: Skill '{skillName}' 不存在，请使用 create 操作";

        try
        {
            JsonDocument.Parse(skillContent);
            File.WriteAllText(filePath, skillContent);
            return JsonSerializer.Serialize(new { success = true, message = $"Skill '{skillName}' 更新成功" });
        }
        catch (JsonException ex)
        {
            return $"错误: skill_content 不是有效的 JSON 格式 - {ex.Message}";
        }
    }

    private static string DeleteSkill(string dir, string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return "错误: skill_name 参数不能为空";

        // 先检查 SKILL.md 目录
        var skillDir = Path.Combine(dir, skillName);
        if (Directory.Exists(skillDir) && File.Exists(Path.Combine(skillDir, "SKILL.md")))
        {
            Directory.Delete(skillDir, recursive: true);
            return JsonSerializer.Serialize(new { success = true, message = $"Skill '{skillName}' (SKILL.md) 删除成功" });
        }

        var filePath = Path.Combine(dir, $"{skillName}.json");
        if (!File.Exists(filePath))
            return $"错误: Skill '{skillName}' 不存在";

        File.Delete(filePath);
        return JsonSerializer.Serialize(new { success = true, message = $"Skill '{skillName}' 删除成功" });
    }
}
