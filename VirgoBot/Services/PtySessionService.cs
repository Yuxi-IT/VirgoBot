using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Porta.Pty;
using VirgoBot.Utilities;

namespace VirgoBot.Services;

public class PtySessionService : IDisposable
{
    private static readonly Regex AnsiEscapeRegex = new(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])", RegexOptions.Compiled);
    private static readonly int MaxOutputChars = 8000;

    /// <summary>
    /// Strip ANSI escape sequences and trim output to a reasonable size for LLM consumption.
    /// </summary>
    private static string SanitizeOutput(string raw)
    {
        var clean = AnsiEscapeRegex.Replace(raw, "");
        // Remove common terminal control sequences that survive the regex
        clean = clean.Replace("\r", "");
        if (clean.Length > MaxOutputChars)
            clean = clean[^MaxOutputChars..] + $"\n... (输出截断, 共 {clean.Length} 字符)";
        return clean.Trim();
    }
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromSeconds(60);
    private const int DefaultCols = 120;
    private const int DefaultRows = 30;
    private const int ReadBufferSize = 4096;

    private readonly ConcurrentDictionary<string, PtySessionState> _sessions = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public PtySessionService()
    {
        _cleanupTimer = new Timer(CleanupIdleSessions, null, CleanupInterval, CleanupInterval);
    }

    public async Task<string> CreateAsync(string shellType = "auto", int cols = DefaultCols, int rows = DefaultRows)
    {
        var sessionId = Guid.NewGuid().ToString("N")[..8];
        var app = ResolveShell(shellType);

        var pty = await PtyProvider.SpawnAsync(new PtyOptions
        {
            App = app,
            Cols = cols,
            Rows = rows,
            Cwd = Environment.CurrentDirectory
        }, CancellationToken.None);

        var cts = new CancellationTokenSource();
        var state = new PtySessionState
        {
            Pty = pty,
            Cts = cts,
            ShellType = shellType,
            Cols = cols,
            Rows = rows
        };

        _sessions[sessionId] = state;

        // Start background read loop
        _ = Task.Run(() => ReadLoopAsync(sessionId, state, cts.Token), cts.Token);

        // Wait for initial prompt and discard it for a clean session
        await Task.Delay(600);
        lock (state.BufferLock) { state.OutputBuffer.Clear(); }

        ColorLog.Info("PTY", $"已创建终端会话 {sessionId} (类型: {shellType}, {cols}x{rows})");
        return sessionId;
    }

    /// <summary>
    /// Write input to a PTY session and wait for output with idle detection.
    /// Tracks buffer position instead of clearing, so no data is lost to race conditions.
    /// </summary>
    public async Task<string> WriteAndWaitAsync(string sessionId, string input, int idleTimeoutMs = 3000, int maxWaitMs = 60000)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
            return $"错误: 会话 {sessionId} 不存在";

        if (state.Pty == null)
            return $"错误: 会话 {sessionId} 已关闭";

        // Track starting position — never clear, only read delta
        int startLength;
        lock (state.BufferLock) { startLength = state.OutputBuffer.Length; }

        // Write input
        state.LastActivity = DateTime.UtcNow;
        var bytes = Encoding.UTF8.GetBytes(input);
        await state.Pty.WriterStream.WriteAsync(bytes, 0, bytes.Length);
        await state.Pty.WriterStream.FlushAsync();

        // Poll for new output with idle detection
        var sw = Stopwatch.StartNew();
        var lastLength = startLength;
        var idleStart = sw.ElapsedMilliseconds;

        while (sw.ElapsedMilliseconds < maxWaitMs)
        {
            await Task.Delay(200);

            int currentLength;
            lock (state.BufferLock) { currentLength = state.OutputBuffer.Length; }

            if (currentLength != lastLength)
            {
                lastLength = currentLength;
                idleStart = sw.ElapsedMilliseconds;
            }
            else if (currentLength > startLength && sw.ElapsedMilliseconds - idleStart >= idleTimeoutMs)
            {
                break; // Output stabilized
            }
        }

        state.LastActivity = DateTime.UtcNow;

        // Extract only new data — don't clear (background loop may still be writing)
        string result;
        lock (state.BufferLock)
        {
            var newLength = state.OutputBuffer.Length - startLength;
            if (newLength <= 0) return "(无输出)";

            result = SanitizeOutput(state.OutputBuffer.ToString(startLength, newLength));

            // Trim buffer periodically to prevent unbounded growth
            if (state.OutputBuffer.Length > 200_000)
                state.OutputBuffer.Remove(0, state.OutputBuffer.Length / 2);
        }

        return string.IsNullOrWhiteSpace(result) ? "(无输出)" : result;
    }

    public async Task WriteAsync(string sessionId, string input)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
            throw new InvalidOperationException($"会话 {sessionId} 不存在");

        if (state.Pty == null)
            throw new InvalidOperationException($"会话 {sessionId} 已关闭");

        state.LastActivity = DateTime.UtcNow;

        var bytes = Encoding.UTF8.GetBytes(input);
        await state.Pty.WriterStream.WriteAsync(bytes, 0, bytes.Length);
        await state.Pty.WriterStream.FlushAsync();
    }

    public string ReadOutput(string sessionId, bool clear = true)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
            return $"错误: 会话 {sessionId} 不存在";

        string result;
        lock (state.BufferLock)
        {
            result = state.OutputBuffer.ToString();
            if (clear)
                state.OutputBuffer.Clear();
        }

        return result;
    }

    public string ReadSnapshot(string sessionId)
    {
        return ReadOutput(sessionId, clear: false);
    }

    public async Task CloseAsync(string sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out var state))
            return;

        await DisposeSessionAsync(state);
        ColorLog.Info("PTY", $"已关闭终端会话 {sessionId}");
    }

    public IReadOnlyList<PtySessionInfo> ListSessions()
    {
        return _sessions.Select(kvp => new PtySessionInfo
        {
            SessionId = kvp.Key,
            ShellType = kvp.Value.ShellType,
            Cols = kvp.Value.Cols,
            Rows = kvp.Value.Rows,
            LastActivity = kvp.Value.LastActivity,
            CreatedAt = kvp.Value.CreatedAt
        }).ToList();
    }

    public async Task ResizeAsync(string sessionId, int cols, int rows)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
            throw new InvalidOperationException($"会话 {sessionId} 不存在");

        if (state.Pty != null)
        {
            state.Pty.Resize(cols, rows);
            state.Cols = cols;
            state.Rows = rows;
        }

        await Task.CompletedTask;
    }

    private async Task ReadLoopAsync(string sessionId, PtySessionState state, CancellationToken ct)
    {
        var buffer = new byte[ReadBufferSize];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var bytesRead = await state.Pty!.ReaderStream.ReadAsync(buffer, 0, buffer.Length, ct);
                if (bytesRead == 0) break; // EOF - process exited

                var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                lock (state.BufferLock)
                {
                    state.OutputBuffer.Append(text);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
        catch (Exception ex)
        {
            ColorLog.Debug("PTY", $"读取循环异常 {sessionId}: {ex.Message}");
        }
    }

    private static async Task DisposeSessionAsync(PtySessionState state)
    {
        try
        {
            state.Cts?.Cancel();
            state.Cts?.Dispose();
            state.Cts = null;

            if (state.Pty != null)
            {
                await Task.Delay(100);
                state.Pty.Dispose();
                state.Pty = null;
            }
        }
        catch { }
    }

    private void CleanupIdleSessions(object? _)
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _sessions)
        {
            if ((now - kvp.Value.LastActivity) > IdleTimeout)
            {
                if (_sessions.TryRemove(kvp.Key, out var removed))
                {
                    _ = DisposeSessionAsync(removed);
                    ColorLog.Info("PTY", $"已自动清理空闲终端 {kvp.Key}");
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cleanupTimer.Dispose();

        foreach (var kvp in _sessions)
        {
            if (_sessions.TryRemove(kvp.Key, out var state))
            {
                try
                {
                    state.Cts?.Cancel();
                    state.Cts?.Dispose();
                    state.Pty?.Dispose();
                }
                catch { }
            }
        }
    }

    private static string ResolveShell(string shellType)
    {
        return shellType.ToLowerInvariant() switch
        {
            "powershell" => "powershell.exe",
            "cmd" => "cmd.exe",
            "bash" => "bash",
            _ => OperatingSystem.IsWindows() ? "powershell.exe" : "bash"
        };
    }

    private sealed class PtySessionState
    {
        public IPtyConnection? Pty { get; set; }
        public StringBuilder OutputBuffer { get; } = new();
        public object BufferLock { get; } = new();
        public CancellationTokenSource? Cts { get; set; }
        public string ShellType { get; set; } = "";
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int Cols { get; set; } = DefaultCols;
        public int Rows { get; set; } = DefaultRows;
    }
}

public class PtySessionInfo
{
    public string SessionId { get; set; } = "";
    public string ShellType { get; set; } = "";
    public int Cols { get; set; }
    public int Rows { get; set; }
    public DateTime LastActivity { get; set; }
    public DateTime CreatedAt { get; set; }
}
