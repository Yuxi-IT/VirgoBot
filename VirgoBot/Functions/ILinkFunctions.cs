using System.Text.Json;
using VirgoBot.Integrations.ILink;

namespace VirgoBot.Functions;

public static class ILinkFunctions
{
    public static IEnumerable<FunctionDefinition> Register(ILinkBridgeService iLinkBridge)
    {
        yield return new FunctionDefinition(
            "ilink_send_image",
            "通过 iLink 向指定用户发送图片。支持本地文件路径或 HTTP/HTTPS URL。",
            new
            {
                type = "object",
                properties = new
                {
                    user_id = new { type = "string", description = "iLink 用户 ID（即消息来源的 UserId）" },
                    source = new { type = "string", description = "图片来源：本地文件路径（如 C:/img.jpg）或 HTTP/HTTPS URL" }
                },
                required = new[] { "user_id", "source" }
            },
            async input =>
            {
                var userId = input.GetProperty("user_id").GetString() ?? "";
                var source = input.GetProperty("source").GetString() ?? "";
                try
                {
                    var bytes = await ReadBytesAsync(source);
                    await iLinkBridge.SendImageAsync(userId, bytes);
                    return "图片已发送";
                }
                catch (Exception ex)
                {
                    return $"发送图片失败: {ex.Message}";
                }
            });

        yield return new FunctionDefinition(
            "ilink_send_voice",
            "通过 iLink 向指定用户发送语音消息。支持本地文件路径或 HTTP/HTTPS URL，格式建议 AMR 或 MP3。",
            new
            {
                type = "object",
                properties = new
                {
                    user_id = new { type = "string", description = "iLink 用户 ID" },
                    source = new { type = "string", description = "语音文件来源：本地路径或 URL" }
                },
                required = new[] { "user_id", "source" }
            },
            async input =>
            {
                var userId = input.GetProperty("user_id").GetString() ?? "";
                var source = input.GetProperty("source").GetString() ?? "";
                try
                {
                    var bytes = await ReadBytesAsync(source);
                    await iLinkBridge.SendVoiceAsync(userId, bytes);
                    return "语音已发送";
                }
                catch (Exception ex)
                {
                    return $"发送语音失败: {ex.Message}";
                }
            });

        yield return new FunctionDefinition(
            "ilink_send_video",
            "通过 iLink 向指定用户发送视频。支持本地文件路径或 HTTP/HTTPS URL，格式建议 MP4。",
            new
            {
                type = "object",
                properties = new
                {
                    user_id = new { type = "string", description = "iLink 用户 ID" },
                    source = new { type = "string", description = "视频文件来源：本地路径或 URL" }
                },
                required = new[] { "user_id", "source" }
            },
            async input =>
            {
                var userId = input.GetProperty("user_id").GetString() ?? "";
                var source = input.GetProperty("source").GetString() ?? "";
                try
                {
                    var bytes = await ReadBytesAsync(source);
                    await iLinkBridge.SendVideoAsync(userId, bytes);
                    return "视频已发送";
                }
                catch (Exception ex)
                {
                    return $"发送视频失败: {ex.Message}";
                }
            });

        yield return new FunctionDefinition(
            "ilink_send_file",
            "通过 iLink 向指定用户发送文件。支持本地文件路径或 HTTP/HTTPS URL。",
            new
            {
                type = "object",
                properties = new
                {
                    user_id = new { type = "string", description = "iLink 用户 ID" },
                    source = new { type = "string", description = "文件来源：本地路径或 URL" },
                    file_name = new { type = "string", description = "发送时显示的文件名（含扩展名），不填则从路径/URL 自动推断" }
                },
                required = new[] { "user_id", "source" }
            },
            async input =>
            {
                var userId = input.GetProperty("user_id").GetString() ?? "";
                var source = input.GetProperty("source").GetString() ?? "";
                var fileName = input.TryGetProperty("file_name", out var fn) ? fn.GetString() : null;
                fileName = string.IsNullOrWhiteSpace(fileName) ? InferFileName(source) : fileName;
                try
                {
                    var bytes = await ReadBytesAsync(source);
                    await iLinkBridge.SendFileAsync(userId, bytes, fileName!);
                    return $"文件 {fileName} 已发送";
                }
                catch (Exception ex)
                {
                    return $"发送文件失败: {ex.Message}";
                }
            });
    }

    private static async Task<byte[]> ReadBytesAsync(string source)
    {
        if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(60);
            return await http.GetByteArrayAsync(source);
        }

        return await File.ReadAllBytesAsync(source);
    }

    private static string InferFileName(string source)
    {
        try
        {
            var uri = new Uri(source);
            var name = Path.GetFileName(uri.LocalPath);
            return string.IsNullOrWhiteSpace(name) ? "file" : name;
        }
        catch
        {
            return Path.GetFileName(source) is { Length: > 0 } n ? n : "file";
        }
    }
}
