import { useState, useEffect } from 'react';
import { Button, Modal, toast, TextField, Label, Input } from '@heroui/react';
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

const DEFAULT_SKILL_MD = `---
name: my-skill
description: Describe what this skill does
allowed-tools:
  - Read
  - Write
  - Bash
---

Instructions for the AI when this skill is activated.

Use $ARGUMENTS to reference user input.
`;

function SkillMdEditModal({ isOpen, onOpenChange, onClose, skill, onSaved }: SkillMdEditModalProps) {
  const { t } = useI18n();
  const [content, setContent] = useState('');
  const [skillName, setSkillName] = useState('');
  const [loading, setLoading] = useState(false);

  const isCreating = !skill;

  useEffect(() => {
    if (isOpen && skill) {
      loadContent();
    } else if (isOpen && !skill) {
      setContent(DEFAULT_SKILL_MD);
      setSkillName('');
    }
  }, [isOpen, skill]);

  const loadContent = async () => {
    if (!skill) return;
    setLoading(true);
    try {
      const name = skill.fileName.replace('/SKILL.md', '');
      const res = await api.get<SkillDetailResponse>(`/api/skills/${name}`);
      if (res.success) {
        setContent(res.data.content);
        setSkillName(name);
      }
    } catch {
      toast.danger(t('common.error'));
    } finally {
      setLoading(false);
    }
  };

  const extractNameFromContent = (md: string): string | null => {
    const match = md.match(/^name:\s*(.+)$/m);
    return match ? match[1].trim() : null;
  };

  const handleSave = async () => {
    if (isCreating) {
      // 从内容中提取 name，或使用手动输入的 name
      const nameFromContent = extractNameFromContent(content);
      const finalName = skillName.trim() || nameFromContent;

      if (!finalName) {
        toast.danger(t('skills.nameRequired'));
        return;
      }

      try {
        await api.post('/api/skills', {
          name: finalName,
          content,
          skillType: 'skill.md',
        });
        toast.success(t('skills.addSuccess'));
        onClose();
        onSaved();
      } catch {
        toast.danger(t('common.error'));
      }
    } else {
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
    }
  };

  return (
    <Modal>
      <Modal.Backdrop isOpen={isOpen} onOpenChange={onOpenChange}>
        <Modal.Container size="lg" className="w-4xl">
          <Modal.Dialog>
            <Modal.Header>
              <Modal.Heading>
                {isCreating ? t('skills.addSkill') : t('skills.editSkill')}
                {skill && <> — {skill.name}</>}
                <span className="ml-2 text-xs font-normal text-gray-400">SKILL.md</span>
              </Modal.Heading>
            </Modal.Header>
            <Modal.Body>
              {loading ? (
                <div className="flex justify-center py-8 text-gray-400">{t('common.loading')}</div>
              ) : (
                <div className="space-y-3">
                  {isCreating && (
                    <TextField value={skillName} onChange={setSkillName}>
                      <Label>{t('skills.dirName')}</Label>
                      <Input placeholder="my-skill" className="font-mono" />
                    </TextField>
                  )}
                  <div>
                    <Label className="text-xs text-gray-500 mb-1 block">
                      {t('skills.skillMdHint')}
                    </Label>
                    <textarea
                      className="w-full h-[55vh] p-3 font-mono text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-transparent resize-none focus:outline-none focus:ring-2 focus:ring-blue-500"
                      value={content}
                      onChange={e => setContent(e.target.value)}
                      spellCheck={false}
                    />
                  </div>
                </div>
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
