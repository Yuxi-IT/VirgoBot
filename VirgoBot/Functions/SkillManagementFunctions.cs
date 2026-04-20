using System.Text.Json;
using VirgoBot.Configuration;

namespace VirgoBot.Functions;

public static class SkillManagementFunctions
{
    public static IEnumerable<FunctionDefinition> Register()
    {
        yield return new FunctionDefinition("manage_skills", "管理 Skills 的内置工具。支持的操作：list(列出所有skills)、get(获取指定skill内容)、create(创建新skill)、update(更新skill)、delete(删除skill)。skill_content 必须是完整的 JSON 字符串。支持两种格式：1) 单功能 skill，包含 name、description、parameters、command 或 http 字段；2) 多子功能 skill，包含 name、description 和 subSkills 数组，每个子功能有独立的 name、description、parameters、command 或 http，子功能注册时名称为 父名_子名，如 office_word_read。", new
        {
            type = "object",
            properties = new
            {
                action = new { type = "string", description = "操作类型：list、get、create、update、delete" },
                skill_name = new { type = "string", description = "skill 文件名(不含.json后缀)，用于 get/create/update/delete 操作" },
                skill_content = new { type = "string", description = "完整的 skill JSON 内容，用于 create 和 update 操作" }
            },
            required = new[] { "action" }
        }, async input =>
        {
            try
            {
                var action = input.TryGetProperty("action", out var a) ? a.GetString() ?? "list" : "list";
                var skillName = input.TryGetProperty("skill_name", out var sn) ? sn.GetString() ?? "" : "";
                var skillContent = input.TryGetProperty("skill_content", out var sc) ? sc.GetString() ?? "" : "";

                var dir = AppConstants.SkillsDirectory;
                Directory.CreateDirectory(dir);

                return action.ToLower() switch
                {
                    "list" => ListSkills(dir),
                    "get" => GetSkill(dir, skillName),
                    "create" => CreateSkill(dir, skillName, skillContent),
                    "update" => UpdateSkill(dir, skillName, skillContent),
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

    private static string GetSkill(string dir, string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return "错误: skill_name 参数不能为空";

        var filePath = Path.Combine(dir, $"{skillName}.json");
        if (!File.Exists(filePath))
            return $"错误: Skill '{skillName}' 不存在";

        var content = File.ReadAllText(filePath);
        return JsonSerializer.Serialize(new { success = true, fileName = $"{skillName}.json", content }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string CreateSkill(string dir, string skillName, string skillContent)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return "错误: skill_name 参数不能为空";

        if (string.IsNullOrWhiteSpace(skillContent))
            return "错误: skill_content 参数不能为空";

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

    private static string UpdateSkill(string dir, string skillName, string skillContent)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return "错误: skill_name 参数不能为空";

        if (string.IsNullOrWhiteSpace(skillContent))
            return "错误: skill_content 参数不能为空";

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

        var filePath = Path.Combine(dir, $"{skillName}.json");
        if (!File.Exists(filePath))
            return $"错误: Skill '{skillName}' 不存在";

        File.Delete(filePath);
        return JsonSerializer.Serialize(new { success = true, message = $"Skill '{skillName}' 删除成功" });
    }
}
