import { useState } from 'react';
import { Button, Dropdown, Label, Modal, Input, toast, Card } from '@heroui/react';
import { Plus } from '@gravity-ui/icons';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import type { SessionInfo, Message, MessagesResponse } from './types';

interface Props {
  sessions: SessionInfo[];
  currentSession: string;
  onSwitch: (fileName: string) => void;
  onCreate: () => void;
  onReload: () => void;
}

export default function SessionList({ sessions, currentSession, onSwitch, onCreate, onReload }: Props) {
  const { t } = useI18n();
  const [renameTarget, setRenameTarget] = useState<SessionInfo | null>(null);
  const [renameValue, setRenameValue] = useState('');

  const handleDelete = async (session: SessionInfo) => {
    if (session.isCurrent) {
      toast.danger(t('chatPage.cannotDeleteCurrent'));
      return;
    }
    try {
      await api.del(`/api/sessions/${encodeURIComponent(session.fileName)}`);
      toast.success(t('chatPage.sessionDeleted'));
      onReload();
    } catch {
      toast.danger(t('chatPage.deleteFailed'));
    }
  };

  const handleRenameOpen = (session: SessionInfo) => {
    setRenameTarget(session);
    setRenameValue(session.sessionName || '');
  };

  const handleRenameConfirm = async () => {
    if (!renameTarget || !renameValue.trim()) return;
    try {
      const wasCurrent = renameTarget.isCurrent;
      if (!wasCurrent) {
        await api.put('/api/sessions/switch', { session: renameTarget.fileName });
      }
      await api.put('/api/sessions/rename', { name: renameValue.trim() });
      if (!wasCurrent) {
        await api.put('/api/sessions/switch', { session: currentSession });
      }
      toast.success(t('chatPage.sessionRenamed'));
      onReload();
    } catch {
      toast.danger(t('chatPage.renameFailed'));
    }
    setRenameTarget(null);
  };

  const handleExport = async (session: SessionInfo) => {
    try {
      const wasCurrent = session.isCurrent;
      if (!wasCurrent) {
        await api.put('/api/sessions/switch', { session: session.fileName });
      }
      const res = await api.get<MessagesResponse>(`/api/messages?limit=9999&offset=0`);
      if (!wasCurrent) {
        await api.put('/api/sessions/switch', { session: currentSession });
      }

      if (!res.success) {
        toast.danger(t('chatPage.exportFailed'));
        return;
      }

      const messages: Message[] = res.data.messages;
      const name = session.sessionName || session.fileName;
      const lines = [`# ${name}\n`];
      for (const msg of messages) {
        const time = new Date(msg.createdAt).toLocaleString();
        const role = msg.role === 'user' ? t('chatPage.roleUser') : msg.role === 'assistant' ? t('chatPage.roleAI') : t('chatPage.roleTool');
        lines.push(`### ${role}  \n*${time}*\n`);
        lines.push(msg.content + '\n');
      }

      const blob = new Blob([lines.join('\n')], { type: 'text/markdown;charset=utf-8' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${name}.md`;
      a.click();
      URL.revokeObjectURL(url);
      toast.success(t('chatPage.exportSuccess'));
    } catch {
      toast.danger(t('chatPage.exportFailed'));
    }
  };

  return (
    <div className="flex flex-col h-full">
      <div className="p-3 border-b flex items-center justify-between">
        <span className="font-semibold text-sm">{t('chatPage.sessions')}</span>
        <Button size="sm" isIconOnly variant="ghost" onPress={onCreate}>
          <Plus />
        </Button>
      </div>
      <div className="flex-1 overflow-y-auto p-2 flex flex-col gap-1">
        {sessions.length === 0 ? (
          <div className="text-center text-default-400 text-sm py-8">{t('chatPage.noSessions')}</div>
        ) : (
          sessions.map(s => {
            const isSelected = s.fileName === currentSession;
            return (
              <Dropdown key={s.fileName} trigger="longPress">
                <Dropdown.Trigger>
                  <Card
                    className={`px-3 py-2 cursor-pointer text-sm text-left ${
                      isSelected
                        ? 'bg-default-400 shadow-sm border-1 border-primary-500'
                        : 'hover:bg-default-100'
                    }`}
                    onClick={() => { if (!isSelected) onSwitch(s.fileName); }}
                    onContextMenu={(e) => e.preventDefault()}
                  >
                    <div className={`font-medium truncate ${isSelected ? 'text-primary' : ''}`}>
                      {s.sessionName || t('chatPage.newSession')}
                      <span className='text-xs'>{t('chatPage.messageCount').replace('{n}', String(s.messageCount))}</span>
                    </div>
                  </Card>
                </Dropdown.Trigger>
                <Dropdown.Popover>
                  <Dropdown.Menu onAction={(key) => {
                    if (key === 'rename') handleRenameOpen(s);
                    if (key === 'export') handleExport(s);
                    if (key === 'delete') handleDelete(s);
                  }}>
                    <Dropdown.Item id="rename" textValue={t('chatPage.rename')}>
                      <Label>{t('chatPage.rename')}</Label>
                    </Dropdown.Item>
                    <Dropdown.Item id="export" textValue={t('chatPage.exportMarkdown')}>
                      <Label>{t('chatPage.exportMarkdown')}</Label>
                    </Dropdown.Item>
                    <Dropdown.Item id="delete" textValue={t('common.delete')}>
                      <Label>{t('common.delete')}</Label>
                    </Dropdown.Item>
                  </Dropdown.Menu>
                </Dropdown.Popover>
              </Dropdown>
            );
          })
        )}
      </div>

      {/* Rename modal */}
      <Modal>
        <Modal.Backdrop isOpen={!!renameTarget} onOpenChange={(open) => { if (!open) setRenameTarget(null); }}>
          <Modal.Container size="sm">
            <Modal.Dialog>
              <Modal.Header>
                <Modal.Heading>{t('chatPage.renameSession')}</Modal.Heading>
              </Modal.Header>
              <Modal.Body>
                <Input
                  placeholder={t('chatPage.sessionName')}
                  value={renameValue}
                  onChange={(e) => setRenameValue(e.target.value)}
                  onKeyDown={(e) => { if (e.key === 'Enter') handleRenameConfirm(); }}
                  autoFocus
                />
              </Modal.Body>
              <Modal.Footer>
                <Button variant="ghost" size="sm" onPress={() => setRenameTarget(null)}>{t('common.cancel')}</Button>
                <Button variant="primary" size="sm" onPress={handleRenameConfirm} isDisabled={!renameValue.trim()}>{t('common.confirm')}</Button>
              </Modal.Footer>
            </Modal.Dialog>
          </Modal.Container>
        </Modal.Backdrop>
      </Modal>
    </div>
  );
}
