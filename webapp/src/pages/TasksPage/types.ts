export interface ScheduledTask {
  id: string;
  name: string;
  description: string;
  enabled: boolean;
  taskType: 'http' | 'shell';
  scheduleType: 'interval' | 'daily' | 'cron';
  intervalMinutes: number;
  dailyTime: string;
  cronExpression: string;
  taskRequirement: string;
  httpMethod: string;
  httpUrl: string;
  httpHeaders: Record<string, string>;
  httpBody: string;
  shellCommand: string;
  lastRunTime?: string;
  nextRunTime?: string;
  createdAt: string;
}

export interface TasksResponse {
  success: boolean;
  data: ScheduledTask[];
}

export interface TaskResponse {
  success: boolean;
  data: ScheduledTask;
}
