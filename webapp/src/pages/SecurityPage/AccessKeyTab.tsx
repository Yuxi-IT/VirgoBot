import { useEffect, useState } from 'react';
import { Button, Card, Switch, Spinner, toast, TextField, Label, Input, TextArea, Modal } from '@heroui/react';
import { Plus, TrashBin, Copy } from '@gravity-ui/icons';
import { api } from '../../services/api';
import { useI18n } from '../../i18n';

interface AccessKey {
  id: string;
  name: string;
  key: string;
  note: string;
  enabled: boolean;
  createdAt: string;
}

interface AccessKeyListResponse {
  success: boolean;
  data: AccessKey[];
}

interface AccessKeyCreateResponse {
  success: boolean;
  data: AccessKey & { key: string };
}

function AccessKeyTab({ active }: { active: boolean }) {
  const { t } = useI18n();
  const [keys, setKeys] = useState<AccessKey[]>([]);
  const [loading, setLoading] = useState(true);
  const [showCreate, setShowCreate] = useState(false);
  const [showKeyDisplay, setShowKeyDisplay] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [newKeyValue, setNewKeyValue] = useState('');
  const [deleteId, setDeleteId] = useState('');
  const [createName, setCreateName] = useState('');
  const [createNote, setCreateNote] = useState('');
  const [creating, setCreating] = useState(false);

  useEffect(() => {
    if (active) loadKeys();
  }, [active]);

  const loadKeys = async () => {
    try {
      setLoading(true);
      const res = await api.get<AccessKeyListResponse>('/api/access-keys');
      if (res.success) setKeys(res.data);
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = async () => {
    if (!createName.trim()) {
      toast.danger(t('security.nameRequired'));
      return;
    }
    setCreating(true);
    try {
      const res = await api.post<AccessKeyCreateResponse>('/api/access-keys', {
        name: createName,
        note: createNote,
      });
      if (res.success) {
        setNewKeyValue(res.data.key);
        setShowCreate(false);
        setShowKeyDisplay(true);
        setCreateName('');
        setCreateNote('');
        toast.success(t('security.createSuccess'));
        loadKeys();
      }
    } finally {
      setCreating(false);
    }
  };

  const handleDelete = async () => {
    await api.del(`/api/access-keys/${deleteId}`);
    toast.success(t('security.deleteSuccess'));
    setShowDeleteConfirm(false);
    loadKeys();
  };

  const handleToggle = async (id: string) => {
    await api.put(`/api/access-keys/${id}/toggle`, {});
    toast.success(t('security.toggleSuccess'));
    loadKeys();
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    toast.success(t('security.copied'));
  };

  if (loading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  return (
    <div className="mt-4">
      <div className="flex justify-between items-center mb-4">
        <p className="text-sm text-gray-500">
          WebSocket AccessKey
        </p>
        <Button variant="primary" onPress={() => setShowCreate(true)}>
          <Plus className="w-4 h-4 mr-1" />
          {t('security.createKey')}
        </Button>
      </div>

      {keys.length === 0 ? (
        <Card>
          <div className="p-8 text-center text-gray-400">
            {t('security.noKeys')}
          </div>
        </Card>
      ) : (
        <Card>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-200 dark:border-gray-700">
                  <th className="text-left p-3 font-medium">{t('security.name')}</th>
                  <th className="text-left p-3 font-medium">{t('security.key')}</th>
                  <th className="text-left p-3 font-medium">{t('security.note')}</th>
                  <th className="text-left p-3 font-medium">{t('security.createdAt')}</th>
                  <th className="text-center p-3 font-medium">{t('security.enabled')}</th>
                  <th className="text-center p-3 font-medium">{t('common.actions')}</th>
                </tr>
              </thead>
              <tbody>
                {keys.map((k) => (
                  <tr key={k.id} className="border-b border-gray-100 dark:border-gray-800">
                    <td className="p-3">{k.name}</td>
                    <td className="p-3 font-mono text-xs">{k.key}</td>
                    <td className="p-3 text-gray-500">{k.note || '-'}</td>
                    <td className="p-3 text-gray-500">
                      {new Date(k.createdAt).toLocaleDateString()}
                    </td>
                    <td className="p-3 text-center">
                      <Switch
                        isSelected={k.enabled}
                        onChange={() => handleToggle(k.id)}
                      />
                    </td>
                    <td className="p-3 text-center">
                      <Button
                        variant="danger"
                        onPress={() => {
                          setDeleteId(k.id);
                          setShowDeleteConfirm(true);
                        }}
                      >
                        <TrashBin className="w-4 h-4" />
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      )}

      {/* Create Modal */}
      <Modal>
        <Modal.Backdrop isOpen={showCreate} onOpenChange={(open) => { if (!open) setShowCreate(false); }}>
          <Modal.Container size="lg">
            <Modal.Dialog>
              <Modal.Header>
                <Modal.Heading>{t('security.createKey')}</Modal.Heading>
              </Modal.Header>
              <Modal.Body>
                <div className="flex flex-col gap-4">
                  <TextField value={createName} onChange={setCreateName}>
                    <Label>{t('security.name')}</Label>
                    <Input />
                  </TextField>
                  <TextField value={createNote} onChange={setCreateNote}>
                    <Label>{t('security.note')}</Label>
                    <TextArea placeholder={t('security.notePlaceholder')} />
                  </TextField>
                </div>
              </Modal.Body>
              <Modal.Footer>
                <Button variant="secondary" onPress={() => setShowCreate(false)}>
                  {t('common.cancel')}
                </Button>
                <Button variant="primary" onPress={handleCreate} isDisabled={creating}>
                  {creating ? t('common.loading') : t('common.confirm')}
                </Button>
              </Modal.Footer>
            </Modal.Dialog>
          </Modal.Container>
        </Modal.Backdrop>
      </Modal>

      {/* Key Display Modal */}
      <Modal>
        <Modal.Backdrop isOpen={showKeyDisplay} onOpenChange={(open) => { if (!open) setShowKeyDisplay(false); }}>
          <Modal.Container>
            <Modal.Dialog>
              <Modal.Header>
                <Modal.Heading>{t('security.keyCreated')}</Modal.Heading>
              </Modal.Header>
              <Modal.Body>
                <p className="text-sm text-gray-500 mb-3">
                  {t('security.keyCreatedHint')}
                </p>
                <div className="flex items-center gap-2 p-3 bg-gray-100 dark:bg-gray-800 rounded-lg">
                  <code className="flex-1 text-sm font-mono break-all select-all">
                    {newKeyValue}
                  </code>
                  <Button variant="ghost" onPress={() => copyToClipboard(newKeyValue)}>
                    <Copy className="w-4 h-4" />
                  </Button>
                </div>
              </Modal.Body>
              <Modal.Footer>
                <Button variant="primary" onPress={() => setShowKeyDisplay(false)}>
                  {t('common.confirm')}
                </Button>
              </Modal.Footer>
            </Modal.Dialog>
          </Modal.Container>
        </Modal.Backdrop>
      </Modal>

      {/* Delete Confirm Modal */}
      <Modal>
        <Modal.Backdrop isOpen={showDeleteConfirm} onOpenChange={(open) => { if (!open) setShowDeleteConfirm(false); }}>
          <Modal.Container>
            <Modal.Dialog>
              <Modal.Header>
                <Modal.Heading>{t('security.deleteKey')}</Modal.Heading>
              </Modal.Header>
              <Modal.Body>
                <p>{t('security.deleteConfirm')}</p>
              </Modal.Body>
              <Modal.Footer>
                <Button variant="secondary" onPress={() => setShowDeleteConfirm(false)}>
                  {t('common.cancel')}
                </Button>
                <Button variant="danger" onPress={handleDelete}>
                  {t('common.delete')}
                </Button>
              </Modal.Footer>
            </Modal.Dialog>
          </Modal.Container>
        </Modal.Backdrop>
      </Modal>
    </div>
  );
}

export default AccessKeyTab;
