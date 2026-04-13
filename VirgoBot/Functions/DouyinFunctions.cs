using System.Text.Json;

namespace VirgoBot.Functions;

public static class DouyinFunctions
{
    public static IEnumerable<FunctionDefinition> Register()
    {
        yield return new FunctionDefinition("switch_douyin_chat", "切换抖音聊天到指定用户", new
        {
            type = "object",
            properties = new
            {
                username = new { type = "string", description = "要切换到的用户名" }
            },
            required = new[] { "username" }
        }, async input =>
        {
            var username = input.GetProperty("username").GetString() ?? "";
            return $"switch_chat:{username}";
        });
    }
}
