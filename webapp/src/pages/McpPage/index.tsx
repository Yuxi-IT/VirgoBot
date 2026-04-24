import { useEffect, useState } from 'react';
import { Button, Card, Modal, Spinner, toast } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import { Plus, ArrowsRotateRight } from '@gravity-ui/icons';
import McpServerCard from './McpServerCard';
import McpFormModal from './McpFormModal';
import type { McpServer, McpServersResponse } from './types';

function McpPage() {
  const { t } = useI18n();
  const [servers, setServers] = useState<McpServer[]>([]);
  const [loading, setLoading] = useState(true);
  const [editingServer, setEditingServer] = useState<McpServer | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [deletingName, setDeletingName] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => { loadServers(); }, []);

  const loadServers = async () => {
    try {
      setLoading(true);
      const res = await api.get<McpServersResponse>('/api/mcp/servers');
      if (res.success) setServers(res.data);
    } catch { /* ignore */ } finally { setLoading(false); }
  };

  const handleSave = async (payload: {
    name: string; transport: string; command: string;
    args: string[]; env: Record<string, string>;
    url: string; enabled: boolean;
  }) => {
    setSaving(true);
    try {
      if (editingServer) {
        await api.put(`/api/mcp/servers/${encodeURIComponent(editingServer.name)}`, payload);
        toast.success(t('mcp.updateSuccess'));
      } else {
        await api.post('/api/mcp/servers', payload);
        toast.success(t('mcp.addSuccess'));
      }
      setShowForm(false);
      setEditingServer(null);
      await loadServers();
    } catch { toast.danger(t('common.error')); } finally { setSaving(false); }
  };

  const handleDelete = async () => {
    if (!deletingName) return;
    setDeleting(true);
    try {
      await api.del(`/api/mcp/servers/${encodeURIComponent(deletingName)}`);
      toast.success(t('mcp.deleteSuccess'));
      setDeletingName(null);
      await loadServers();
    } catch { toast.danger(t('common.error')); } finally { setDeleting(false); }
  };

  const handleRestart = async (name: string) => {
    try {
      await api.post(`/api/mcp/servers/${encodeURIComponent(name)}/restart`, {});
      toast.success(t('mcp.restartSuccess'));
      await loadServers();
    } catch { toast.danger(t('common.error')); }
  };

  if (loading) {
    return (
      <DefaultLayout>
        <div className="flex items-center justify-center h-[60vh]"><Spinner size="lg" /></div>
      </DefaultLayout>
    );
  }

  return (
    <DefaultLayout>
      <div className="max-w-4xl mx-auto p-4 sm:p-6 space-y-6">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-bold">{t('mcp.title')}</h1>
          <div className="flex gap-2">
            <Button size="sm" variant="secondary" onPress={loadServers}>
              <ArrowsRotateRight className="w-4 h-4" />
              {t('common.refresh')}
            </Button>
            <Button size="sm" onPress={() => { setEditingServer(null); setShowForm(true); }}>
              <Plus className="w-4 h-4" />
              {t('mcp.addServer')}
            </Button>
          </div>
        </div>

        {servers.length === 0 ? (
          <Card>
            <div className="p-8 text-center text-gray-500">{t('mcp.noServers')}</div>
          </Card>
        ) : (
          <div className="space-y-4">
            {servers.map(s => (
              <McpServerCard
                key={s.name}
                server={s}
                onEdit={() => { setEditingServer(s); setShowForm(true); }}
                onDelete={() => setDeletingName(s.name)}
                onRestart={() => handleRestart(s.name)}
              />
            ))}
          </div>
        )}
      </div>

      <McpFormModal
        isOpen={showForm}
        editingServer={editingServer}
        onClose={() => { setShowForm(false); setEditingServer(null); }}
        onSave={handleSave}
        saving={saving}
      />

      {/* Delete Confirm Modal */}
      <Modal>
        <Modal.Backdrop isOpen={!!deletingName} onOpenChange={(open) => { if (!open) setDeletingName(null); }}>
          <Modal.Container>
            <Modal.Dialog>
              <Modal.Header>
                <Modal.Heading>{t('mcp.deleteServer')}</Modal.Heading>
              </Modal.Header>
              <Modal.Body>
                <p>{t('mcp.deleteConfirm')}</p>
              </Modal.Body>
              <Modal.Footer>
                <Button variant="secondary" onPress={() => setDeletingName(null)} isDisabled={deleting}>{t('common.cancel')}</Button>
                <Button variant="danger" onPress={handleDelete} isDisabled={deleting}>
                  {deleting ? <Spinner size="sm" className="mr-1" /> : null}
                  {t('common.delete')}
                </Button>
              </Modal.Footer>
            </Modal.Dialog>
          </Modal.Container>
        </Modal.Backdrop>
      </Modal>
    </DefaultLayout>
  );
}

export default McpPage;
