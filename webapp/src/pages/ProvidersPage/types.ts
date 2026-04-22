export interface ProviderInfo {
  name: string;
  apiKey: string;
  baseUrl: string;
  currentModel: string;
  models: string[];
  protocol: string;
}

export interface ProvidersResponse {
  success: boolean;
  data: {
    providers: ProviderInfo[];
    currentProvider: string;
  };
}

export interface ModelsResponse {
  success: boolean;
  data: string[];
}

export const PRESET_PROVIDERS: { label: string; baseUrl: string; protocol: string }[] = [
  { label: 'Qwen 官方', baseUrl: 'https://dashscope.aliyuncs.com/compatible-mode', protocol: 'openai' },
  { label: 'DeepSeek 官方', baseUrl: 'https://api.deepseek.com', protocol: 'openai' },
  { label: 'OpenAI 官方', baseUrl: 'https://api.openai.com', protocol: 'openai' },
  { label: 'Claude 官方', baseUrl: 'https://api.anthropic.com', protocol: 'anthropic' },
  { label: '智谱清言', baseUrl: 'https://open.bigmodel.cn/api/paas', protocol: 'openai' },
  { label: 'Gemini 官方', baseUrl: 'https://generativelanguage.googleapis.com', protocol: 'gemini' },
];
