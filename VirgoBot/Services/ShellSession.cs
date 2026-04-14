using System.Diagnostics;
using System.Text;

namespace VirgoBot.Services;

/// <summary>
/// 单个持久交互式 Shell 会话，封装一个长期运行的进程。
/// </summary>
public class ShellSession : IDisposable
{
    private const int MaxOutputLength = 8000;

    private readonly Process _process;
    private readonly StringBuilder _outputBuffer = new();
    private readonly object _bufferLock = new();
    private bool _disposed;

    public string ShellType { get; }
    public DateTime LastActivity { get; private set; } = DateTime.UtcNow;
    public bool HasExited => _process.HasExited;
    public int? ExitCode => _process.HasExited ? _process.ExitCode : null;

    public ShellSession(string shellType)
    {
        ShellType = shellType;

        var (fileName, arguments) = GetShellCommand(shellType);
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            },
            EnableRaisingEvents = true
        };

        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (_bufferLock) { _outputBuffer.AppendLine(e.Data); }
        };

        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (_bufferLock) { _outputBuffer.AppendLine("[stderr] " + e.Data); }
        };

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    /// <summary>
    /// 发送输入到 stdin，然后轮询等待输出稳定后返回结果。
    /// </summary>
    public async Task<string> SendAndReadAsync(string input, int idleTimeoutMs = 2000, int maxWaitMs = 30000)
    {
        if (_disposed || _process.HasExited)
            return "[会话已结束]";

        LastActivity = DateTime.UtcNow;

        // 清空缓冲区
        lock (_bufferLock) { _outputBuffer.Clear(); }

        // 写入 stdin
        try
        {
            await _process.StandardInput.WriteLineAsync(input);
            await _process.StandardInput.FlushAsync();
        }
        catch (Exception ex)
        {
            return $"[写入失败: {ex.Message}]";
        }

        // 轮询等待输出稳定
        var stopwatch = Stopwatch.StartNew();
        var lastLength = 0;
        var idleStart = stopwatch.ElapsedMilliseconds;

        while (stopwatch.ElapsedMilliseconds < maxWaitMs)
        {
            await Task.Delay(100);

            int currentLength;
            lock (_bufferLock) { currentLength = _outputBuffer.Length; }

            if (currentLength != lastLength)
            {
                // 有新输出，重置空闲计时
                lastLength = currentLength;
                idleStart = stopwatch.ElapsedMilliseconds;
            }
            else if (stopwatch.ElapsedMilliseconds - idleStart >= idleTimeoutMs)
            {
                // 空闲超时，输出稳定
                break;
            }

            // 进程已退出且无新输出
            if (_process.HasExited)
            {
                await Task.Delay(200); // 等待最后的输出刷新
                break;
            }
        }

        LastActivity = DateTime.UtcNow;

        // 读取并截断输出
        string result;
        lock (_bufferLock) { result = _outputBuffer.ToString(); }

        if (result.Length > MaxOutputLength)
        {
            result = result[..MaxOutputLength] + $"\n...[输出已截断，共 {result.Length} 字符]";
        }

        return string.IsNullOrWhiteSpace(result) ? "(无输出)" : result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (!_process.HasExited)
            {
                // 关闭 stdin 触发进程优雅退出
                try { _process.StandardInput.Close(); } catch { /* ignore */ }

                if (!_process.WaitForExit(3000))
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
        }
        catch { /* ignore cleanup errors */ }
        finally
        {
            _process.Dispose();
        }
    }

    private static (string fileName, string arguments) GetShellCommand(string shellType)
    {
        return shellType.ToLowerInvariant() switch
        {
            "powershell" => ("powershell", "-NoLogo -NoProfile -Command -"),
            "cmd" => ("cmd.exe", "/Q /K"),
            "bash" => ("bash", "--norc -i"),
            _ => OperatingSystem.IsWindows()
                ? ("powershell", "-NoLogo -NoProfile -Command -")
                : ("bash", "--norc -i")
        };
    }
}
