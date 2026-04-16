using System.Net;
using System.Text.Json;
using VirgoBot.Configuration;
using VirgoBot.Services;
using VirgoBot.Utilities;
using static VirgoBot.Channels.Handlers.HttpResponseHelper;

namespace VirgoBot.Channels.Handlers;

public class SkillApiHandler
{
    private readonly Gateway _gateway;

    public SkillApiHandler(Gateway gateway)
    {
        _gateway = gateway;
    }

    public async Task HandleGetSkillsRequest(HttpListenerContext ctx)
    {
        var dir = AppConstants.SkillsDirectory;
        Directory.CreateDirectory(dir);

        var skills = new List<object>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.StartsWith("_")) continue;

            try
            {
                var json = await File.ReadAllTextAsync(file);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var mode = root.TryGetProperty("mode", out var modeEl) ? modeEl.GetString() ?? "command" : "command";

                string command;
                if (mode == "http" && root.TryGetProperty("http", out var httpEl))
                {
                    var method = httpEl.TryGetProperty("method", out var m) ? m.GetString() ?? "GET" : "GET";
                    var url = httpEl.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                    command = $"{method} {url}";
                }
                else
                {
                    command = root.TryGetProperty("command", out var cmdEl) ? cmdEl.GetString() ?? "" : "";
                }

                skills.Add(new
                {
                    fileName,
                    name = root.GetProperty("name").GetString() ?? "",
                    description = root.GetProperty("description").GetString() ?? "",
                    command,
                    mode,
                    parameterCount = root.TryGetProperty("parameters", out var p) ? p.GetArrayLength() : 0
                });
            }
            catch
            {
                skills.Add(new { fileName, name = fileName, description = "解析失败", command = "", mode = "command", parameterCount = 0 });
            }
        }

        await SendJsonResponse(ctx, new { success = true, data = skills });
    }

    public async Task HandleGetSkillRequest(HttpListenerContext ctx)
    {
        var name = ctx.Request.Url!.AbsolutePath.Replace("/api/skills/", "");
        var filePath = Path.Combine(AppConstants.SkillsDirectory, $"{name}.json");

        if (!File.Exists(filePath))
        {
            await SendErrorResponse(ctx, 404, "Skill not found");
            return;
        }

        var content = await File.ReadAllTextAsync(filePath);
        await SendJsonResponse(ctx, new { success = true, data = new { fileName = $"{name}.json", content } });
    }

    public async Task HandleCreateSkillRequest(HttpListenerContext ctx)
    {
        var body = await ReadRequestBody<SkillRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Name))
        {
            await SendErrorResponse(ctx, 400, "Name is required");
            return;
        }

        var dir = AppConstants.SkillsDirectory;
        Directory.CreateDirectory(dir);

        var fileName = $"{body.Name}.json";
        var filePath = Path.Combine(dir, fileName);

        if (File.Exists(filePath))
        {
            await SendErrorResponse(ctx, 409, "Skill already exists");
            return;
        }

        await File.WriteAllTextAsync(filePath, body.Content ?? "{}");
        ColorLog.Success("SKILL", $"Skill 已创建: {fileName}");
        await SendJsonResponse(ctx, new { success = true, message = "Skill created" });
    }

    public async Task HandleUpdateSkillRequest(HttpListenerContext ctx)
    {
        var name = ctx.Request.Url!.AbsolutePath.Replace("/api/skills/", "");
        var filePath = Path.Combine(AppConstants.SkillsDirectory, $"{name}.json");

        if (!File.Exists(filePath))
        {
            await SendErrorResponse(ctx, 404, "Skill not found");
            return;
        }

        var body = await ReadRequestBody<SkillRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Content))
        {
            await SendErrorResponse(ctx, 400, "Content is required");
            return;
        }

        if (!string.IsNullOrWhiteSpace(body.Name) && body.Name != name)
        {
            var newFilePath = Path.Combine(AppConstants.SkillsDirectory, $"{body.Name}.json");
            if (File.Exists(newFilePath))
            {
                await SendErrorResponse(ctx, 409, "Target skill name already exists");
                return;
            }
            File.Delete(filePath);
            filePath = newFilePath;
        }

        await File.WriteAllTextAsync(filePath, body.Content);
        ColorLog.Success("SKILL", $"Skill 已更新: {Path.GetFileName(filePath)}");
        await SendJsonResponse(ctx, new { success = true, message = "Skill updated" });
    }

    public async Task HandleDeleteSkillRequest(HttpListenerContext ctx)
    {
        var name = ctx.Request.Url!.AbsolutePath.Replace("/api/skills/", "");
        var filePath = Path.Combine(AppConstants.SkillsDirectory, $"{name}.json");

        if (!File.Exists(filePath))
        {
            await SendErrorResponse(ctx, 404, "Skill not found");
            return;
        }

        File.Delete(filePath);
        ColorLog.Success("SKILL", $"Skill 已删除: {name}.json");
        await SendJsonResponse(ctx, new { success = true, message = "Skill deleted" });
    }
}

public record SkillRequest
{
    public string? Name { get; init; }
    public string? Content { get; init; }
}
