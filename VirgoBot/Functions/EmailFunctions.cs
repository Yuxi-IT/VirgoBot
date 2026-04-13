using System.Text.Json;
using VirgoBot.Features.Email;

namespace VirgoBot.Functions;

public static class EmailFunctions
{
    public static IEnumerable<FunctionDefinition> Register(EmailService emailService)
    {
        yield return new FunctionDefinition("send_email", "发送邮件", new
        {
            type = "object",
            properties = new
            {
                to = new { type = "string", description = "收件人邮箱地址" },
                subject = new { type = "string", description = "邮件主题" },
                body = new { type = "string", description = "邮件正文内容" }
            },
            required = new[] { "to", "subject", "body" }
        }, async input =>
        {
            var to = input.GetProperty("to").GetString() ?? "";
            var subject = input.GetProperty("subject").GetString() ?? "";
            var body = input.GetProperty("body").GetString() ?? "";
            await emailService.SendEmailAsync(to, subject, body);
            return "邮件发送成功";
        });
    }
}
