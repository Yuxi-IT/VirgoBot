import { Button } from '@heroui/react';
import { Plus } from '@gravity-ui/icons';
import type { SessionInfo } from './types';

interface Props {
  sessions: SessionInfo[];
  currentSession: string;
  onSwitch: (fileName: string) => void;
  onCreate: () => void;
  onReload: () => void;
}

export default function SessionList({ sessions, currentSession, onSwitch, onCreate }: Props) {
  return (
    <div className="flex flex-col h-full">
      <div className="p-3 border-b flex items-center justify-between">
        <span className="font-semibold text-sm">会话</span>
        <Button size="sm" isIconOnly variant="ghost" onPress={onCreate}>
          <Plus />
        </Button>
      </div>
      <div className="flex-1 overflow-y-auto">
        {sessions.map(s => (
          <div
            key={s.fileName}
            className={`px-3 py-2 cursor-pointer text-sm border-b hover:bg-default-100 transition-colors ${
              s.fileName === currentSession ? 'bg-primary/10 border-l-2 border-l-primary' : ''
            }`}
            onClick={() => onSwitch(s.fileName)}
          >
            <div className="font-medium truncate">
              {s.sessionName || '新会话'}
            </div>
            <div className="text-xs text-default-400 mt-0.5">
              {s.messageCount} 条消息
            </div>
          </div>
        ))}
        {sessions.length === 0 && (
          <div className="text-center text-default-400 text-sm py-8">暂无会话</div>
        )}
      </div>
    </div>
  );
}
