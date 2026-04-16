using System.Net;
using System.Text;
using System.Text.Json;

namespace VirgoBot.Channels.Handlers;

public static class HttpResponseHelper
{
    public static async Task SendJsonResponse(HttpListenerContext ctx, object data, int statusCode = 200)
    {
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
    }

    public static async Task SendErrorResponse(HttpListenerContext ctx, int statusCode, string message)
    {
        await SendJsonResponse(ctx, new { success = false, error = message }, statusCode);
    }

    public static async Task<T?> ReadRequestBody<T>(HttpListenerContext ctx)
    {
        using var reader = new StreamReader(ctx.Request.InputStream);
        var body = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public static string? GetQueryParam(HttpListenerContext ctx, string paramName)
    {
        return ctx.Request.QueryString[paramName];
    }
}
