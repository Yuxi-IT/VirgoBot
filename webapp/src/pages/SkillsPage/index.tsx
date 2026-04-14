import { useEffect, useState } from 'react';
import { Button, Modal, toast } from '@heroui/react';
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

  const formModal = useOverlayState();
  const deleteModal = useOverlayState();

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

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4">
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-2xl font-bold">{t('skills.title')}</h1>
            <p className="text-sm text-gray-500 mt-1">{t('skills.restartHint')}</p>
          </div>
          <Button onPress={openAddModal}>
            {t('skills.addSkill')}
          </Button>
        </div>

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

        {/* Delete Confirmation Modal */}
        <Modal>
          <Modal.Backdrop isOpen={deleteModal.isOpen} onOpenChange={deleteModal.toggle}>
            <Modal.Container>
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
