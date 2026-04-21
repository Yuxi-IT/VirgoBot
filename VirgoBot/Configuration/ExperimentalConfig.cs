namespace VirgoBot.Configuration;

public class ExperimentalConfig
{
    public VoiceConfig? Voice { get; set; }
}

public class VoiceConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string AsrResourceId { get; set; } = "volc.bigasr.auc_turbo";
    public string TtsResourceId { get; set; } = "seed-tts-2.0";
    public string VoiceType { get; set; } = "zh_female_vv_uranus_bigtts";
}
