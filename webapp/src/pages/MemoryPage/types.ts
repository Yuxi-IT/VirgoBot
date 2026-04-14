export interface SessionInfo {
  fileName: string;
  messageCount: number;
  soulCount: number;
  lastModified: string;
  size: number;
  isCurrent: boolean;
}

export interface UserInfo {
  userId: string;
  messageCount: number;
  lastActive: string;
}

export interface Message {
  id: number;
  role: string;
  content: string;
  createdAt: string;
}

export interface SoulEntry {
  id: number;
  content: string;
  createdAt: string;
}

export interface SessionsResponse {
  success: boolean;
  data: SessionInfo[];
}

export interface UsersResponse {
  success: boolean;
  data: UserInfo[];
}

export interface MessagesResponse {
  success: boolean;
  data: {
    messages: Message[];
    total: number;
    userId: string;
  };
}

export interface SoulResponse {
  success: boolean;
  data: SoulEntry[];
}
