export interface ScheduledTask {
  id: string;
  name: string;
  description: string;
  enabled: boolean;
  taskType: 'http' | 'shell' | 'text';
  scheduleType: 'interval' | 'daily' | 'once';
  intervalMinutes: number;
  dailyTime: string;
  cronExpression: string;
  onceDelayMinutes?: number;
  onceAt?: string;
  taskRequirement: string;
  httpMethod: string;
  httpUrl: string;
  httpHeaders: Record<string, string>;
  httpBody: string;
  shellCommand: string;
  textInstruction: string;
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
