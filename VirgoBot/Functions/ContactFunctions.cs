using System.Text;
using System.Text.Json;
using VirgoBot.Services;

namespace VirgoBot.Functions;

public static class ContactFunctions
{
    public static IEnumerable<FunctionDefinition> Register(ContactService contactService)
    {
        yield return new FunctionDefinition("add_contact", "Add a contact", new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string", description = "Name" },
                email = new { type = "string", description = "Email (optional)" },
                phone = new { type = "string", description = "Phone (optional)" },
                notes = new { type = "string", description = "Notes (optional)" }
            },
            required = new[] { "name" }
        }, async input =>
        {
            var name = input.GetProperty("name").GetString() ?? "";
            var email = input.TryGetProperty("email", out var e) ? e.GetString() : null;
            var phone = input.TryGetProperty("phone", out var p) ? p.GetString() : null;
            var notes = input.TryGetProperty("notes", out var n) ? n.GetString() : null;
            contactService.AddContact(name, email, phone, notes);
            return "Contact added successfully";
        });

        yield return new FunctionDefinition("list_contacts", "List all contacts", new
        {
            type = "object",
            properties = new { }
        }, async input =>
        {
            var contacts = contactService.GetAllContacts();
            if (contacts.Count == 0) return "Address book is empty";
            var sb = new StringBuilder();
            foreach (var c in contacts)
            {
                sb.AppendLine($"[{c.Id}] {c.Name}");
                if (!string.IsNullOrEmpty(c.Email)) sb.AppendLine($"  Email: {c.Email}");
                if (!string.IsNullOrEmpty(c.Phone)) sb.AppendLine($"  Phone: {c.Phone}");
                if (!string.IsNullOrEmpty(c.Notes)) sb.AppendLine($"  Notes: {c.Notes}");
            }
            return sb.ToString();
        });

        yield return new FunctionDefinition("find_contact", "Search contacts", new
        {
            type = "object",
            properties = new
            {
                keyword = new { type = "string", description = "Search keyword" }
            },
            required = new[] { "keyword" }
        }, async input =>
        {
            var keyword = input.GetProperty("keyword").GetString() ?? "";
            var contact = contactService.FindContact(keyword);
            if (contact == null) return "Contact not found";
            var sb = new StringBuilder();
            sb.AppendLine($"[{contact.Id}] {contact.Name}");
            if (!string.IsNullOrEmpty(contact.Email)) sb.AppendLine($"Email: {contact.Email}");
            if (!string.IsNullOrEmpty(contact.Phone)) sb.AppendLine($"Phone: {contact.Phone}");
            if (!string.IsNullOrEmpty(contact.Notes)) sb.AppendLine($"Notes: {contact.Notes}");
            return sb.ToString();
        });

        yield return new FunctionDefinition("update_contact", "Update contact info", new
        {
            type = "object",
            properties = new
            {
                id = new { type = "number", description = "Contact ID" },
                name = new { type = "string", description = "New name (optional)" },
                email = new { type = "string", description = "New email (optional)" },
                phone = new { type = "string", description = "New phone (optional)" },
                notes = new { type = "string", description = "New notes (optional)" }
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
            return "Contact updated successfully";
        });

        yield return new FunctionDefinition("delete_contact", "Delete a contact", new
        {
            type = "object",
            properties = new
            {
                id = new { type = "number", description = "Contact ID" }
            },
            required = new[] { "id" }
        }, async input =>
        {
            var id = input.GetProperty("id").GetInt32();
            contactService.DeleteContact(id);
            return "Contact deleted successfully";
        });
    }
}
