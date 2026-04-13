using System.Text;
using System.Text.Json;
using VirgoBot.Services;

namespace VirgoBot.Functions;

public static class ContactFunctions
{
    public static IEnumerable<FunctionDefinition> Register(ContactService contactService)
    {
        yield return new FunctionDefinition("add_contact", "添加联系人", new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string", description = "姓名" },
                email = new { type = "string", description = "邮箱(可选)" },
                phone = new { type = "string", description = "电话(可选)" },
                notes = new { type = "string", description = "备注(可选)" }
            },
            required = new[] { "name" }
        }, async input =>
        {
            var name = input.GetProperty("name").GetString() ?? "";
            var email = input.TryGetProperty("email", out var e) ? e.GetString() : null;
            var phone = input.TryGetProperty("phone", out var p) ? p.GetString() : null;
            var notes = input.TryGetProperty("notes", out var n) ? n.GetString() : null;
            contactService.AddContact(name, email, phone, notes);
            return "联系人添加成功";
        });

        yield return new FunctionDefinition("list_contacts", "查看所有联系人", new
        {
            type = "object",
            properties = new { }
        }, async input =>
        {
            var contacts = contactService.GetAllContacts();
            if (contacts.Count == 0) return "通讯录为空";
            var sb = new StringBuilder();
            foreach (var c in contacts)
            {
                sb.AppendLine($"[{c.Id}] {c.Name}");
                if (!string.IsNullOrEmpty(c.Email)) sb.AppendLine($"  邮箱: {c.Email}");
                if (!string.IsNullOrEmpty(c.Phone)) sb.AppendLine($"  电话: {c.Phone}");
                if (!string.IsNullOrEmpty(c.Notes)) sb.AppendLine($"  备注: {c.Notes}");
            }
            return sb.ToString();
        });

        yield return new FunctionDefinition("find_contact", "搜索联系人", new
        {
            type = "object",
            properties = new
            {
                keyword = new { type = "string", description = "搜索关键词" }
            },
            required = new[] { "keyword" }
        }, async input =>
        {
            var keyword = input.GetProperty("keyword").GetString() ?? "";
            var contact = contactService.FindContact(keyword);
            if (contact == null) return "未找到联系人";
            var sb = new StringBuilder();
            sb.AppendLine($"[{contact.Id}] {contact.Name}");
            if (!string.IsNullOrEmpty(contact.Email)) sb.AppendLine($"邮箱: {contact.Email}");
            if (!string.IsNullOrEmpty(contact.Phone)) sb.AppendLine($"电话: {contact.Phone}");
            if (!string.IsNullOrEmpty(contact.Notes)) sb.AppendLine($"备注: {contact.Notes}");
            return sb.ToString();
        });

        yield return new FunctionDefinition("update_contact", "修改联系人信息", new
        {
            type = "object",
            properties = new
            {
                id = new { type = "number", description = "联系人ID" },
                name = new { type = "string", description = "新姓名(可选)" },
                email = new { type = "string", description = "新邮箱(可选)" },
                phone = new { type = "string", description = "新电话(可选)" },
                notes = new { type = "string", description = "新备注(可选)" }
            },
            required = new[] { "id" }
        }, async input =>
        {
            var id = input.GetProperty("id").GetInt32();
            var name = input.TryGetProperty("name", out var n) ? n.GetString() : null;
            var email = input.TryGetProperty("email", out var e) ? e.GetString() : null;
            var phone = input.TryGetProperty("phone", out var p) ? p.GetString() : null;
            var notes = input.TryGetProperty("notes", out var nt) ? nt.GetString() : null;
            contactService.UpdateContact(id, name, email, phone, notes);
            return "联系人更新成功";
        });

        yield return new FunctionDefinition("delete_contact", "删除联系人", new
        {
            type = "object",
            properties = new
            {
                id = new { type = "number", description = "联系人ID" }
            },
            required = new[] { "id" }
        }, async input =>
        {
            var id = input.GetProperty("id").GetInt32();
            contactService.DeleteContact(id);
            return "联系人删除成功";
        });
    }
}
