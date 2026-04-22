export interface SessionInfo {
  fileName: string;
  sessionName: string | null;
  messageCount: number;
  soulCount: number;
  lastModified: string;
  size: number;
  isCurrent: boolean;
}

export interface SessionsResponse {
  success: boolean;
  data: SessionInfo[];
}

export interface Message {
  id: number;
  role: string;
  content: string;
  createdAt: string;
}

export interface MessagesResponse {
  success: boolean;
  data: {
    messages: Message[];
    total: number;
    userId: string;
  };
}

export interface AgentInfo {
  name: string;
  fileName: string;
  memoryPath: string;
  preview: string;
  size: number;
}

export interface AgentsResponse {
  success: boolean;
  data: {
    agents: AgentInfo[];
    currentAgent: string;
  };
}

export interface UserInfo {
  userId: string;
}
