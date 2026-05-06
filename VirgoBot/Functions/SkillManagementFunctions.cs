using System.Text.Json;
using VirgoBot.Configuration;

namespace VirgoBot.Functions;

public static class SkillManagementFunctions
{
    public static IEnumerable<FunctionDefinition> Register()
    {
        yield return new FunctionDefinition("manage_skills", "Built-in tool for managing Skills. Supports two formats:\n1) JSON skill: filename.json, containing name, description, parameters, command/http fields, supports subSkills for multiple sub-functions. Format: {\r\n  \"name\": \"example_skill\",\r\n  \"description\": \"This is an example Skill. Files starting with underscore are not loaded.\",\r\n  \"parameters\": [\r\n    {\r\n      \"name\": \"arg1\",\r\n      \"type\": \"string\",\r\n      \"description\": \"Parameter 1\",\r\n      \"required\": true\r\n    },\r\n    {\r\n      \"name\": \"arg2\",\r\n      \"type\": \"string\",\r\n      \"description\": \"Parameter 2 (optional)\",\r\n      \"required\": false\r\n    }\r\n  ],\r\n  \"command\": \"echo {{arg1}} {{arg2}}\"\r\n}\n(2) SKILL.md standard format (compatible with OpenClaw / Claude Code): directory-type skills/{name}/SKILL.md, YAML frontmatter with name, description, allowed-tools, model, Markdown body as instructions, supports $ARGUMENTS parameter substitution.\nWhen creating SKILL.md, skill_name is the directory name, skill_content is the full SKILL.md content.\nIn JSON skills, use {{parameter_name}} double braces for parameter references.", new
        {
            type = "object",
            properties = new
            {
                action = new { type = "string", description = "Operation: list, get, create, update, delete" },
                skill_name = new { type = "string", description = "Skill name (JSON without .json suffix, SKILL.md as directory name), for get/create/update/delete operations" },
                skill_content = new { type = "string", description = "Complete skill content (JSON string or SKILL.md Markdown), for create and update operations" },
                skill_type = new { type = "string", description = "Skill type: json (default) or skill.md" }
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
                    _ => "Invalid operation, supported: list, get, create, update, delete"
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
                skills.Add(new { fileName, name = fileName, description = "Parse failed", mode = "unknown", skillType = "json" });
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
            return "Error: skill_name parameter cannot be empty";

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
            return $"Error: Skill '{skillName}' not found";

        var jsonContent = File.ReadAllText(filePath);
        return JsonSerializer.Serialize(new { success = true, fileName = $"{skillName}.json", content = jsonContent, skillType = "json" }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string CreateSkill(string dir, string skillName, string skillContent, string skillType)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return "Error: skill_name parameter cannot be empty";

        if (string.IsNullOrWhiteSpace(skillContent))
            return "Error: skill_content parameter cannot be empty";

        if (skillType == "skill.md")
        {
            var skillDir = Path.Combine(dir, skillName);
            if (Directory.Exists(skillDir) && File.Exists(Path.Combine(skillDir, "SKILL.md")))
                return $"Error: Skill '{skillName}' already exists, use update instead";

            // 验证 SKILL.md 格式
            var parsed = SkillMdParser.Parse(skillContent);
            if (parsed == null)
                return "Error: Invalid SKILL.md format, must include YAML frontmatter (---) with at least a name field";

            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), skillContent);
            return JsonSerializer.Serialize(new { success = true, message = $"Skill '{skillName}' (SKILL.md) created successfully" });
        }

        var filePath = Path.Combine(dir, $"{skillName}.json");
        if (File.Exists(filePath))
            return $"Error: Skill '{skillName}' already exists, use update instead";

        try
        {
            JsonDocument.Parse(skillContent);
            File.WriteAllText(filePath, skillContent);
            return JsonSerializer.Serialize(new { success = true, message = $"Skill '{skillName}' created successfully" });
        }
        catch (JsonException ex)
        {
            return $"Error: skill_content is not valid JSON - {ex.Message}";
        }
    }

    private static string UpdateSkill(string dir, string skillName, string skillContent, string skillType)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return "Error: skill_name parameter cannot be empty";

        if (string.IsNullOrWhiteSpace(skillContent))
            return "Error: skill_content parameter cannot be empty";

        // 先检查 SKILL.md 目录
        var skillMdPath = Path.Combine(dir, skillName, "SKILL.md");
        if (File.Exists(skillMdPath))
        {
            var parsed = SkillMdParser.Parse(skillContent);
            if (parsed == null)
                return "Error: Invalid SKILL.md format, must include YAML frontmatter (---) with at least a name field";

            File.WriteAllText(skillMdPath, skillContent);
            return JsonSerializer.Serialize(new { success = true, message = $"Skill '{skillName}' (SKILL.md) updated successfully" });
        }

        var filePath = Path.Combine(dir, $"{skillName}.json");
        if (!File.Exists(filePath))
            return $"Error: Skill '{skillName}' not found, use create instead";

        try
        {
            JsonDocument.Parse(skillContent);
            File.WriteAllText(filePath, skillContent);
            return JsonSerializer.Serialize(new { success = true, message = $"Skill '{skillName}' updated successfully" });
        }
        catch (JsonException ex)
        {
            return $"Error: skill_content is not valid JSON - {ex.Message}";
        }
    }

    private static string DeleteSkill(string dir, string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return "Error: skill_name parameter cannot be empty";

        // 先检查 SKILL.md 目录
        var skillDir = Path.Combine(dir, skillName);
        if (Directory.Exists(skillDir) && File.Exists(Path.Combine(skillDir, "SKILL.md")))
        {
            Directory.Delete(skillDir, recursive: true);
            return JsonSerializer.Serialize(new { success = true, message = $"Skill '{skillName}' (SKILL.md) deleted successfully" });
        }

        var filePath = Path.Combine(dir, $"{skillName}.json");
        if (!File.Exists(filePath))
            return $"Error: Skill '{skillName}' not found";

        File.Delete(filePath);
        return JsonSerializer.Serialize(new { success = true, message = $"Skill '{skillName}' deleted successfully" });
    }
}
