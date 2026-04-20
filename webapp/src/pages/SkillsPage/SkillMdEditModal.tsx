import { useState, useEffect } from 'react';
import { Button, Modal, toast } from '@heroui/react';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import type { SkillInfo, SkillDetailResponse } from './types';

interface SkillMdEditModalProps {
  isOpen: boolean;
  onOpenChange: () => void;
  onClose: () => void;
  skill: SkillInfo | null;
  onSaved: () => void;
}

function SkillMdEditModal({ isOpen, onOpenChange, onClose, skill, onSaved }: SkillMdEditModalProps) {
  const { t } = useI18n();
  const [content, setContent] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (isOpen && skill) {
      loadContent();
    }
  }, [isOpen, skill]);

  const loadContent = async () => {
    if (!skill) return;
    setLoading(true);
    try {
      // fileName 格式为 "dirName/SKILL.md"，取 dirName
      const name = skill.fileName.replace('/SKILL.md', '');
      const res = await api.get<SkillDetailResponse>(`/api/skills/${name}`);
      if (res.success) setContent(res.data.content);
    } catch {
      toast.danger(t('common.error'));
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    if (!skill) return;
    try {
      const name = skill.fileName.replace('/SKILL.md', '');
      await api.put(`/api/skills/${name}`, { content });
      toast.success(t('skills.updateSuccess'));
      onClose();
      onSaved();
    } catch {
      toast.danger(t('common.error'));
    }
  };

  return (
    <Modal>
      <Modal.Backdrop isOpen={isOpen} onOpenChange={onOpenChange}>
        <Modal.Container size="xl" className="w-4xl">
          <Modal.Dialog>
            <Modal.Header>
              <Modal.Heading>
                {t('skills.editSkill')} — {skill?.name}
                <span className="ml-2 text-xs font-normal text-gray-400">SKILL.md</span>
              </Modal.Heading>
            </Modal.Header>
            <Modal.Body>
              {loading ? (
                <div className="flex justify-center py-8 text-gray-400">{t('common.loading')}</div>
              ) : (
                <textarea
                  className="w-full h-[60vh] p-3 font-mono text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-transparent resize-none focus:outline-none focus:ring-2 focus:ring-blue-500"
                  value={content}
                  onChange={e => setContent(e.target.value)}
                  spellCheck={false}
                />
              )}
            </Modal.Body>
            <Modal.Footer>
              <Button variant="secondary" onPress={onClose}>
                {t('common.cancel')}
              </Button>
              <Button onPress={handleSave} isDisabled={loading}>
                {t('common.save')}
              </Button>
            </Modal.Footer>
          </Modal.Dialog>
        </Modal.Container>
      </Modal.Backdrop>
    </Modal>
  );
}

export default SkillMdEditModal;
