using System.Text.Json;
using VirgoBot.Services;

namespace VirgoBot.Functions;

public static class TerminalFunctions
{
    public static IEnumerable<FunctionDefinition> Register(PtySessionService ptyService)
    {
        yield return new FunctionDefinition("terminal_create", "创建一个新的终端会话（PTY），支持交互式程序（REPL、vim等）", new
        {
            type = "object",
            properties = new
            {
                shell_type = new
                {
                    type = "string",
                    description = "Shell类型: auto(自动选择), powershell, cmd, bash",
                    @enum = new[] { "auto", "powershell", "cmd", "bash" }
                },
                cols = new { type = "integer", description = "终端列数，默认120" },
                rows = new { type = "integer", description = "终端行数，默认30" }
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
                return $"终端会话已创建，session_id: {sessionId}";
            }
            catch (Exception ex)
            {
                return $"创建终端会话失败: {ex.Message}";
            }
        });

        yield return new FunctionDefinition("terminal_write", "向终端会话发送一条命令并自动等待输出结果。注意: 必须一次发送完整的命令(包括必要时的换行符)，不要逐字符发送。发送后会自动等待命令执行完毕并返回清洗后的输出(ANSI转义码会被自动清除)", new
        {
            type = "object",
            properties = new
            {
                session_id = new { type = "string", description = "会话ID" },
                input = new { type = "string", description = "要执行的完整命令, 不需要带换行符(会自动追加)。例如: ipconfig, dir, ping baidu.com" }
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
                return $"写入失败: {ex.Message}";
            }
        });

        yield return new FunctionDefinition("terminal_close", "关闭终端会话并释放资源", new
        {
            type = "object",
            properties = new
            {
                session_id = new { type = "string", description = "要关闭的会话ID" }
            },
            required = new[] { "session_id" }
        }, async input =>
        {
            var sessionId = input.GetProperty("session_id").GetString() ?? "";
            try
            {
                await ptyService.CloseAsync(sessionId);
                return $"终端会话 {sessionId} 已关闭";
            }
            catch (Exception ex)
            {
                return $"关闭失败: {ex.Message}";
            }
        });

        yield return new FunctionDefinition("terminal_list", "列出所有活跃的终端会话", new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        }, _ =>
        {
            var sessions = ptyService.ListSessions();
            if (sessions.Count == 0)
                return Task.FromResult("当前没有活跃的终端会话");

            var lines = sessions.Select(s =>
                $"  {s.SessionId}: {s.ShellType} ({s.Cols}x{s.Rows}), 最后活动: {s.LastActivity:HH:mm:ss}");
            return Task.FromResult("活跃终端会话:\n" + string.Join('\n', lines));
        });

        yield return new FunctionDefinition("terminal_resize", "调整终端会话的窗口大小", new
        {
            type = "object",
            properties = new
            {
                session_id = new { type = "string", description = "会话ID" },
                cols = new { type = "integer", description = "新的列数" },
                rows = new { type = "integer", description = "新的行数" }
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
                return $"终端 {sessionId} 已调整为 {cols}x{rows}";
            }
            catch (Exception ex)
            {
                return $"调整大小失败: {ex.Message}";
            }
        });
    }
}
