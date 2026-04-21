using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VirgoBot.Configuration;

namespace VirgoBot.Features.Voice;

/// <summary>
/// 语音服务（ASR和TTS）
/// </summary>
public class VoiceService
{
    private readonly HttpClient _httpClient;
    private readonly ExperimentalConfig _config;

    private const string AsrUrl = "https://openspeech.bytedance.com/api/v3/auc/bigmodel/recognize/flash";
    private const string TtsUrl = "https://openspeech.bytedance.com/api/v3/tts/unidirectional/sse";

    public VoiceService(ExperimentalConfig config)
    {
        _config = config;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// 语音识别（ASR）- 将base64音频转为文本
    /// </summary>
    public async Task<AsrResult> RecognizeAsync(string audioBase64, CancellationToken cancellationToken = default)
    {
        if (_config.Voice == null || string.IsNullOrEmpty(_config.Voice.ApiKey))
        {
            throw new InvalidOperationException("语音配置未设置");
        }

        var taskId = Guid.NewGuid().ToString();
        var request = new
        {
            user = new { uid = _config.Voice.ApiKey },
            audio = new { data = audioBase64 },
            request = new { model_name = "bigmodel" }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, AsrUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json")
        };

        httpRequest.Headers.Add("X-Api-Key", _config.Voice.ApiKey);
        httpRequest.Headers.Add("X-Api-Resource-Id", _config.Voice.AsrResourceId);
        httpRequest.Headers.Add("X-Api-Request-Id", taskId);
        httpRequest.Headers.Add("X-Api-Sequence", "-1");

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        
        if (!response.Headers.TryGetValues("X-Api-Status-Code", out var statusCodes))
        {
            throw new Exception("ASR请求失败：未返回状态码");
        }

        var statusCode = statusCodes.First();
        var logId = response.Headers.TryGetValues("X-Tt-Logid", out var logIds) 
            ? logIds.First() : "unknown";

        if (statusCode != "20000000")
        {
            var errorMsg = response.Headers.TryGetValues("X-Api-Message", out var messages)
                ? messages.First() : "未知错误";
            throw new Exception($"ASR识别失败 [code={statusCode}, logid={logId}]: {errorMsg}");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<AsrResponse>(responseBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (result?.Result?.Text == null)
        {
            throw new Exception("ASR返回结果为空");
        }

        return new AsrResult
        {
            Text = result.Result.Text,
            Duration = result.AudioInfo?.Duration ?? 0,
            LogId = logId
        };
    }

    /// <summary>
    /// 语音合成（TTS）- 将文本转为base64音频
    /// </summary>
    public async Task<TtsResult> SynthesizeAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_config.Voice == null || string.IsNullOrEmpty(_config.Voice.ApiKey))
        {
            throw new InvalidOperationException("语音配置未设置");
        }

        var request = new
        {
            user = new { uid = "virgobot_user" },
            req_params = new
            {
                text,
                speaker = _config.Voice.VoiceType,
                audio_params = new
                {
                    format = "mp3",
                    sample_rate = 24000,
                    enable_timestamp = true
                },
                additions = "{\"explicit_language\":\"zh\",\"disable_markdown_filter\":true,\"enable_timestamp\":true}"
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, TtsUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json")
        };

        httpRequest.Headers.Add("X-Api-Key", _config.Voice.ApiKey);
        httpRequest.Headers.Add("X-Api-Resource-Id", _config.Voice.TtsResourceId);
        httpRequest.Headers.Add("Connection", "keep-alive");

        var response = await _httpClient.SendAsync(
            httpRequest, 
            HttpCompletionOption.ResponseHeadersRead, 
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var logId = response.Headers.TryGetValues("X-Tt-Logid", out var logIds)
            ? logIds.First() : "unknown";

        var audioData = new List<byte>();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? line;
        var eventData = new StringBuilder();
        
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                // 空行表示一个事件结束
                if (eventData.Length > 0)
                {
                    ProcessSseEvent(eventData.ToString(), audioData);
                    eventData.Clear();
                }
                continue;
            }

            if (line.StartsWith(":"))
            {
                // 注释行，跳过
                continue;
            }

            eventData.AppendLine(line);
        }

        // 处理最后一个事件
        if (eventData.Length > 0)
        {
            ProcessSseEvent(eventData.ToString(), audioData);
        }

        if (audioData.Count == 0)
        {
            throw new Exception("TTS未返回音频数据");
        }

        var audioBase64 = Convert.ToBase64String(audioData.ToArray());
        
        return new TtsResult
        {
            AudioBase64 = audioBase64,
            AudioSize = audioData.Count,
            LogId = logId
        };
    }

    private void ProcessSseEvent(string eventText, List<byte> audioData)
    {
        var lines = eventText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string? dataContent = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                dataContent = line.Substring(5).Trim();
                break;
            }
        }

        if (string.IsNullOrEmpty(dataContent))
        {
            return;
        }

        try
        {
            var jsonData = JsonSerializer.Deserialize<JsonElement>(dataContent);
            
            var code = jsonData.TryGetProperty("code", out var codeEl) 
                ? codeEl.GetInt32() : 0;

            if (code == 0 && jsonData.TryGetProperty("data", out var dataEl))
            {
                var base64Audio = dataEl.GetString();
                if (!string.IsNullOrEmpty(base64Audio))
                {
                    var chunk = Convert.FromBase64String(base64Audio);
                    audioData.AddRange(chunk);
                }
            }
            else if (code > 0 && code != 20000000)
            {
                throw new Exception($"TTS合成错误: code={code}");
            }
        }
        catch (JsonException)
        {
            // 忽略无法解析的数据
        }
    }
}

// ASR响应模型
public class AsrResponse
{
    public AudioInfo? AudioInfo { get; set; }
    public AsrResultData? Result { get; set; }
}

public class AudioInfo
{
    public int Duration { get; set; }
}

public class AsrResultData
{
    public string? Text { get; set; }
}

// 返回结果模型
public class AsrResult
{
    public string Text { get; set; } = string.Empty;
    public int Duration { get; set; }
    public string LogId { get; set; } = string.Empty;
}

public class TtsResult
{
    public string AudioBase64 { get; set; } = string.Empty;
    public int AudioSize { get; set; }
    public string LogId { get; set; } = string.Empty;
}
