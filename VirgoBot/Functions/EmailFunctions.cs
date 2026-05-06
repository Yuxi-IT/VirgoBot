using System.Text.Json;
using VirgoBot.Features.Email;

namespace VirgoBot.Functions;

public static class EmailFunctions
{
    public static IEnumerable<FunctionDefinition> Register(EmailService emailService)
    {
        yield return new FunctionDefinition("send_email", "Send an email", new
        {
            type = "object",
            properties = new
            {
                to = new { type = "string", description = "Recipient email address" },
                subject = new { type = "string", description = "Email subject" },
                body = new { type = "string", description = "Email body content" }
            },
            required = new[] { "to", "subject", "body" }
        }, async input =>
        {
            var to = input.GetProperty("to").GetString() ?? "";
            var subject = input.GetProperty("subject").GetString() ?? "";
            var body = input.GetProperty("body").GetString() ?? "";
            await emailService.SendEmailAsync(to, subject, body);
            return "Email sent successfully";
        });
    }
}
