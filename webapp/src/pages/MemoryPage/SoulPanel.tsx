import { useEffect, useState } from 'react';
import { Card, Button, Spinner, TextArea, toast } from '@heroui/react';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import type { SoulEntry, SoulResponse } from './types';

interface SoulPanelProps {
  active: boolean;
}

function SoulPanel({ active }: SoulPanelProps) {
  const { t } = useI18n();
  const [entries, setEntries] = useState<SoulEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [newContent, setNewContent] = useState('');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editContent, setEditContent] = useState('');
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    if (active && !loaded) {
      loadSoul();
      setLoaded(true);
    }
  }, [active]);

  const loadSoul = async () => {
    try {
      setLoading(true);
      const res = await api.get<SoulResponse>('/api/soul');
      if (res.success) {
        setEntries(res.data);
      }
    } catch {
      // silently fail
    } finally {
      setLoading(false);
    }
  };

  const addEntry = async () => {
    if (!newContent.trim()) return;
    try {
      await api.post('/api/soul', { content: newContent });
      toast.success(t('memory.addSuccess'));
      setNewContent('');
      loadSoul();
    } catch {
      toast.danger(t('settings.saveFailed'));
    }
  };

  const updateEntry = async (id: number) => {
    if (!editContent.trim()) return;
    try {
      await api.put(`/api/soul/${id}`, { content: editContent });
      toast.success(t('memory.updateSuccess'));
      setEditingId(null);
      setEditContent('');
      loadSoul();
    } catch {
      toast.danger(t('settings.saveFailed'));
    }
  };

  const deleteEntry = async (id: number) => {
    if (!confirm(t('memory.deleteConfirm'))) return;
    try {
      await api.del(`/api/soul/${id}`);
      toast.success(t('memory.deleteSuccess'));
      loadSoul();
    } catch {
      toast.danger(t('settings.saveFailed'));
    }
  };

  return (
    <Card className="mt-4">
      <Card.Header>
        <Card.Title>{t('memory.soul')}</Card.Title>
      </Card.Header>
      <Card.Content>
        {loading ? (
          <div className="flex justify-center py-8">
            <Spinner size="lg" />
          </div>
        ) : (
          <div className="space-y-4">
            {/* Add new entry */}
            <div className="p-4 border border-dashed border-gray-300 dark:border-gray-600 rounded-lg">
              <TextArea
                value={newContent}
                onChange={(e) => setNewContent(e.target.value)}
                rows={3}
                placeholder={t('memory.content')}
                className="w-full mb-3"
              />
              <Button size="sm" onPress={addEntry} isDisabled={!newContent.trim()}>
                {t('memory.addSoul')}
              </Button>
            </div>

            {/* Entries list */}
            {entries.length === 0 ? (
              <p className="text-gray-500 text-center py-4">{t('common.noData')}</p>
            ) : (
              <div className="space-y-3">
                {entries.map((entry) => (
                  <div key={entry.id} className="p-4 rounded-lg bg-gray-50 dark:bg-gray-800">
                    {editingId === entry.id ? (
                      <div className="space-y-2">
                        <TextArea
                          value={editContent}
                          onChange={(e) => setEditContent(e.target.value)}
                          rows={3}
                          className="w-full"
                        />
                        <div className="flex gap-2">
                          <Button size="sm" onPress={() => updateEntry(entry.id)}>
                            {t('common.save')}
                          </Button>
                          <Button size="sm" variant="secondary" onPress={() => setEditingId(null)}>
                            {t('common.cancel')}
                          </Button>
                        </div>
                      </div>
                    ) : (
                      <>
                        <p className="text-sm whitespace-pre-wrap mb-2">{entry.content}</p>
                        <div className="flex items-center justify-between">
                          <span className="text-xs text-gray-400">
                            {new Date(entry.createdAt).toLocaleString()}
                          </span>
                          <div className="flex gap-2">
                            <Button
                              size="sm"
                              variant="secondary"
                              onPress={() => {
                                setEditingId(entry.id);
                                setEditContent(entry.content);
                              }}
                            >
                              {t('common.edit')}
                            </Button>
                            <Button
                              size="sm"
                              variant="danger"
                              onPress={() => deleteEntry(entry.id)}
                            >
                              {t('common.delete')}
                            </Button>
                          </div>
                        </div>
                      </>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </Card.Content>
    </Card>
  );
}

export default SoulPanel;
