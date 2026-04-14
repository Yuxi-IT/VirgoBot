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
