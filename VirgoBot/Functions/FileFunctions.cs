using System.Text.Json;

namespace VirgoBot.Functions;

public static class FileFunctions
{
    public static IEnumerable<FunctionDefinition> Register()
    {
        yield return new FunctionDefinition("read_file", "读取文件内容", new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "文件路径" }
            },
            required = new[] { "path" }
        }, input =>
        {
            var path = input.GetProperty("path").GetString() ?? "";
            return Task.FromResult(File.Exists(path) ? File.ReadAllText(path) : "文件不存在");
        });

        yield return new FunctionDefinition("write_file", "写入文件内容", new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "文件路径" },
                content = new { type = "string", description = "文件内容" }
            },
            required = new[] { "path", "content" }
        }, input =>
        {
            var path = input.GetProperty("path").GetString() ?? "";
            var content = input.GetProperty("content").GetString() ?? "";
            File.WriteAllText(path, content);
            return Task.FromResult("写入成功");
        });

        yield return new FunctionDefinition("download_file", "从URL下载文件到指定位置", new
        {
            type = "object",
            properties = new
            {
                url = new { type = "string", description = "文件URL" },
                save_path = new { type = "string", description = "保存路径" }
            },
            required = new[] { "url", "save_path" }
        }, async input =>
        {
            var url = input.GetProperty("url").GetString() ?? "";
            var savePath = input.GetProperty("save_path").GetString() ?? "";

            using var client = new HttpClient();
            var data = await client.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(savePath, data);
            return $"文件已下载到: {savePath} ({data.Length} 字节)";
        });
    }
}
