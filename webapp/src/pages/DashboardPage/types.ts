export interface ChannelInfo {
  enabled: boolean;
  status: string;
  clients?: number;
}

export interface StatusData {
  botName: string;
  model: string;
  uptime: string;
  startTime: string;
  connectedClients: number;
  channels: Record<string, ChannelInfo>;
  server: {
    listenUrl: string;
    maxTokens: number;
    messageLimit: number;
  };
}

export interface ApiResponse {
  success: boolean;
  data: StatusData;
}
