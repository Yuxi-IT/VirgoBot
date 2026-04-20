using System.Net;
using System.Text.Json;
using System.IO.Compression;
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

                // 多子模块 Skill
                if (root.TryGetProperty("subSkills", out var subSkillsEl) && subSkillsEl.ValueKind == JsonValueKind.Array)
                {
                    skills.Add(new
                    {
                        fileName,
                        name = root.GetProperty("name").GetString() ?? "",
                        description = root.GetProperty("description").GetString() ?? "",
                        command = "",
                        mode = "multi",
                        parameterCount = 0,
                        skillType = "json",
                        subSkillCount = subSkillsEl.GetArrayLength()
                    });
                    continue;
                }

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
                    parameterCount = root.TryGetProperty("parameters", out var p) ? p.GetArrayLength() : 0,
                    skillType = "json",
                    subSkillCount = 0
                });
            }
            catch
            {
                skills.Add(new { fileName, name = fileName, description = "解析失败", command = "", mode = "command", parameterCount = 0, skillType = "json" });
            }
        }

        foreach (var subDir in Directory.GetDirectories(dir))
        {
            var skillMdPath = Path.Combine(subDir, "SKILL.md");
            if (!File.Exists(skillMdPath)) continue;

            try
            {
                var mdContent = await File.ReadAllTextAsync(skillMdPath);
                var parsed = VirgoBot.Functions.SkillMdParser.Parse(mdContent);
                if (parsed == null) continue;

                var dirName = Path.GetFileName(subDir);
                skills.Add(new
                {
                    fileName = dirName + "/SKILL.md",
                    name = parsed.Name,
                    description = parsed.Description,
                    command = "",
                    mode = "skill.md",
                    parameterCount = 0,
                    skillType = "skill.md",
                    subSkillCount = 0
                });
            }
            catch
            {
            }
        }

        await SendJsonResponse(ctx, new { success = true, data = skills });
    }

    public async Task HandleGetSkillRequest(HttpListenerContext ctx)
    {
        var name = ctx.Request.Url!.AbsolutePath.Replace("/api/skills/", "");

        // SKILL.md 目录型：name 格式为 "dirName"，对应 skills/dirName/SKILL.md
        var skillMdPath = Path.Combine(AppConstants.SkillsDirectory, name, "SKILL.md");
        if (File.Exists(skillMdPath))
        {
            var mdContent = await File.ReadAllTextAsync(skillMdPath);
            await SendJsonResponse(ctx, new { success = true, data = new { fileName = $"{name}/SKILL.md", content = mdContent, skillType = "skill.md" } });
            return;
        }

        var filePath = Path.Combine(AppConstants.SkillsDirectory, $"{name}.json");
        if (!File.Exists(filePath))
        {
            await SendErrorResponse(ctx, 404, "Skill not found");
            return;
        }

        var content = await File.ReadAllTextAsync(filePath);
        await SendJsonResponse(ctx, new { success = true, data = new { fileName = $"{name}.json", content, skillType = "json" } });
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

        var body = await ReadRequestBody<SkillRequest>(ctx);
        if (body == null || string.IsNullOrWhiteSpace(body.Content))
        {
            await SendErrorResponse(ctx, 400, "Content is required");
            return;
        }

        // SKILL.md 目录型
        var skillMdPath = Path.Combine(AppConstants.SkillsDirectory, name, "SKILL.md");
        if (File.Exists(skillMdPath))
        {
            await File.WriteAllTextAsync(skillMdPath, body.Content);
            ColorLog.Success("SKILL", $"SKILL.md 已更新: {name}");
            await SendJsonResponse(ctx, new { success = true, message = "Skill updated" });
            return;
        }

        var filePath = Path.Combine(AppConstants.SkillsDirectory, $"{name}.json");
        if (!File.Exists(filePath))
        {
            await SendErrorResponse(ctx, 404, "Skill not found");
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
        var dir = AppConstants.SkillsDirectory;

        var filePath = Path.Combine(dir, $"{name}.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            ColorLog.Success("SKILL", $"Skill 已删除: {name}.json");
            await SendJsonResponse(ctx, new { success = true, message = "Skill deleted" });
            return;
        }

        var skillDir = Path.Combine(dir, name);
        if (Directory.Exists(skillDir) && File.Exists(Path.Combine(skillDir, "SKILL.md")))
        {
            Directory.Delete(skillDir, recursive: true);
            ColorLog.Success("SKILL", $"Skill 目录已删除: {name}");
            await SendJsonResponse(ctx, new { success = true, message = "Skill deleted" });
            return;
        }

        await SendErrorResponse(ctx, 404, "Skill not found");
    }

    /// <summary>
    /// 处理 Skill 压缩包导入（multipart/form-data 上传 .zip 文件）
    /// </summary>
    public async Task HandleImportSkillZipRequest(HttpListenerContext ctx)
    {
        try
        {
            var contentType = ctx.Request.ContentType ?? "";

            byte[] zipBytes;

            if (contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                zipBytes = await ExtractZipFromMultipart(ctx);
            }
            else
            {
                using var ms = new MemoryStream();
                await ctx.Request.InputStream.CopyToAsync(ms);
                zipBytes = ms.ToArray();
            }

            if (zipBytes.Length == 0)
            {
                await SendErrorResponse(ctx, 400, "Empty file");
                return;
            }

            var result = await ImportSkillZip(zipBytes);
            if (result.Success)
            {
                ColorLog.Success("SKILL", $"Skill 压缩包导入成功: {result.SkillName}");
                await SendJsonResponse(ctx, new { success = true, message = "Skill imported", skillName = result.SkillName });
            }
            else
            {
                await SendErrorResponse(ctx, 400, result.Error ?? "Import failed");
            }
        }
        catch (Exception ex)
        {
            await SendErrorResponse(ctx, 500, ex.Message);
        }
    }

    /// <summary>
    /// 处理从 URL 下载 Skill 压缩包
    /// </summary>
    public async Task HandleImportSkillZipFromUrlRequest(HttpListenerContext ctx)
    {
        try
        {
            var body = await ReadRequestBody<ImportUrlRequest>(ctx);
            if (body == null || string.IsNullOrWhiteSpace(body.Url))
            {
                await SendErrorResponse(ctx, 400, "URL is required");
                return;
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            byte[] zipBytes;
            try
            {
                zipBytes = await http.GetByteArrayAsync(body.Url);
            }
            catch (Exception ex)
            {
                await SendErrorResponse(ctx, 400, $"Failed to download: {ex.Message}");
                return;
            }

            var result = await ImportSkillZip(zipBytes);
            if (result.Success)
            {
                ColorLog.Success("SKILL", $"Skill 压缩包从 URL 导入成功: {result.SkillName}");
                await SendJsonResponse(ctx, new { success = true, message = "Skill imported", skillName = result.SkillName });
            }
            else
            {
                await SendErrorResponse(ctx, 400, result.Error ?? "Import failed");
            }
        }
        catch (Exception ex)
        {
            await SendErrorResponse(ctx, 500, ex.Message);
        }
    }

    private static async Task<byte[]> ExtractZipFromMultipart(HttpListenerContext ctx)
    {
        using var ms = new MemoryStream();
        await ctx.Request.InputStream.CopyToAsync(ms);
        var rawBytes = ms.ToArray();

        var contentType = ctx.Request.ContentType ?? "";
        var boundaryIndex = contentType.IndexOf("boundary=", StringComparison.OrdinalIgnoreCase);
        if (boundaryIndex < 0) return rawBytes; // fallback

        var boundary = "--" + contentType[(boundaryIndex + 9)..].Trim();
        var boundaryBytes = System.Text.Encoding.ASCII.GetBytes(boundary);

        var headerEnd = FindSequence(rawBytes, System.Text.Encoding.ASCII.GetBytes("\r\n\r\n"), 0);
        if (headerEnd < 0) return rawBytes;

        var dataStart = headerEnd + 4;

        var endBoundaryBytes = System.Text.Encoding.ASCII.GetBytes("\r\n" + boundary);
        var dataEnd = FindSequence(rawBytes, endBoundaryBytes, dataStart);
        if (dataEnd < 0) dataEnd = rawBytes.Length;

        var result = new byte[dataEnd - dataStart];
        Array.Copy(rawBytes, dataStart, result, 0, result.Length);
        return result;
    }

    private static int FindSequence(byte[] source, byte[] pattern, int startIndex)
    {
        for (var i = startIndex; i <= source.Length - pattern.Length; i++)
        {
            var found = true;
            for (var j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] != pattern[j]) { found = false; break; }
            }
            if (found) return i;
        }
        return -1;
    }

    private static async Task<(bool Success, string? SkillName, string? Error)> ImportSkillZip(byte[] zipBytes)
    {
        try
        {
            using var zipStream = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            ZipArchiveEntry? skillMdEntry = null;
            string? skillDirPrefix = null;

            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (name.EndsWith("SKILL.md", StringComparison.OrdinalIgnoreCase))
                {
                    skillMdEntry = entry;
                    var slashIdx = name.LastIndexOf('/');
                    skillDirPrefix = slashIdx >= 0 ? name[..(slashIdx + 1)] : "";
                    break;
                }
            }

            if (skillMdEntry == null)
            {
                var jsonEntry = archive.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                    !e.FullName.StartsWith("_"));

                if (jsonEntry == null)
                    return (false, null, "ZIP 中未找到 SKILL.md 或 .json 文件");

                using var jsonStream = jsonEntry.Open();
                using var reader = new StreamReader(jsonStream);
                var jsonContent = await reader.ReadToEndAsync();

                using var doc = JsonDocument.Parse(jsonContent);
                var skillName = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(skillName))
                    return (false, null, "JSON Skill 缺少 name 字段");

                var jsonPath = Path.Combine(AppConstants.SkillsDirectory, $"{skillName}.json");
                await File.WriteAllTextAsync(jsonPath, jsonContent);
                return (true, skillName, null);
            }

            using var mdStream = skillMdEntry.Open();
            using var mdReader = new StreamReader(mdStream);
            var mdContent = await mdReader.ReadToEndAsync();

            var parsed = VirgoBot.Functions.SkillMdParser.Parse(mdContent);
            if (parsed == null)
                return (false, null, "SKILL.md 缺少有效的 frontmatter (name/description)");

            var targetDir = Path.Combine(AppConstants.SkillsDirectory, parsed.Name);
            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, recursive: true);
            Directory.CreateDirectory(targetDir);

            foreach (var entry in archive.Entries)
            {
                if (entry.Length == 0 && entry.FullName.EndsWith("/")) continue;

                var entryPath = entry.FullName.Replace('\\', '/');

                var relativePath = skillDirPrefix != null && entryPath.StartsWith(skillDirPrefix)
                    ? entryPath[skillDirPrefix.Length..]
                    : entryPath;

                if (string.IsNullOrEmpty(relativePath)) continue;

                var destPath = Path.Combine(targetDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
                var destDirPath = Path.GetDirectoryName(destPath)!;
                Directory.CreateDirectory(destDirPath);

                using var entryStream = entry.Open();
                using var destStream = File.Create(destPath);
                await entryStream.CopyToAsync(destStream);
            }

            return (true, parsed.Name, null);
        }
        catch (InvalidDataException)
        {
            return (false, null, "无效的 ZIP 文件");
        }
    }

    public record SkillRequest
    {
        public string? Name { get; init; }
        public string? Content { get; init; }
    }

    public record ImportUrlRequest
    {
        public string? Url { get; init; }
    }
}