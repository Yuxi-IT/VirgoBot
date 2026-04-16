using System.Net;
using VirgoBot.Services;
using static VirgoBot.Channels.Handlers.HttpResponseHelper;

namespace VirgoBot.Channels.Handlers;

public class ContactApiHandler
{
    private readonly Gateway _gateway;

    public ContactApiHandler(Gateway gateway)
    {
        _gateway = gateway;
    }

    public async Task HandleGetContactsRequest(HttpListenerContext ctx)
    {
        var contacts = _gateway.ContactService.GetAllContacts();
        await SendJsonResponse(ctx, new { success = true, data = contacts });
    }

    public async Task HandleAddContactRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<ContactRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Name))
        {
            await SendErrorResponse(ctx, 400, "Name is required");
            return;
        }

        _gateway.ContactService.AddContact(body.Name, body.Email, body.Phone, body.Notes);
        await SendJsonResponse(ctx, new { success = true, message = "Contact added" });
    }

    public async Task HandleUpdateContactRequest(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath;
        var idStr = path.Replace("/api/contacts/", "");

        if (!int.TryParse(idStr, out var id))
        {
            await SendErrorResponse(ctx, 400, "Invalid contact ID");
            return;
        }

        var body = await ReadRequestBody<ContactRequest>(ctx);
        if (body == null)
        {
            await SendErrorResponse(ctx, 400, "Invalid request body");
            return;
        }

        _gateway.ContactService.UpdateContact(id, body.Name, body.Email, body.Phone, body.Notes);
        await SendJsonResponse(ctx, new { success = true, message = "Contact updated" });
    }

    public async Task HandleDeleteContactRequest(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath;
        var idStr = path.Replace("/api/contacts/", "");

        if (!int.TryParse(idStr, out var id))
        {
            await SendErrorResponse(ctx, 400, "Invalid contact ID");
            return;
        }

        _gateway.ContactService.DeleteContact(id);
        await SendJsonResponse(ctx, new { success = true, message = "Contact deleted" });
    }
}

public record ContactRequest
{
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Notes { get; init; }
}
