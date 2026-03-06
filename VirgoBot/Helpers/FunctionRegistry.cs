using System.Diagnostics;
using System.Text.Json;

namespace VirgoBot.Helpers;

public class FunctionRegistry
{
    private readonly Dictionary<string, Func<JsonElement, string>> _functions = new();
    private readonly List<object> _toolSchemas = new();

    public FunctionRegistry()
    {
        RegisterDefaultFunctions();
    }

    private void RegisterDefaultFunctions()
    {
        Register("get_time", "获取当前服务器时间", new { type = "object", properties = new { } }, _ =>
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        Register("get_bot_info", "获取机器人信息", new { type = "object", properties = new { } }, _ =>
            "Telegram Claude Bot v1.0");

        Register("execute_shell", "执行 Shell 命令并返回结果", new
        {
            type = "object",
            properties = new
            {
                command = new { type = "string", description = "要执行的命令" }
            },
            required = new[] { "command" }
        }, input =>
        {
            var cmd = input.GetProperty("command").GetString() ?? "";
            return ExecuteShell(cmd);
        });

        Register("read_file", "读取文件内容", new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "文件路径" }
            },
            required = new[] { "path" }
        }, input =>
        {
            var path = input.GetProperty("path").GetString() ?? "";
            return File.Exists(path) ? File.ReadAllText(path) : "文件不存在";
        });

        Register("write_file", "写入文件内容", new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "文件路径" },
                content = new { type = "string", description = "文件内容" }
            },
            required = new[] { "path", "content" }
        }, input =>
        {
            var path = input.GetProperty("path").GetString() ?? "";
            var content = input.GetProperty("content").GetString() ?? "";
            File.WriteAllText(path, content);
            return "写入成功";
        });

        Register("list_files", "列出目录下的文件", new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "目录路径" }
            },
            required = new[] { "path" }
        }, input =>
        {
            var path = input.GetProperty("path").GetString() ?? ".";
            if (!Directory.Exists(path)) return "目录不存在";
            var files = Directory.GetFileSystemEntries(path);
            return string.Join("\n", files);
        });
    }

    public void Register(string name, string description, object inputSchema, Func<JsonElement, string> handler)
    {
        _functions[name] = handler;
        _toolSchemas.Add(new { name, description, input_schema = inputSchema });
    }

    public string Execute(string name, JsonElement input)
    {
        return _functions.ContainsKey(name) ? _functions[name](input) : "unknown tool";
    }

    public object[] GetToolSchemas() => _toolSchemas.ToArray();

    private string ExecuteShell(string command)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return string.IsNullOrEmpty(error) ? output : $"{output}\n错误: {error}";
        }
        catch (Exception ex)
        {
            return $"执行失败: {ex.Message}";
        }
    }
}
