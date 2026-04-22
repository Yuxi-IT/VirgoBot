import { useEffect, useState } from 'react';
import { Button, Spinner, TextArea, Modal, toast } from '@heroui/react';
import { TrashBin, Pencil } from '@gravity-ui/icons';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';

interface SoulEntry {
  id: number;
  content: string;
  createdAt: string;
}

export default function SoulPanel() {
  const { t } = useI18n();
  const [entries, setEntries] = useState<SoulEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [newContent, setNewContent] = useState('');
  const [adding, setAdding] = useState(false);
  const [editTarget, setEditTarget] = useState<SoulEntry | null>(null);
  const [editContent, setEditContent] = useState('');
  const [editSaving, setEditSaving] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<SoulEntry | null>(null);

  const load = async () => {
    try {
      setLoading(true);
      const res = await api.get<{ success: boolean; data: SoulEntry[] }>('/api/soul');
      if (res.success) setEntries(res.data);
    } catch { /* silent */ } finally { setLoading(false); }
  };

  useEffect(() => { load(); }, []);

  const handleAdd = async () => {
    if (!newContent.trim()) return;
    setAdding(true);
    try {
      await api.post('/api/soul', { content: newContent.trim() });
      toast.success(t('memory.addSuccess'));
      setNewContent('');
      load();
    } catch {
      toast.danger(t('common.error'));
    } finally { setAdding(false); }
  };

  const handleEdit = async () => {
    if (!editTarget || !editContent.trim()) return;
    setEditSaving(true);
    try {
      await api.put(`/api/soul/${editTarget.id}`, { content: editContent.trim() });
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
      <div className="flex-1 overflow-y-auto p-4 space-y-3">
        {entries.length === 0 ? (
          <div className="flex items-center justify-center h-32 text-default-400">
            {t('common.noData')}
          </div>
        ) : entries.map(entry => (
          <div key={entry.id} className="bg-content2 rounded-2xl px-3 py-2 text-sm group">
            <div className="whitespace-pre-wrap break-words">{entry.content}</div>
            <div className="flex items-center justify-between mt-1">
              <span className="text-[10px] text-default-400">
                {new Date(entry.createdAt).toLocaleString()}
              </span>
              <div className="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                <Button
                  size="sm" variant="ghost" isIconOnly
                  onPress={() => { setEditTarget(entry); setEditContent(entry.content); }}
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

      <div className="border-t p-3">
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
      </div>

      {/* Edit modal */}
      <Modal>
        <Modal.Backdrop isOpen={!!editTarget} onOpenChange={(open) => { if (!open) setEditTarget(null); }}>
          <Modal.Container>
            <Modal.Dialog>
              <Modal.Header><Modal.Heading>{t('memory.editSoul')}</Modal.Heading></Modal.Header>
              <Modal.Body>
                <TextArea
                  className="font-mono w-full"
                  rows={6}
                  value={editContent}
                  onChange={(e) => setEditContent(e.target.value)}
                />
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
