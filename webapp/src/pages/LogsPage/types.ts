export interface LogEntry {
  id: number;
  level: string;
  component: string;
  message: string;
  timestamp: string;
}

export interface LogsResponse {
  success: boolean;
  data: {
    logs: LogEntry[];
    total: number;
  };
}
