using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace VirgoBot.Functions;

public static class ShellFunctions
{
    private const int DefaultTimeoutSeconds = 60;
    private const int IdleTimeoutMs = 3000;
    private const int MaxOutputLength = 8000;

    public static IEnumerable<FunctionDefinition> Register()
    {
        yield return new FunctionDefinition("execute_shell", "执行Shell命令并返回结果", new
        {
            type = "object",
            properties = new
            {
                command = new { type = "string", description = "要执行的命令" },
                timeout_seconds = new { type = "integer", description = "超时时间（秒），默认60" }
            },
            required = new[] { "command" }
        }, async input =>
        {
            var cmd = input.GetProperty("command").GetString() ?? "";
            var timeout = input.TryGetProperty("timeout_seconds", out var ts) ? ts.GetInt32() : DefaultTimeoutSeconds;
            return await ExecuteShellAsync(cmd, timeout);
        });
    }

    private static async Task<string> ExecuteShellAsync(string command, int timeoutSeconds)
    {
        try
        {
            var isWindows = OperatingSystem.IsWindows();
            var outputBuffer = new StringBuilder();
            var bufferLock = new object();

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = isWindows ? "cmd.exe" : "/bin/bash",
                    Arguments = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                },
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                lock (bufferLock) { outputBuffer.AppendLine(e.Data); }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                lock (bufferLock) { outputBuffer.AppendLine("[stderr] " + e.Data); }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // 使用 idle 检测 + 最大超时，不会死等
            var maxWaitMs = timeoutSeconds * 1000;
            var stopwatch = Stopwatch.StartNew();
            var lastLength = 0;
            var idleStart = stopwatch.ElapsedMilliseconds;

            while (stopwatch.ElapsedMilliseconds < maxWaitMs)
            {
                await Task.Delay(100);

                if (process.HasExited)
                {
                    await Task.Delay(200); // 等待最后的输出刷新
                    break;
                }

                int currentLength;
                lock (bufferLock) { currentLength = outputBuffer.Length; }

                if (currentLength != lastLength)
                {
                    lastLength = currentLength;
                    idleStart = stopwatch.ElapsedMilliseconds;
                }
                else if (currentLength > 0 && stopwatch.ElapsedMilliseconds - idleStart >= IdleTimeoutMs)
                {
                    // 有输出但已经静默 3 秒 — 可能在等待输入，先返回已有输出
                    break;
                }
            }

            var timedOut = !process.HasExited;
            if (timedOut)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
            }

            string result;
            lock (bufferLock) { result = outputBuffer.ToString(); }

            if (result.Length > MaxOutputLength)
                result = result[..MaxOutputLength] + $"\n...[输出已截断，共 {result.Length} 字符]";

            if (timedOut)
            {
                var elapsed = stopwatch.ElapsedMilliseconds;
                if (elapsed >= maxWaitMs)
                    result += $"\n[命令执行超时({timeoutSeconds}秒)，进程已终止]";
                else
                    result += $"\n[命令输出静默超过{IdleTimeoutMs / 1000}秒，可能在等待输入，进程已终止]";
            }

            process.Dispose();
            return string.IsNullOrWhiteSpace(result) ? "(无输出)" : result;
        }
        catch (Exception ex)
        {
            return $"执行失败: {ex.Message}";
        }
    }
}
