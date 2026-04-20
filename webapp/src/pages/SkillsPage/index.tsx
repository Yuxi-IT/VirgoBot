import { useEffect, useState, useRef } from 'react';
import { Button, Modal, toast, TextField, Label, Input } from '@heroui/react';
import { useOverlayState } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import SkillsTable from './SkillsTable';
import SkillFormModal from './SkillFormModal';
import type { SkillInfo, SkillsResponse } from './types';

function SkillsPage() {
  const { t } = useI18n();
  const [skills, setSkills] = useState<SkillInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [editingSkill, setEditingSkill] = useState<SkillInfo | null>(null);
  const [deletingSkill, setDeletingSkill] = useState<SkillInfo | null>(null);
  const [importUrl, setImportUrl] = useState('');
  const [importing, setImporting] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const formModal = useOverlayState();
  const deleteModal = useOverlayState();
  const importModal = useOverlayState();

  useEffect(() => {
    loadSkills();
  }, []);

  const loadSkills = async () => {
    try {
      setLoading(true);
      const res = await api.get<SkillsResponse>('/api/skills');
      if (res.success) {
        setSkills(res.data);
      }
    } catch {
      // silently fail
    } finally {
      setLoading(false);
    }
  };

  const openAddModal = () => {
    setEditingSkill(null);
    formModal.open();
  };

  const openEditModal = (skill: SkillInfo) => {
    setEditingSkill(skill);
    formModal.open();
  };

  const openDeleteModal = (skill: SkillInfo) => {
    setDeletingSkill(skill);
    deleteModal.open();
  };

  const handleDelete = async () => {
    if (!deletingSkill) return;
    try {
      const skillName = deletingSkill.fileName.replace('.json', '');
      await api.del(`/api/skills/${skillName}`);
      toast.success(t('skills.deleteSuccess'));
      deleteModal.close();
      await loadSkills();
    } catch {
      toast.danger(t('common.error'));
    }
  };

  const handleImportFromFile = () => {
    fileInputRef.current?.click();
  };

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    try {
      if (file.name.endsWith('.zip')) {
        const formData = new FormData();
        formData.append('file', file);
        const response = await fetch('/api/skills/import', {
          method: 'POST',
          body: formData,
        });
        const result = await response.json();
        if (!result.success) throw new Error(result.error || 'Import failed');
      } else {
        const text = await file.text();
        const skillData = JSON.parse(text);
        await api.post('/api/skills', skillData);
      }
      toast.success(t('skills.importSuccess'));
      await loadSkills();
    } catch {
      toast.danger(t('skills.importFailed'));
    }

    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const handleImportFromUrl = async () => {
    if (!importUrl.trim()) {
      toast.danger(t('skills.urlRequired'));
      return;
    }

    try {
      setImporting(true);
      const isZip = importUrl.trim().toLowerCase().endsWith('.zip');

      if (isZip) {
        const response = await fetch('/api/skills/import-url', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ url: importUrl.trim() }),
        });
        const result = await response.json();
        if (!result.success) throw new Error(result.error || 'Import failed');
      } else {
        const response = await fetch(importUrl);
        if (!response.ok) throw new Error('Failed to fetch');
        const skillData = await response.json();
        await api.post('/api/skills', skillData);
      }

      toast.success(t('skills.importSuccess'));
      importModal.close();
      setImportUrl('');
      await loadSkills();
    } catch {
      toast.danger(t('skills.importFailed'));
    } finally {
      setImporting(false);
    }
  };

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4">
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-2xl font-bold">{t('skills.title')}</h1>
            <p className="text-sm text-gray-500 mt-1">{t('skills.restartHint')}</p>
          </div>
          <div className="flex gap-2">
            <Button variant="secondary" onPress={handleImportFromFile}>
              {t('skills.importFromFile')}
            </Button>
            <Button variant="secondary" onPress={importModal.open}>
              {t('skills.importFromUrl')}
            </Button>
            <Button onPress={openAddModal}>
              {t('skills.addSkill')}
            </Button>
          </div>
        </div>

        <input
          ref={fileInputRef}
          type="file"
          accept=".json,.zip"
          onChange={handleFileChange}
          style={{ display: 'none' }}
        />

        <SkillsTable
          skills={skills}
          loading={loading}
          searchQuery={searchQuery}
          onSearchChange={setSearchQuery}
          onEdit={openEditModal}
          onDelete={openDeleteModal}
        />

        <SkillFormModal
          isOpen={formModal.isOpen}
          onOpenChange={formModal.toggle}
          onClose={formModal.close}
          editingSkill={editingSkill}
          onSaved={loadSkills}
        />

        <Modal>
          <Modal.Backdrop isOpen={importModal.isOpen} onOpenChange={importModal.toggle}>
            <Modal.Container size="lg">
              <Modal.Dialog>
                <Modal.Header>
                  <Modal.Heading>{t('skills.importFromUrl')}</Modal.Heading>
                </Modal.Header>
                <Modal.Body>
                  <TextField value={importUrl} onChange={setImportUrl}>
                    <Label>{t('skills.skillUrl')}</Label>
                    <Input placeholder="https://example.com/skill.json" />
                  </TextField>
                </Modal.Body>
                <Modal.Footer>
                  <Button variant="secondary" onPress={importModal.close}>
                    {t('common.cancel')}
                  </Button>
                  <Button onPress={handleImportFromUrl} isDisabled={importing}>
                    {importing ? t('skills.importing') : t('common.confirm')}
                  </Button>
                </Modal.Footer>
              </Modal.Dialog>
            </Modal.Container>
          </Modal.Backdrop>
        </Modal>

        <Modal>
          <Modal.Backdrop isOpen={deleteModal.isOpen} onOpenChange={deleteModal.toggle}>
            <Modal.Container size="lg">
              <Modal.Dialog role="alertdialog">
                <Modal.Header>
                  <Modal.Heading>{t('skills.deleteSkill')}</Modal.Heading>
                </Modal.Header>
                <Modal.Body>
                  <p>{t('skills.deleteConfirm')}</p>
                </Modal.Body>
                <Modal.Footer>
                  <Button variant="secondary" onPress={deleteModal.close}>
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
    </DefaultLayout>
  );
}

export default SkillsPage;
