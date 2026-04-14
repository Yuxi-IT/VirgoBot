import { useState } from 'react';
import { Card, Button, Spinner, Chip, TextArea, toast } from '@heroui/react';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import type { AgentInfo, AgentDetailResponse } from './types';

interface AgentCardProps {
  agent: AgentInfo;
  isCurrent: boolean;
  switching: boolean;
  onSwitch: (memoryPath: string) => void;
  onDeleted: () => void;
}

function AgentCard({ agent, isCurrent, switching, onSwitch, onDeleted }: AgentCardProps) {
  const { t } = useI18n();
  const [expanded, setExpanded] = useState(false);
  const [content, setContent] = useState('');
  const [contentLoading, setContentLoading] = useState(false);
  const [editing, setEditing] = useState(false);
  const [editContent, setEditContent] = useState('');
  const [savingEdit, setSavingEdit] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const toggleExpand = async () => {
    if (expanded && !editing) {
      setExpanded(false);
      setEditing(false);
      return;
    }
    try {
      setContentLoading(true);
      setExpanded(true);
      setEditing(false);
      const res = await api.get<AgentDetailResponse>(`/api/agents/${encodeURIComponent(agent.name)}`);
      if (res.success) {
        setContent(res.data.content);
      }
    } catch (e) {
      console.error('Failed to load agent content:', e);
    } finally {
      setContentLoading(false);
    }
  };

  const startEditing = () => {
    setEditing(true);
    setEditContent(content);
  };

  const cancelEditing = () => {
    setEditing(false);
    setEditContent('');
  };

  const saveEdit = async () => {
    if (!editContent.trim()) return;
    try {
      setSavingEdit(true);
      await api.put(`/api/agents/${encodeURIComponent(agent.name)}`, { content: editContent });
      toast.success(t('agent.updateSuccess'));
      setEditing(false);
      setContent(editContent);
    } catch {
      toast.danger(t('settings.saveFailed'));
    } finally {
      setSavingEdit(false);
    }
  };

  const handleDelete = async () => {
    if (isCurrent) {
      toast.danger(t('agent.cannotDeleteCurrent'));
      return;
    }
    if (!confirm(t('agent.deleteConfirm'))) return;
    try {
      setDeleting(true);
      await api.del(`/api/agents/${encodeURIComponent(agent.name)}`);
      toast.success(t('agent.deleteSuccess'));
      onDeleted();
    } catch {
      toast.danger(t('settings.saveFailed'));
    } finally {
      setDeleting(false);
    }
  };

  return (
    <Card className={`hover:scale-101 transition-all cursor-pointer ${isCurrent ? 'ring-2 ring-blue-500 bg-blue-500/10' : ''}`}>
      <Card.Header>
        <div className="flex items-center justify-between w-full">
          <div className="flex items-center gap-3">
            <Card.Title className="text-lg">{agent.name}</Card.Title>
            {isCurrent && (
              <Chip size="sm" color="accent" variant="soft">
                {t('agent.current')}
              </Chip>
            )}
            <span className="text-xs text-gray-400">{agent.size} chars</span>
          </div>
          <div className="flex gap-2">
            <Button size="sm" variant="secondary" onPress={toggleExpand}>
              {expanded ? t('common.cancel') : t('agent.preview')}
            </Button>
            {!isCurrent && (
              <>
                <Button
                  size="sm"
                  onPress={() => onSwitch(agent.memoryPath)}
                  isDisabled={switching}
                >
                  {switching ? <Spinner size="sm" className="mr-1" /> : null}
                  {t('agent.switchAgent')}
                </Button>
                <Button
                  size="sm"
                  variant="danger"
                  onPress={handleDelete}
                  isDisabled={deleting}
                >
                  {deleting ? <Spinner size="sm" className="mr-1" /> : null}
                  {t('common.delete')}
                </Button>
              </>
            )}
          </div>
        </div>
      </Card.Header>

      {!expanded && (
        <Card.Content>
          <p className="text-sm text-gray-500 line-clamp-3">{agent.preview}</p>
        </Card.Content>
      )}

      {expanded && (
        <Card.Content>
          {contentLoading ? (
            <div className="flex justify-center py-4">
              <Spinner size="sm" />
            </div>
          ) : editing ? (
            <div className="space-y-3">
              <TextArea
                value={editContent}
                onChange={(e) => setEditContent(e.target.value)}
                rows={18}
                className="font-mono w-full"
              />
              <div className="flex gap-2">
                <Button
                  size="sm"
                  onPress={saveEdit}
                  isDisabled={savingEdit || !editContent.trim()}
                >
                  {savingEdit ? <Spinner size="sm" className="mr-1" /> : null}
                  {t('common.save')}
                </Button>
                <Button size="sm" variant="secondary" onPress={cancelEditing}>
                  {t('common.cancel')}
                </Button>
              </div>
            </div>
          ) : (
            <div className="space-y-3">
              <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded-lg max-h-96 overflow-y-auto">
                <pre className="whitespace-pre-wrap text-sm font-mono">{content}</pre>
              </div>
              <Button size="sm" variant="secondary" onPress={startEditing}>
                {t('agent.editAgent')}
              </Button>
            </div>
          )}
        </Card.Content>
      )}
    </Card>
  );
}

export default AgentCard;
