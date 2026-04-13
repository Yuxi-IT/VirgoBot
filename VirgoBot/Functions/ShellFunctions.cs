using System.Diagnostics;
using System.Text.Json;

namespace VirgoBot.Functions;

public static class ShellFunctions
{
    public static IEnumerable<FunctionDefinition> Register()
    {
        yield return new FunctionDefinition("execute_shell", "执行Shell命令并返回结果", new
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
            return Task.FromResult(ExecuteShell(cmd));
        });
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
            process.WaitForExit();

            return string.IsNullOrEmpty(error) ? output : $"{output}\n错误: {error}";
        }
        catch (Exception ex)
        {
            return $"执行失败: {ex.Message}";
        }
    }
}
