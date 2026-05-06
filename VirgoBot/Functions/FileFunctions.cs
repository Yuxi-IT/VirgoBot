using System.Text.Json;

namespace VirgoBot.Functions;

public static class FileFunctions
{
    public static IEnumerable<FunctionDefinition> Register()
    {
        yield return new FunctionDefinition("read_file", "Read file contents", new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "File path" }
            },
            required = new[] { "path" }
        }, input =>
        {
            var path = input.GetProperty("path").GetString() ?? "";
            return Task.FromResult(File.Exists(path) ? File.ReadAllText(path) : "File not found");
        });

        yield return new FunctionDefinition("write_file", "Write content to file", new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "File path" },
                content = new { type = "string", description = "File content" }
            },
            required = new[] { "path", "content" }
        }, input =>
        {
            var path = input.GetProperty("path").GetString() ?? "";
            var content = input.GetProperty("content").GetString() ?? "";
            File.WriteAllText(path, content);
            return Task.FromResult("Write succeeded");
        });

        yield return new FunctionDefinition("download_file", "Download file from URL to specified location", new
        {
            type = "object",
            properties = new
            {
                url = new { type = "string", description = "File URL" },
                save_path = new { type = "string", description = "Save path" }
            },
            required = new[] { "url", "save_path" }
        }, async input =>
        {
            var url = input.GetProperty("url").GetString() ?? "";
            var savePath = input.GetProperty("save_path").GetString() ?? "";

            using var client = new HttpClient();
            var data = await client.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(savePath, data);
            return $"File downloaded to: {savePath} ({data.Length} bytes)";
        });
    }
}
