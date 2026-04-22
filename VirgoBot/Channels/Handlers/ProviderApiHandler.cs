using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VirgoBot.Configuration;
using VirgoBot.Services;
using VirgoBot.Utilities;
using static VirgoBot.Channels.Handlers.HttpResponseHelper;

namespace VirgoBot.Channels.Handlers;

public class ProviderApiHandler
{
    private readonly Gateway _gateway;

    public ProviderApiHandler(Gateway gateway)
    {
        _gateway = gateway;
    }

    public async Task HandleGetProvidersRequest(HttpListenerContext ctx)
    {
        var providers = _gateway.Config.Providers.Select(p => new
        {
            name = p.Name,
            apiKey = MaskSecret(p.ApiKey),
            baseUrl = p.BaseUrl,
            currentModel = p.CurrentModel,
            models = p.Models,
            protocol = p.Protocol
        });

        await SendJsonResponse(ctx, new
        {
            success = true,
            data = new
            {
                providers,
                currentProvider = _gateway.Config.CurrentProvider
            }
        });
    }

    public async Task HandleCreateProviderRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<ProviderCreateUpdateRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Name))
        {
            await SendErrorResponse(ctx, 400, "Name is required");
            return;
        }

        var config = _gateway.Config;
        if (config.Providers.Any(p => p.Name.Equals(body.Name, StringComparison.OrdinalIgnoreCase)))
        {
            await SendErrorResponse(ctx, 409, "Provider already exists");
            return;
        }

        var provider = new ProviderConfig
        {
            Name = body.Name.Trim(),
            ApiKey = body.ApiKey ?? "",
            BaseUrl = body.BaseUrl ?? "",
            CurrentModel = body.CurrentModel ?? "",
            Models = body.Models ?? new List<string>(),
            Protocol = body.Protocol ?? "openai"
        };

        config.Providers.Add(provider);
        if (config.Providers.Count == 1)
            config.CurrentProvider = provider.Name;

        ConfigLoader.Save(config);
        ColorLog.Success("PROVIDER", $"供应商已创建: {provider.Name}");
        await SendJsonResponse(ctx, new { success = true, message = "Provider created" });
    }

    public async Task HandleUpdateProviderRequest(HttpListenerContext ctx)
    {
        var name = Uri.UnescapeDataString(
            ctx.Request.Url!.AbsolutePath.Replace("/api/providers/", ""));

        var config = _gateway.Config;
        var provider = config.Providers.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (provider == null)
        {
            await SendErrorResponse(ctx, 404, "Provider not found");
            return;
        }

        var body = await ReadRequestBody<ProviderCreateUpdateRequest>(ctx);
        if (body == null)
        {
            await SendErrorResponse(ctx, 400, "Invalid request body");
            return;
        }

        if (!string.IsNullOrWhiteSpace(body.ApiKey)) provider.ApiKey = body.ApiKey;
        if (!string.IsNullOrWhiteSpace(body.BaseUrl)) provider.BaseUrl = body.BaseUrl;
        if (!string.IsNullOrWhiteSpace(body.CurrentModel)) provider.CurrentModel = body.CurrentModel;
        if (body.Models != null) provider.Models = body.Models;
        if (!string.IsNullOrWhiteSpace(body.Protocol)) provider.Protocol = body.Protocol;

        if (!string.IsNullOrWhiteSpace(body.Name) &&
            !body.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            if (config.CurrentProvider.Equals(name, StringComparison.OrdinalIgnoreCase))
                config.CurrentProvider = body.Name;
            provider.Name = body.Name.Trim();
        }

        ConfigLoader.Save(config);
        ColorLog.Success("PROVIDER", $"供应商已更新: {provider.Name}");
        await SendJsonResponse(ctx, new { success = true, message = "Provider updated" });
    }

    public async Task HandleDeleteProviderRequest(HttpListenerContext ctx)
    {
        var name = Uri.UnescapeDataString(
            ctx.Request.Url!.AbsolutePath.Replace("/api/providers/", ""));

        var config = _gateway.Config;
        var provider = config.Providers.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (provider == null)
        {
            await SendErrorResponse(ctx, 404, "Provider not found");
            return;
        }

        if (config.CurrentProvider.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            await SendErrorResponse(ctx, 400, "Cannot delete the current provider");
            return;
        }

        config.Providers.Remove(provider);
        ConfigLoader.Save(config);
        ColorLog.Success("PROVIDER", $"供应商已删除: {name}");
        await SendJsonResponse(ctx, new { success = true, message = "Provider deleted" });
    }

    public async Task HandleSwitchCurrentProviderRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<ProviderSwitchRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Name))
        {
            await SendErrorResponse(ctx, 400, "Provider name is required");
            return;
        }

        var config = _gateway.Config;
        if (!config.Providers.Any(p => p.Name.Equals(body.Name, StringComparison.OrdinalIgnoreCase)))
        {
            await SendErrorResponse(ctx, 404, "Provider not found");
            return;
        }

        config.CurrentProvider = body.Name;
        ConfigLoader.Save(config);
        ColorLog.Success("PROVIDER", $"已切换到供应商: {body.Name}");
        await SendJsonResponse(ctx, new { success = true, message = "Current provider switched" });
    }

    public async Task HandleGetModelsRequest(HttpListenerContext ctx)
    {
        var name = ctx.Request.Url!.AbsolutePath
            .Replace("/api/providers/", "")
            .Replace("/models", "");
        name = Uri.UnescapeDataString(name);

        var config = _gateway.Config;
        var provider = config.Providers.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (provider == null)
        {
            await SendErrorResponse(ctx, 404, "Provider not found");
            return;
        }

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", provider.ApiKey);

            var baseUrl = provider.BaseUrl.TrimEnd('/');
            var modelsUrl = baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                ? $"{baseUrl}/models"
                : $"{baseUrl}/v1/models";

            var response = await http.GetAsync(modelsUrl);
            var result = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                await SendErrorResponse(ctx, (int)response.StatusCode, $"Failed to fetch models: {result}");
                return;
            }

            using var doc = JsonDocument.Parse(result);
            var models = new List<string>();

            if (doc.RootElement.TryGetProperty("data", out var dataEl) &&
                dataEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dataEl.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var idEl))
                        models.Add(idEl.GetString() ?? "");
                }
            }

            models.Sort();
            await SendJsonResponse(ctx, new { success = true, data = models });
        }
        catch (Exception ex)
        {
            await SendErrorResponse(ctx, 500, $"Failed to fetch models: {ex.Message}");
        }
    }

    private static string MaskSecret(string secret)
    {
        if (string.IsNullOrEmpty(secret) || secret.Length <= 8)
            return "****";
        return secret[..4] + "****" + secret[^4..];
    }
}

public record ProviderCreateUpdateRequest
{
    public string? Name { get; init; }
    public string? ApiKey { get; init; }
    public string? BaseUrl { get; init; }
    public string? CurrentModel { get; init; }
    public List<string>? Models { get; init; }
    public string? Protocol { get; init; }
}

public record ProviderSwitchRequest
{
    public string? Name { get; init; }
}
