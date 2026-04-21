namespace VirgoBot.Configuration;

/// <summary>
/// 实验性功能配置
/// </summary>
public class ExperimentalConfig
{
    /// <summary>
    /// 语音功能配置
    /// </summary>
    public VoiceConfig? Voice { get; set; }
}

/// <summary>
/// 语音功能配置
/// </summary>
public class VoiceConfig
{
    /// <summary>
    /// API密钥（ASR和TTS共用）
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// ASR资源ID
    /// </summary>
    public string AsrResourceId { get; set; } = "volc.bigasr.auc_turbo";

    /// <summary>
    /// TTS资源ID
    /// </summary>
    public string TtsResourceId { get; set; } = "seed-tts-2.0";

    /// <summary>
    /// 语音类型
    /// </summary>
    public string VoiceType { get; set; } = "zh_female_vv_uranus_bigtts";
}
