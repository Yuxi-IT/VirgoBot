using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VirgoBot.Configuration;
using VirgoBot.Utilities;

namespace VirgoBot.Functions;

public static class SkillLoader
{
    private const int CommandTimeoutSeconds = 60;

    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(CommandTimeoutSeconds)
    };

    public static IEnumerable<FunctionDefinition> LoadAll()
    {
        var dir = AppConstants.SkillsDirectory;
        Directory.CreateDirectory(dir);

        if (!Directory.GetFiles(dir, "*.json").Any() && !Directory.GetDirectories(dir).Any(d => File.Exists(Path.Combine(d, "SKILL.md"))))
            CreateExampleSkill(dir);

        var loaded = new List<FunctionDefinition>();

        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            if (Path.GetFileName(file).StartsWith("_"))
                continue;

            try
            {
                var def = ParseSkillFile(file);
                if (def != null) loaded.Add(def);
            }
            catch (Exception ex)
            {
                ColorLog.Warning("SkillLoader", $"加载 Skill 失败: {Path.GetFileName(file)} - {ex.Message}");
            }
        }

        foreach (var subDir in Directory.GetDirectories(dir))
        {
            var skillMdPath = Path.Combine(subDir, "SKILL.md");
            if (!File.Exists(skillMdPath))
                continue;

            try
            {
                var def = ParseSkillMdDirectory(subDir, skillMdPath);
                if (def != null) loaded.Add(def);
            }
            catch (Exception ex)
            {
                ColorLog.Warning("SkillLoader", $"加载 SKILL.md 失败: {Path.GetFileName(subDir)} - {ex.Message}");
            }
        }

        ColorLog.Warning("SkillLoader", $"已加载 {loaded.Count} 个外部 Skill");
        return loaded;
    }

    private static FunctionDefinition? ParseSkillMdDirectory(string skillDir, string skillMdPath)
    {
        var content = File.ReadAllText(skillMdPath);
        var parsed = SkillMdParser.Parse(content);
        if (parsed == null)
            throw new InvalidOperationException("SKILL.md 缺少有效的 frontmatter (name/description)");

        var skillDirName = Path.GetFileName(skillDir);

        var fullDescription = string.IsNullOrWhiteSpace(parsed.Body)
            ? parsed.Description
            : $"{parsed.Description}\n\n---\n{parsed.Body}";

        var inputSchema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["input"] = new Dictionary<string, string>
                {
                    ["type"] = "string",
                    ["description"] = "传递给 Skill 的输入内容"
                }
            }
        };

        return new FunctionDefinition(parsed.Name, fullDescription, inputSchema, input =>
        {
            var userInput = input.TryGetProperty("input", out var inp) ? inp.GetString() ?? "" : "";
            return Task.FromResult($"[Skill '{parsed.Name}' 已激活] 输入: {userInput}");
        });
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

        var mode = root.TryGetProperty("mode", out var modeEl) ? modeEl.GetString() ?? "command" : "command";

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

        if (mode == "http")
        {
            var httpEl = root.GetProperty("http");
            var method = httpEl.GetProperty("method").GetString() ?? "GET";
            var urlTemplate = httpEl.GetProperty("url").GetString() ?? "";

            var headerTemplates = new Dictionary<string, string>();
            if (httpEl.TryGetProperty("headers", out var headersEl) && headersEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var h in headersEl.EnumerateObject())
                {
                    headerTemplates[h.Name] = h.Value.GetString() ?? "";
                }
            }

            var bodyTemplate = httpEl.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? "" : "";

            return new FunctionDefinition(name, description, inputSchema, async input =>
            {
                return await ExecuteHttpAsync(method, urlTemplate, headerTemplates, bodyTemplate, parameters, input);
            });
        }
        else
        {
            var command = root.GetProperty("command").GetString()
                ?? throw new InvalidOperationException("Skill 缺少 command 字段");
            var commandTemplate = command;

            return new FunctionDefinition(name, description, inputSchema, input =>
            {
                var resolvedCommand = ResolveCommand(commandTemplate, parameters, input);
                return Task.FromResult(ExecuteShell(resolvedCommand));
            });
        }
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

    private static string ResolveTemplate(string template, List<SkillParameter> parameters, JsonElement input)
    {
        var result = template;

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

        return result;
    }

    private static string ResolveCommand(string commandTemplate, List<SkillParameter> parameters, JsonElement input)
    {
        var result = ResolveTemplate(commandTemplate, parameters, input);

        result = Regex.Replace(result, @"\s{2,}", " ").Trim();

        return result;
    }

    private static async Task<string> ExecuteHttpAsync(
        string method, string urlTemplate,
        Dictionary<string, string> headerTemplates, string bodyTemplate,
        List<SkillParameter> parameters, JsonElement input)
    {
        try
        {
            var url = ResolveTemplate(urlTemplate, parameters, input);
            var body = ResolveTemplate(bodyTemplate, parameters, input);

            var resolvedHeaders = new Dictionary<string, string>();
            foreach (var kv in headerTemplates)
            {
                resolvedHeaders[kv.Key] = ResolveTemplate(kv.Value, parameters, input);
            }

            var httpMethod = method.ToUpperInvariant() switch
            {
                "GET" => HttpMethod.Get,
                "POST" => HttpMethod.Post,
                "PUT" => HttpMethod.Put,
                "DELETE" => HttpMethod.Delete,
                "PATCH" => HttpMethod.Patch,
                _ => HttpMethod.Get
            };

            using var request = new HttpRequestMessage(httpMethod, url);

            var contentType = "application/json";
            foreach (var kv in resolvedHeaders)
            {
                if (kv.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = kv.Value;
                }
                else
                {
                    request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                }
            }

            if (!string.IsNullOrEmpty(body) &&
                (httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put || httpMethod == HttpMethod.Patch))
            {
                request.Content = new StringContent(body, Encoding.UTF8, contentType);
            }

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return responseBody;
            }

            return $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n{responseBody}";
        }
        catch (TaskCanceledException)
        {
            return $"HTTP 请求超时({CommandTimeoutSeconds}秒)";
        }
        catch (Exception ex)
        {
            return $"HTTP 请求失败: {ex.Message}";
        }
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

        var httpExample = new
        {
            name = "example_http_skill",
            description = "这是一个 HTTP 模式的示例 Skill，以下划线开头的文件不会被加载",
            mode = "http",
            parameters = new[]
            {
                new { name = "city", type = "string", description = "城市名称", required = true }
            },
            http = new
            {
                method = "GET",
                url = "https://api.example.com/weather/{{city}}",
                headers = new Dictionary<string, string>
                {
                    ["Accept"] = "application/json"
                },
                body = ""
            }
        };

        var httpJson = JsonSerializer.Serialize(httpExample, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(dir, "_example_http.json"), httpJson);
    }

    private class SkillParameter
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "string";
        public string Description { get; set; } = "";
        public bool Required { get; set; }
    }
}
