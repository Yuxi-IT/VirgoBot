using System.Net;
using System.Text.Json;
using VirgoBot.Configuration;
using VirgoBot.Features.Voice;
using static VirgoBot.Channels.Handlers.HttpResponseHelper;

namespace VirgoBot.Channels.Handlers;

/// <summary>
/// 语音API处理器
/// </summary>
public class VoiceApiHandler
{
    private VoiceService? _voiceService;
    private ExperimentalConfig _config;

    public VoiceApiHandler()
    {
        _config = ExperimentalConfigLoader.Load();
        InitializeVoiceService();
    }

    private void InitializeVoiceService()
    {
        if (_config.Voice != null && !string.IsNullOrEmpty(_config.Voice.ApiKey))
        {
            _voiceService = new VoiceService(_config);
        }
    }

    /// <summary>
    /// 获取语音配置
    /// </summary>
    public async Task HandleGetConfigRequest(HttpListenerContext ctx)
    {
        var maskedConfig = new
        {
            voice = _config.Voice == null ? null : new
            {
                apiKey = MaskSecret(_config.Voice.ApiKey),
                asrResourceId = _config.Voice.AsrResourceId,
                ttsResourceId = _config.Voice.TtsResourceId,
                voiceType = _config.Voice.VoiceType
            }
        };

        await SendJsonResponse(ctx, new { success = true, data = maskedConfig });
    }

    /// <summary>
    /// 更新语音配置
    /// </summary>
    public async Task HandleUpdateConfigRequest(HttpListenerContext ctx)
    {
        var request = await ReadRequestBody<UpdateVoiceConfigRequest>(ctx);
        if (request?.Voice == null)
        {
            await SendErrorResponse(ctx, 400, "无效的请求数据");
            return;
        }

        // 如果apiKey是掩码，保留原值
        if (request.Voice.ApiKey?.StartsWith("***") == true && _config.Voice != null)
        {
            request.Voice.ApiKey = _config.Voice.ApiKey;
        }

        _config.Voice = new VoiceConfig
        {
            ApiKey = request.Voice.ApiKey ?? string.Empty,
            AsrResourceId = request.Voice.AsrResourceId ?? "volc.bigasr.auc_turbo",
            TtsResourceId = request.Voice.TtsResourceId ?? "seed-tts-2.0",
            VoiceType = request.Voice.VoiceType ?? "zh_female_vv_uranus_bigtts"
        };

        ExperimentalConfigLoader.Save(_config);
        InitializeVoiceService();

        await SendJsonResponse(ctx, new { success = true, message = "配置已更新" });
    }

    /// <summary>
    /// 语音识别（ASR）
    /// </summary>
    public async Task HandleAsrRequest(HttpListenerContext ctx)
    {
        if (_voiceService == null)
        {
            await SendErrorResponse(ctx, 400, "语音服务未配置");
            return;
        }

        var request = await ReadRequestBody<AsrRequest>(ctx);
        if (request == null || string.IsNullOrEmpty(request.AudioBase64))
        {
            await SendErrorResponse(ctx, 400, "缺少音频数据");
            return;
        }

        try
        {
            var result = await _voiceService.RecognizeAsync(request.AudioBase64);
            await SendJsonResponse(ctx, new
            {
                success = true,
                data = new
                {
                    text = result.Text,
                    duration = result.Duration,
                    logId = result.LogId
                }
            });
        }
        catch (Exception ex)
        {
            await SendErrorResponse(ctx, 500, $"语音识别失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 语音合成（TTS）
    /// </summary>
    public async Task HandleTtsRequest(HttpListenerContext ctx)
    {
        if (_voiceService == null)
        {
            await SendErrorResponse(ctx, 400, "语音服务未配置");
            return;
        }

        var request = await ReadRequestBody<TtsRequest>(ctx);
        if (request == null || string.IsNullOrEmpty(request.Text))
        {
            await SendErrorResponse(ctx, 400, "缺少文本数据");
            return;
        }

        try
        {
            var result = await _voiceService.SynthesizeAsync(request.Text);
            await SendJsonResponse(ctx, new
            {
                success = true,
                data = new
                {
                    audioBase64 = result.AudioBase64,
                    audioSize = result.AudioSize,
                    logId = result.LogId
                }
            });
        }
        catch (Exception ex)
        {
            await SendErrorResponse(ctx, 500, $"语音合成失败: {ex.Message}");
        }
    }

    private static string MaskSecret(string? secret)
    {
        if (string.IsNullOrEmpty(secret) || secret.Length <= 8)
        {
            return "***";
        }
        return secret.Substring(0, 4) + "***" + secret.Substring(secret.Length - 4);
    }
}

// 请求模型
public class UpdateVoiceConfigRequest
{
    public VoiceConfigRequest? Voice { get; set; }
}

public class VoiceConfigRequest
{
    public string? ApiKey { get; set; }
    public string? AsrResourceId { get; set; }
    public string? TtsResourceId { get; set; }
    public string? VoiceType { get; set; }
}

public class AsrRequest
{
    public string AudioBase64 { get; set; } = string.Empty;
}

public class TtsRequest
{
    public string Text { get; set; } = string.Empty;
}