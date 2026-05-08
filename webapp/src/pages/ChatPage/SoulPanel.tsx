import { useEffect, useState } from 'react';
import { Button, Spinner, TextArea, Modal, toast, Chip } from '@heroui/react';
import { TrashBin, Pencil } from '@gravity-ui/icons';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';

interface SoulEntry {
  id: number;
  content: string;
  createdAt: string;
  tags: string;
  weight: number;
  accessCount: number;
  lastAccessed?: string;
  source: string;
  forgotten: boolean;
}

export default function SoulPanel() {
  const { t } = useI18n();
  const [entries, setEntries] = useState<SoulEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [newContent, setNewContent] = useState('');
  const [newTags, setNewTags] = useState('');
  const [newWeight, setNewWeight] = useState(1.0);
  const [adding, setAdding] = useState(false);
  const [editTarget, setEditTarget] = useState<SoulEntry | null>(null);
  const [editContent, setEditContent] = useState('');
  const [editTags, setEditTags] = useState('');
  const [editWeight, setEditWeight] = useState(1.0);
  const [editSaving, setEditSaving] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<SoulEntry | null>(null);
  const [searchQuery, setSearchQuery] = useState('');

  const load = async () => {
    try {
      setLoading(true);
      const params = new URLSearchParams();
      if (searchQuery.trim()) params.set('search', searchQuery.trim());
      const qs = params.toString();
      const res = await api.get<{ success: boolean; data: SoulEntry[] }>(`/api/soul${qs ? '?' + qs : ''}`);
      if (res.success) setEntries(res.data);
    } catch { /* silent */ } finally { setLoading(false); }
  };

  useEffect(() => { load(); }, [searchQuery]);

  const getWeightColor = (w: number) => {
    if (w >= 0.7) return 'success';
    if (w >= 0.3) return 'warning';
    return 'danger';
  };

  const parseTags = (tagsStr: string) =>
    tagsStr ? tagsStr.split(',').map(t => t.trim()).filter(Boolean) : [];

  const handleAdd = async () => {
    if (!newContent.trim()) return;
    setAdding(true);
    try {
      await api.post('/api/soul', {
        content: newContent.trim(),
        tags: newTags.trim() || undefined,
        weight: newWeight,
        source: 'user',
      });
      toast.success(t('memory.addSuccess'));
      setNewContent('');
      setNewTags('');
      setNewWeight(1.0);
      load();
    } catch {
      toast.danger(t('common.error'));
    } finally { setAdding(false); }
  };

  const handleEdit = async () => {
    if (!editTarget || !editContent.trim()) return;
    setEditSaving(true);
    try {
      await api.put(`/api/soul/${editTarget.id}`, {
        content: editContent.trim(),
        tags: editTags.trim() || undefined,
        weight: editWeight,
      });
      toast.success(t('memory.updateSuccess'));
      setEditTarget(null);
      load();
    } catch {
      toast.danger(t('common.error'));
    } finally { setEditSaving(false); }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await api.del(`/api/soul/${deleteTarget.id}`);
      toast.success(t('memory.deleteSuccess'));
      setDeleteTarget(null);
      load();
    } catch {
      toast.danger(t('common.error'));
    }
  };

  if (loading) {
    return <div className="flex items-center justify-center h-full"><Spinner size="lg" /></div>;
  }

  return (
    <div className="flex flex-col h-full pb-2">
      {/* Search bar */}
      <div className="px-4 pt-3">
        <TextArea
          className="text-[14px]"
          rows={1}
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          placeholder={t('common.search') + '...'}
        />
      </div>

      <div className="flex-1 overflow-y-auto p-4 space-y-3">
        {entries.length === 0 ? (
          <div className="flex items-center justify-center h-32 text-default-400">
            {t('common.noData')}
          </div>
        ) : entries.map(entry => (
          <div key={entry.id} className="bg-content2 rounded-2xl px-3 py-2 text-sm group">
            <div className="whitespace-pre-wrap break-words">{entry.content}</div>
            <div className="flex flex-wrap items-center gap-1 mt-1">
              {/* Weight indicator */}
              <Chip size="sm" variant="soft" color={getWeightColor(entry.weight)}>
                w:{entry.weight.toFixed(2)}
              </Chip>
              {/* Tags */}
              {parseTags(entry.tags).map(tag => (
                <Chip key={tag} size="sm" variant="soft" color="default">
                  {tag}
                </Chip>
              ))}
              {entry.source !== 'user' && (
                <Chip size="sm" variant="soft" color="accent">
                  {entry.source}
                </Chip>
              )}
            </div>
            <div className="flex items-center justify-between mt-1">
              <span className="text-[10px] text-default-400">
                {new Date(entry.createdAt).toLocaleString()}
              </span>
              <div className="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                <Button
                  size="sm" variant="ghost" isIconOnly
                  onPress={() => { setEditTarget(entry); setEditContent(entry.content); setEditTags(entry.tags || ''); setEditWeight(entry.weight); }}
                >
                  <Pencil className="w-3.5 h-3.5" />
                </Button>
                <Button
                  size="sm" variant="ghost" isIconOnly
                  onPress={() => setDeleteTarget(entry)}
                >
                  <TrashBin className="w-3.5 h-3.5" />
                </Button>
              </div>
            </div>
          </div>
        ))}
      </div>

      <div className="border-t p-3 space-y-2">
        <div className="flex gap-2 items-end">
          <TextArea
            className="flex-1 text-[16px]"
            rows={2}
            value={newContent}
            onChange={(e) => setNewContent(e.target.value)}
            placeholder={t('memory.content')}
          />
          <Button size="sm" onPress={handleAdd} isDisabled={adding || !newContent.trim()}>
            {adding ? <Spinner size="sm" /> : t('memory.addSoul')}
          </Button>
        </div>
        <div className="flex gap-2">
          <TextArea
            className="flex-1 text-[12px]"
            rows={1}
            value={newTags}
            onChange={(e) => setNewTags(e.target.value)}
            placeholder="Tags: emotion:happy, scene:work"
          />
          <div className="flex items-center gap-1">
            <span className="text-[11px] text-default-400 w-4">{newWeight.toFixed(1)}</span>
            <input
              type="range"
              min="0"
              max="1"
              step="0.1"
              value={newWeight}
              onChange={(e) => setNewWeight(parseFloat(e.target.value))}
              className="w-16 h-4"
            />
          </div>
        </div>
      </div>

      {/* Edit modal */}
      <Modal>
        <Modal.Backdrop isOpen={!!editTarget} onOpenChange={(open) => { if (!open) setEditTarget(null); }}>
          <Modal.Container>
            <Modal.Dialog>
              <Modal.Header><Modal.Heading>{t('memory.editSoul')}</Modal.Heading></Modal.Header>
              <Modal.Body className="space-y-3">
                <TextArea
                  className="font-mono w-full"
                  rows={6}
                  value={editContent}
                  onChange={(e) => setEditContent(e.target.value)}
                />
                <TextArea
                  className="w-full"
                  rows={1}
                  value={editTags}
                  onChange={(e) => setEditTags(e.target.value)}
                  placeholder="Tags (comma-separated)"
                />
                <div className="flex items-center gap-2">
                  <span className="text-sm">{t('memory.weight')}: {editWeight.toFixed(1)}</span>
                  <input
                    type="range"
                    min="0"
                    max="1"
                    step="0.1"
                    value={editWeight}
                    onChange={(e) => setEditWeight(parseFloat(e.target.value))}
                    className="flex-1 h-4"
                  />
                </div>
              </Modal.Body>
              <Modal.Footer>
                <Button variant="ghost" size="sm" onPress={() => setEditTarget(null)}>{t('common.cancel')}</Button>
                <Button size="sm" onPress={handleEdit} isDisabled={editSaving || !editContent.trim()}>
                  {editSaving ? <Spinner size="sm" /> : t('common.save')}
                </Button>
              </Modal.Footer>
            </Modal.Dialog>
          </Modal.Container>
        </Modal.Backdrop>
      </Modal>

      {/* Delete confirmation modal */}
      <Modal>
        <Modal.Backdrop isOpen={!!deleteTarget} onOpenChange={(open) => { if (!open) setDeleteTarget(null); }}>
          <Modal.Container size="sm">
            <Modal.Dialog>
              <Modal.Header><Modal.Heading>{t('memory.deleteSoul')}</Modal.Heading></Modal.Header>
              <Modal.Body><p className="text-sm">{t('memory.deleteConfirm')}</p></Modal.Body>
              <Modal.Footer>
                <Button variant="ghost" size="sm" onPress={() => setDeleteTarget(null)}>{t('common.cancel')}</Button>
                <Button variant="danger" size="sm" onPress={handleDelete}>{t('common.delete')}</Button>
              </Modal.Footer>
            </Modal.Dialog>
          </Modal.Container>
        </Modal.Backdrop>
      </Modal>
    </div>
  );
}
