import { Button, ListBox, Label, Description } from '@heroui/react';
import { Plus } from '@gravity-ui/icons';
import type { SessionInfo } from './types';
import type { Selection } from '@heroui/react';

interface Props {
  sessions: SessionInfo[];
  currentSession: string;
  onSwitch: (fileName: string) => void;
  onCreate: () => void;
  onReload: () => void;
}

export default function SessionList({ sessions, currentSession, onSwitch, onCreate }: Props) {
  const handleSelectionChange = (keys: Selection) => {
    if (keys === 'all') return;
    const selected = [...keys][0];
    if (selected && String(selected) !== currentSession) {
      onSwitch(String(selected));
    }
  };

  return (
    <div className="flex flex-col h-full">
      <div className="p-3 border-b flex items-center justify-between">
        <span className="font-semibold text-sm">会话</span>
        <Button size="sm" isIconOnly variant="ghost" onPress={onCreate}>
          <Plus />
        </Button>
      </div>
      <div className="flex-1 overflow-y-auto">
        {sessions.length === 0 ? (
          <div className="text-center text-default-400 text-sm py-8">暂无会话</div>
        ) : (
          <ListBox
            aria-label="会话列表"
            selectionMode="single"
            selectedKeys={new Set([currentSession])}
            onSelectionChange={handleSelectionChange}
          >
            {sessions.map(s => (
              <ListBox.Item key={s.fileName} id={s.fileName} textValue={s.sessionName || '新会话'}>
                <Label>{s.sessionName || '新会话'}</Label>
                <Description>{s.messageCount} 条消息</Description>
              </ListBox.Item>
            ))}
          </ListBox>
        )}
      </div>
    </div>
  );
}
