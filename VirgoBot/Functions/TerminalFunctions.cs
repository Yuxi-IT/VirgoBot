using System.Text.Json;
using VirgoBot.Services;

namespace VirgoBot.Functions;

public static class TerminalFunctions
{
    public static IEnumerable<FunctionDefinition> Register(PtySessionService ptyService)
    {
        yield return new FunctionDefinition("terminal_create", "Create a new terminal session (PTY), supports interactive programs (REPL, vim, etc.)", new
        {
            type = "object",
            properties = new
            {
                shell_type = new
                {
                    type = "string",
                    description = "Shell type: auto, powershell, cmd, bash",
                    @enum = new[] { "auto", "powershell", "cmd", "bash" }
                },
                cols = new { type = "integer", description = "Terminal columns, default 120" },
                rows = new { type = "integer", description = "Terminal rows, default 30" }
            },
            required = Array.Empty<string>()
        }, async input =>
        {
            var shellType = input.TryGetProperty("shell_type", out var st)
                ? st.GetString() ?? "auto"
                : "auto";
            var cols = input.TryGetProperty("cols", out var c) ? c.GetInt32() : 120;
            var rows = input.TryGetProperty("rows", out var r) ? r.GetInt32() : 30;

            try
            {
                var sessionId = await ptyService.CreateAsync(shellType, cols, rows);
                return $"Terminal session created, session_id: {sessionId}";
            }
            catch (Exception ex)
            {
                return $"Failed to create terminal session: {ex.Message}";
            }
        });

        yield return new FunctionDefinition("terminal_write", "Send a command to a terminal session and automatically wait for output. Note: Send the complete command at once (including newlines if needed), do not send character by character. After sending, it automatically waits for command completion and returns cleaned output (ANSI escape codes are automatically stripped).", new
        {
            type = "object",
            properties = new
            {
                session_id = new { type = "string", description = "Session ID" },
                input = new { type = "string", description = "The complete command to execute (no trailing newline needed, it will be auto-appended). Example: ipconfig, dir, ping example.com" }
            },
            required = new[] { "session_id", "input" }
        }, async input =>
        {
            var sessionId = input.GetProperty("session_id").GetString() ?? "";
            var text = input.GetProperty("input").GetString() ?? "";

            try
            {
                // Ensure Windows line endings for PTY
                if (!text.Contains('\r'))
                    text = text.Replace("\n", "\r\n");
                if (!text.EndsWith('\n'))
                    text += "\r\n";

                return await ptyService.WriteAndWaitAsync(sessionId, text);
            }
            catch (Exception ex)
            {
                return $"Write failed: {ex.Message}";
            }
        });

        yield return new FunctionDefinition("terminal_close", "Close terminal session and release resources", new
        {
            type = "object",
            properties = new
            {
                session_id = new { type = "string", description = "Session ID to close" }
            },
            required = new[] { "session_id" }
        }, async input =>
        {
            var sessionId = input.GetProperty("session_id").GetString() ?? "";
            try
            {
                await ptyService.CloseAsync(sessionId);
                return $"Terminal session {sessionId} closed";
            }
            catch (Exception ex)
            {
                return $"Close failed: {ex.Message}";
            }
        });

        yield return new FunctionDefinition("terminal_list", "List all active terminal sessions", new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        }, _ =>
        {
            var sessions = ptyService.ListSessions();
            if (sessions.Count == 0)
                return Task.FromResult("No active terminal sessions");

            var lines = sessions.Select(s =>
                $"  {s.SessionId}: {s.ShellType} ({s.Cols}x{s.Rows}), last activity: {s.LastActivity:HH:mm:ss}");
            return Task.FromResult("Active terminal sessions:\n" + string.Join('\n', lines));
        });

        yield return new FunctionDefinition("terminal_resize", "Resize terminal session window", new
        {
            type = "object",
            properties = new
            {
                session_id = new { type = "string", description = "Session ID" },
                cols = new { type = "integer", description = "New column count" },
                rows = new { type = "integer", description = "New row count" }
            },
            required = new[] { "session_id", "cols", "rows" }
        }, async input =>
        {
            var sessionId = input.GetProperty("session_id").GetString() ?? "";
            var cols = input.GetProperty("cols").GetInt32();
            var rows = input.GetProperty("rows").GetInt32();

            try
            {
                await ptyService.ResizeAsync(sessionId, cols, rows);
                return $"Terminal {sessionId} resized to {cols}x{rows}";
            }
            catch (Exception ex)
            {
                return $"Resize failed: {ex.Message}";
            }
        });
    }
}
