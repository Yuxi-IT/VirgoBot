export interface ConfigData {
  model: string;
  baseUrl: string;
  memoryFile: string;
  server: {
    listenUrl: string;
    maxTokens: number;
    messageLimit: number;
  };
  email: {
    imapHost: string;
    address: string;
    password: string;
    enabled: boolean;
  };
  iLink: {
    enabled: boolean;
  };
  allowedUsers: number[];
}

export interface ConfigResponse {
  success: boolean;
  data: ConfigData;
}

export interface ContentResponse {
  success: boolean;
  data: { content: string };
}
