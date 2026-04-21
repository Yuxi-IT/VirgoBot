namespace VirgoBot.Configuration;

public enum ApiStandard
{
    /// <summary>OpenAI Chat Completions 标准（默认，兼容绝大多数厂商）</summary>
    OpenAI = 0,

    /// <summary>Anthropic Claude 原生 API 标准</summary>
    Anthropic = 1,

    /// <summary>Google Gemini 原生 API 标准</summary>
    Gemini = 2,
}
