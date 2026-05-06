using System.Text.Json;
using VirgoBot.Integrations.ILink;

namespace VirgoBot.Functions;

public static class ILinkFunctions
{
    public static IEnumerable<FunctionDefinition> Register(ILinkBridgeService iLinkBridge)
    {
        yield return new FunctionDefinition(
            "ilink_send_image",
            "Send an image to a specified user via iLink. Supports local file paths or HTTP/HTTPS URLs.",
            new
            {
                type = "object",
                properties = new
                {
                    user_id = new { type = "string", description = "iLink user ID (the UserId from the incoming message)" },
                    source = new { type = "string", description = "Image source: local file path (e.g. C:/img.jpg) or HTTP/HTTPS URL" }
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
                    return "Image sent";
                }
                catch (Exception ex)
                {
                    return $"Failed to send image: {ex.Message}";
                }
            });

        yield return new FunctionDefinition(
            "ilink_send_voice",
            "Send a voice message to a specified user via iLink. Supports local file paths or HTTP/HTTPS URLs. Recommended format: AMR or MP3.",
            new
            {
                type = "object",
                properties = new
                {
                    user_id = new { type = "string", description = "iLink user ID" },
                    source = new { type = "string", description = "Voice file source: local path or URL" }
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
                    return "Voice sent";
                }
                catch (Exception ex)
                {
                    return $"Failed to send voice: {ex.Message}";
                }
            });

        yield return new FunctionDefinition(
            "ilink_send_video",
            "Send a video to a specified user via iLink. Supports local file paths or HTTP/HTTPS URLs. Recommended format: MP4.",
            new
            {
                type = "object",
                properties = new
                {
                    user_id = new { type = "string", description = "iLink user ID" },
                    source = new { type = "string", description = "Video file source: local path or URL" }
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
                    return "Video sent";
                }
                catch (Exception ex)
                {
                    return $"Failed to send video: {ex.Message}";
                }
            });

        yield return new FunctionDefinition(
            "ilink_send_file",
            "Send a file to a specified user via iLink. Supports local file paths or HTTP/HTTPS URLs.",
            new
            {
                type = "object",
                properties = new
                {
                    user_id = new { type = "string", description = "iLink user ID" },
                    source = new { type = "string", description = "File source: local path or URL" },
                    file_name = new { type = "string", description = "Display filename (with extension) when sending. Auto-inferred from path/URL if left empty." }
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
                    return $"File {fileName} sent";
                }
                catch (Exception ex)
                {
                    return $"Failed to send file: {ex.Message}";
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
