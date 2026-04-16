using System.Text.Json;
using VirgoBot.Services;

namespace VirgoBot.Functions;

public static class InteractiveShellFunctions
{
    public static IEnumerable<FunctionDefinition> Register(ShellSessionService shellSessionService)
    {
        yield return new FunctionDefinition("create_shell", "创建持久交互式Shell会话，支持多步交互操作（如REPL）", new
        {
            type = "object",
            properties = new
            {
                shell_type = new
                {
                    type = "string",
                    description = "Shell类型: auto(自动选择), powershell, cmd, bash",
                    @enum = new[] { "auto", "powershell", "cmd", "bash" }
                }
            },
            required = Array.Empty<string>()
        }, input =>
        {
            var shellType = input.TryGetProperty("shell_type", out var st)
                ? st.GetString() ?? "auto"
                : "auto";

            try
            {
                var sessionId = shellSessionService.CreateSession(shellType);
                return Task.FromResult($"会话已创建，session_id: {sessionId}");
            }
            catch (Exception ex)
            {
                return Task.FromResult($"创建会话失败: {ex.Message}");
            }
        });

        yield return new FunctionDefinition("shell_input", "向持久Shell会话发送输入并等待输出返回", new
        {
            type = "object",
            properties = new
            {
                session_id = new { type = "string", description = "会话ID" },
                input = new { type = "string", description = "要发送到Shell的输入内容" },
                idle_timeout_ms = new
                {
                    type = "integer",
                    description = "无新输出持续多久（毫秒）后判定输出稳定，默认2000"
                },
                max_wait_ms = new
                {
                    type = "integer",
                    description = "最大等待时间（毫秒），默认30000"
                }
            },
            required = new[] { "session_id", "input" }
        }, async input =>
        {
            var sessionId = input.GetProperty("session_id").GetString() ?? "";
            var text = input.GetProperty("input").GetString() ?? "";

            var idleTimeoutMs = input.TryGetProperty("idle_timeout_ms", out var it)
                ? it.GetInt32()
                : 2000;

            var maxWaitMs = input.TryGetProperty("max_wait_ms", out var mw)
                ? mw.GetInt32()
                : 30000;

            return await shellSessionService.SendInputAsync(sessionId, text, idleTimeoutMs, maxWaitMs);
        });

        yield return new FunctionDefinition("close_shell", "关闭持久Shell会话并释放资源", new
        {
            type = "object",
            properties = new
            {
                session_id = new { type = "string", description = "要关闭的会话ID" }
            },
            required = new[] { "session_id" }
        }, input =>
        {
            var sessionId = input.GetProperty("session_id").GetString() ?? "";
            return Task.FromResult(shellSessionService.CloseSession(sessionId));
        });
    }
}
