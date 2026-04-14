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

export interface AgentDetailResponse {
  success: boolean;
  data: {
    name: string;
    content: string;
  };
}
