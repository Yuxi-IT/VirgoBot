using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using VirgoBot.Configuration;

namespace VirgoBot.Functions;

public static class SkillLoader
{
    private const int CommandTimeoutSeconds = 60;

    public static IEnumerable<FunctionDefinition> LoadAll()
    {
        var dir = AppConstants.SkillsDirectory;
        Directory.CreateDirectory(dir);

        if (!Directory.GetFiles(dir, "*.json").Any())
            CreateExampleSkill(dir);

        var files = Directory.GetFiles(dir, "*.json");
        var loaded = new List<FunctionDefinition>();

        foreach (var file in files)
        {
            if (Path.GetFileName(file).StartsWith("_"))
                continue;

            FunctionDefinition? def = null;
            try
            {
                def = ParseSkillFile(file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SkillLoader] 加载 Skill 失败: {Path.GetFileName(file)} - {ex.Message}");
            }

            if (def != null)
                loaded.Add(def);
        }

        Console.WriteLine($"[SkillLoader] 已加载 {loaded.Count} 个外部 Skill");
        return loaded;
    }

    private static FunctionDefinition ParseSkillFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var name = root.GetProperty("name").GetString()
            ?? throw new InvalidOperationException("Skill 缺少 name 字段");
        var description = root.GetProperty("description").GetString()
            ?? throw new InvalidOperationException("Skill 缺少 description 字段");
        var command = root.GetProperty("command").GetString()
            ?? throw new InvalidOperationException("Skill 缺少 command 字段");

        var parameters = new List<SkillParameter>();
        if (root.TryGetProperty("parameters", out var paramsElement))
        {
            foreach (var param in paramsElement.EnumerateArray())
            {
                parameters.Add(new SkillParameter
                {
                    Name = param.GetProperty("name").GetString() ?? "",
                    Type = param.TryGetProperty("type", out var t) ? t.GetString() ?? "string" : "string",
                    Description = param.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                    Required = param.TryGetProperty("required", out var r) && r.GetBoolean()
                });
            }
        }

        var inputSchema = BuildInputSchema(parameters);
        var commandTemplate = command;

        return new FunctionDefinition(name, description, inputSchema, input =>
        {
            var resolvedCommand = ResolveCommand(commandTemplate, parameters, input);
            return Task.FromResult(ExecuteShell(resolvedCommand));
        });
    }

    private static object BuildInputSchema(List<SkillParameter> parameters)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var param in parameters)
        {
            properties[param.Name] = new Dictionary<string, string>
            {
                ["type"] = param.Type,
                ["description"] = param.Description
            };

            if (param.Required)
                required.Add(param.Name);
        }

        if (required.Count > 0)
        {
            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = required
            };
        }

        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties
        };
    }

    private static string ResolveCommand(string commandTemplate, List<SkillParameter> parameters, JsonElement input)
    {
        var result = commandTemplate;

        foreach (var param in parameters)
        {
            var placeholder = "{{" + param.Name + "}}";
            var value = "";

            if (input.TryGetProperty(param.Name, out var prop))
            {
                value = prop.ValueKind == JsonValueKind.String
                    ? prop.GetString() ?? ""
                    : prop.GetRawText();
            }

            result = result.Replace(placeholder, value);
        }

        // 清理多余空格
        result = Regex.Replace(result, @"\s{2,}", " ").Trim();

        return result;
    }

    private static string ExecuteShell(string command)
    {
        try
        {
            var isWindows = OperatingSystem.IsWindows();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = isWindows ? "cmd.exe" : "/bin/bash",
                    Arguments = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(CommandTimeoutSeconds * 1000))
            {
                process.Kill(true);
                return $"命令执行超时({CommandTimeoutSeconds}秒)";
            }

            return string.IsNullOrEmpty(error) ? output : $"{output}\n错误: {error}";
        }
        catch (Exception ex)
        {
            return $"执行失败: {ex.Message}";
        }
    }

    private static void CreateExampleSkill(string dir)
    {
        var example = new
        {
            name = "example_skill",
            description = "这是一个示例 Skill，以下划线开头的文件不会被加载",
            parameters = new[]
            {
                new { name = "arg1", type = "string", description = "参数1", required = true },
                new { name = "arg2", type = "string", description = "参数2(可选)", required = false }
            },
            command = "echo {{arg1}} {{arg2}}"
        };

        var json = JsonSerializer.Serialize(example, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(dir, "_example.json"), json);
    }

    private class SkillParameter
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "string";
        public string Description { get; set; } = "";
        public bool Required { get; set; }
    }
}
