export interface McpServer {
  name: string;
  transport: string;
  command: string;
  args: string[];
  env: Record<string, string>;
  url: string;
  enabled: boolean;
  status: string;
  toolCount: number;
  error?: string;
}

export interface McpTool {
  name: string;
  description?: string;
  inputSchema?: Record<string, unknown>;
}

export interface McpServersResponse {
  success: boolean;
  data: McpServer[];
}

export interface McpToolsResponse {
  success: boolean;
  data: McpTool[];
}
