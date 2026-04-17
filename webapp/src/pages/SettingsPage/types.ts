export interface ConfigData {
  model: string;
  baseUrl: string;
  memoryFile: string;
  server: {
    listenUrl: string;
    maxTokens: number;
    messageLimit: number;
    messageSplitDelimiters: string;
    autoResponse: {
      enabled: boolean;
      minIdleMinutes: number;
      maxIdleMinutes: number;
    };
  };
  channel: {
    telegram: {
      enabled: boolean;
      botToken: string;
      allowedUsers: number[];
    };
    email: {
      enabled: boolean;
      imapHost: string;
      imapPort: number;
      smtpHost: string;
      smtpPort: number;
      address: string;
      password: string;
      notification: {
        notifyToTelegram: boolean;
        notifyToILink: boolean;
        notifyToWebSocket: boolean;
      };
    };
    iLink: {
      enabled: boolean;
      token: string;
      webSocketUrl: string;
      sendUrl: string;
      webhookPath: string;
      defaultUserId: string;
    };
  };
}

export interface ConfigResponse {
  success: boolean;
  data: ConfigData;
}

export interface ContentResponse {
  success: boolean;
  data: { content: string };
}
