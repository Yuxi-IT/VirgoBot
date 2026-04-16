using System.Collections.Concurrent;
using VirgoBot.Utilities;

namespace VirgoBot.Services;

public class ShellSessionService : IDisposable
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, ShellSession> _sessions = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public ShellSessionService()
    {
        _cleanupTimer = new Timer(CleanupIdleSessions, null, CleanupInterval, CleanupInterval);
    }

    public string CreateSession(string shellType = "auto")
    {
        var sessionId = Guid.NewGuid().ToString("N")[..8];
        var session = new ShellSession(shellType);
        _sessions[sessionId] = session;
        ColorLog.Info("SHELL", $"已创建会话 {sessionId} (类型: {session.ShellType})");
        return sessionId;
    }

    public async Task<string> SendInputAsync(string sessionId, string input, int idleTimeoutMs = 2000, int maxWaitMs = 30000)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return $"错误: 会话 {sessionId} 不存在";

        if (session.HasExited)
        {
            _sessions.TryRemove(sessionId, out _);
            session.Dispose();
            return $"错误: 会话 {sessionId} 已退出 (退出码: {session.ExitCode})";
        }

        return await session.SendAndReadAsync(input, idleTimeoutMs, maxWaitMs);
    }

    public string CloseSession(string sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out var session))
            return $"错误: 会话 {sessionId} 不存在";

        session.Dispose();
        ColorLog.Info("SHELL", $"已关闭会话 {sessionId}");
        return $"会话 {sessionId} 已关闭";
    }

    public IReadOnlyList<ShellSessionInfo> ListSessions()
    {
        return _sessions.Select(kvp => new ShellSessionInfo
        {
            SessionId = kvp.Key,
            ShellType = kvp.Value.ShellType,
            HasExited = kvp.Value.HasExited,
            LastActivity = kvp.Value.LastActivity
        }).ToList();
    }

    private void CleanupIdleSessions(object? state)
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _sessions)
        {
            var session = kvp.Value;
            var isIdle = (now - session.LastActivity) > IdleTimeout;

            if (isIdle || session.HasExited)
            {
                if (_sessions.TryRemove(kvp.Key, out var removed))
                {
                    removed.Dispose();
                    ColorLog.Info("SHELL", $"已自动清理会话 {kvp.Key} ({(isIdle ? "空闲超时" : "进程已退出")})");
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
            if (_sessions.TryRemove(kvp.Key, out var session))
            {
                session.Dispose();
            }
        }
    }
}

public class ShellSessionInfo
{
    public string SessionId { get; set; } = "";
    public string ShellType { get; set; } = "";
    public bool HasExited { get; set; }
    public DateTime LastActivity { get; set; }
}
