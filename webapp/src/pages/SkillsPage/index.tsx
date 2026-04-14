import { useEffect, useState } from 'react';
import { Card, Button, Chip, Spinner, Table, Modal, TextField, Label, Input, SearchField, toast } from '@heroui/react';
import { useOverlayState } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';

interface SkillInfo {
  fileName: string;
  name: string;
  description: string;
  command: string;
  parameterCount: number;
}

interface SkillsResponse {
  success: boolean;
  data: SkillInfo[];
}

interface SkillDetailResponse {
  success: boolean;
  data: {
    fileName: string;
    content: string;
  };
}

interface SkillParam {
  name: string;
  type: string;
  description: string;
  required: boolean;
}

interface SkillJson {
  name: string;
  description: string;
  parameters: SkillParam[];
  command: string;
}

function SkillsPage() {
  const { t } = useI18n();
  const [skills, setSkills] = useState<SkillInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [editingSkill, setEditingSkill] = useState<string | null>(null); // skill name being edited
  const [deletingSkill, setDeletingSkill] = useState<SkillInfo | null>(null);

  const formModal = useOverlayState();
  const deleteModal = useOverlayState();

  // Form state
  const [formName, setFormName] = useState('');
  const [formDescription, setFormDescription] = useState('');
  const [formCommand, setFormCommand] = useState('');
  const [formParams, setFormParams] = useState<SkillParam[]>([]);
  const [formError, setFormError] = useState('');

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
    setFormName('');
    setFormDescription('');
    setFormCommand('');
    setFormParams([]);
    setFormError('');
    formModal.open();
  };

  const openEditModal = async (skill: SkillInfo) => {
    try {
      const skillName = skill.fileName.replace('.json', '');
      const res = await api.get<SkillDetailResponse>(`/api/skills/${skillName}`);
      if (res.success) {
        const parsed: SkillJson = JSON.parse(res.data.content);
        setEditingSkill(skillName);
        setFormName(parsed.name);
        setFormDescription(parsed.description || '');
        setFormCommand(parsed.command || '');
        setFormParams(parsed.parameters || []);
        setFormError('');
        formModal.open();
      }
    } catch {
      toast.danger(t('common.error'));
    }
  };

  const openDeleteModal = (skill: SkillInfo) => {
    setDeletingSkill(skill);
    deleteModal.open();
  };

  const addParam = () => {
    setFormParams([...formParams, { name: '', type: 'string', description: '', required: false }]);
  };

  const updateParam = (index: number, field: keyof SkillParam, value: string | boolean) => {
    const updated = [...formParams];
    (updated[index] as any)[field] = value;
    setFormParams(updated);
  };

  const removeParam = (index: number) => {
    setFormParams(formParams.filter((_, i) => i !== index));
  };

  const handleSave = async () => {
    setFormError('');

    if (!formName.trim()) {
      setFormError(t('skills.nameRequired'));
      return;
    }

    const skillJson: SkillJson = {
      name: formName.trim(),
      description: formDescription.trim(),
      parameters: formParams.filter(p => p.name.trim()),
      command: formCommand.trim(),
    };

    const content = JSON.stringify(skillJson, null, 2);

    try {
      if (editingSkill) {
        await api.put(`/api/skills/${editingSkill}`, { name: formName.trim(), content });
        toast.success(t('skills.updateSuccess'));
      } else {
        await api.post('/api/skills', { name: formName.trim(), content });
        toast.success(t('skills.addSuccess'));
      }
      formModal.close();
      await loadSkills();
    } catch {
      toast.danger(t('common.error'));
    }
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

  const filteredSkills = searchQuery
    ? skills.filter(s =>
        s.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
        s.description.toLowerCase().includes(searchQuery.toLowerCase()) ||
        s.command.toLowerCase().includes(searchQuery.toLowerCase())
      )
    : skills;

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

        {/* Search */}
        <div className="mb-4">
          <SearchField value={searchQuery} onChange={setSearchQuery}>
            <SearchField.Group>
              <SearchField.SearchIcon />
              <SearchField.Input placeholder={t('common.search')} />
              <SearchField.ClearButton />
            </SearchField.Group>
          </SearchField>
        </div>

        {/* Skills Table */}
        <Card>
          <Card.Content>
            {loading ? (
              <div className="flex justify-center py-8">
                <Spinner size="lg" />
              </div>
            ) : filteredSkills.length === 0 ? (
              <p className="text-center py-8 text-gray-500">{t('common.noData')}</p>
            ) : (
              <Table>
                <Table.ScrollContainer>
                  <Table.Content aria-label="Skills">
                    <Table.Header>
                      <Table.Column>{t('skills.name')}</Table.Column>
                      <Table.Column>{t('skills.description')}</Table.Column>
                      <Table.Column>{t('skills.command')}</Table.Column>
                      <Table.Column>{t('skills.parameterCount')}</Table.Column>
                      <Table.Column>{t('common.actions')}</Table.Column>
                    </Table.Header>
                    <Table.Body>
                      {filteredSkills.map(skill => (
                        <Table.Row key={skill.fileName}>
                          <Table.Cell>
                            <span className="font-medium font-mono">{skill.name}</span>
                          </Table.Cell>
                          <Table.Cell>
                            <div className="max-w-xs truncate" title={skill.description}>
                              {skill.description}
                            </div>
                          </Table.Cell>
                          <Table.Cell>
                            <Chip size="sm" variant="soft">
                              <span className="font-mono text-xs">{skill.command}</span>
                            </Chip>
                          </Table.Cell>
                          <Table.Cell>
                            <Chip size="sm" color="accent">{skill.parameterCount}</Chip>
                          </Table.Cell>
                          <Table.Cell>
                            <div className="flex gap-2">
                              <Button size="sm" variant="secondary" onPress={() => openEditModal(skill)}>
                                {t('common.edit')}
                              </Button>
                              <Button size="sm" variant="danger" onPress={() => openDeleteModal(skill)}>
                                {t('common.delete')}
                              </Button>
                            </div>
                          </Table.Cell>
                        </Table.Row>
                      ))}
                    </Table.Body>
                  </Table.Content>
                </Table.ScrollContainer>
              </Table>
            )}
          </Card.Content>
        </Card>

        {/* Add/Edit Modal */}
        <Modal>
          <Modal.Backdrop isOpen={formModal.isOpen} onOpenChange={formModal.toggle}>
            <Modal.Container size="lg">
              <Modal.Dialog>
                <Modal.Header>
                  <Modal.Heading>
                    {editingSkill ? t('skills.editSkill') : t('skills.addSkill')}
                  </Modal.Heading>
                </Modal.Header>
                <Modal.Body>
                  <div className="space-y-4">
                    {formError && (
                      <div className="text-sm text-red-500 bg-red-50 dark:bg-red-900/20 p-2 rounded">
                        {formError}
                      </div>
                    )}

                    <TextField isRequired value={formName} onChange={setFormName}>
                      <Label>{t('skills.name')}</Label>
                      <Input placeholder="e.g. ffmpeg_convert" className="font-mono" />
                    </TextField>

                    <TextField value={formDescription} onChange={setFormDescription}>
                      <Label>{t('skills.description')}</Label>
                      <Input placeholder={t('skills.description')} />
                    </TextField>

                    <TextField isRequired value={formCommand} onChange={setFormCommand}>
                      <Label>{t('skills.command')}</Label>
                      <Input placeholder="e.g. ffmpeg -i {{input}} {{output}}" className="font-mono" />
                    </TextField>

                    {/* Parameters */}
                    <div>
                      <div className="flex items-center justify-between mb-2">
                        <Label>{t('skills.parameters')}</Label>
                        <Button size="sm" variant="secondary" onPress={addParam}>
                          {t('skills.addParam')}
                        </Button>
                      </div>
                      {formParams.length === 0 ? (
                        <p className="text-sm text-gray-400">{t('common.noData')}</p>
                      ) : (
                        <div className="space-y-3">
                          {formParams.map((param, index) => (
                            <div key={index} className="flex gap-2 items-end p-3 bg-gray-50 dark:bg-gray-800/50 rounded-lg">
                              <div className="flex-1">
                                <TextField value={param.name} onChange={(v) => updateParam(index, 'name', v)}>
                                  <Label className="text-xs">{t('skills.paramName')}</Label>
                                  <Input placeholder="name" className="font-mono" />
                                </TextField>
                              </div>
                              <div className="w-24">
                                <TextField value={param.type} onChange={(v) => updateParam(index, 'type', v)}>
                                  <Label className="text-xs">{t('skills.paramType')}</Label>
                                  <Input placeholder="string" className="font-mono" />
                                </TextField>
                              </div>
                              <div className="flex-1">
                                <TextField value={param.description} onChange={(v) => updateParam(index, 'description', v)}>
                                  <Label className="text-xs">{t('skills.paramDesc')}</Label>
                                  <Input placeholder={t('skills.paramDesc')} />
                                </TextField>
                              </div>
                              <div className="flex items-center gap-2 pb-1">
                                <label className="flex items-center gap-1 text-xs cursor-pointer">
                                  <input
                                    type="checkbox"
                                    checked={param.required}
                                    onChange={(e) => updateParam(index, 'required', e.target.checked)}
                                    className="rounded"
                                  />
                                  {t('skills.paramRequired')}
                                </label>
                                <Button size="sm" variant="danger" onPress={() => removeParam(index)}>
                                  ×
                                </Button>
                              </div>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  </div>
                </Modal.Body>
                <Modal.Footer>
                  <Button variant="secondary" onPress={formModal.close}>
                    {t('common.cancel')}
                  </Button>
                  <Button onPress={handleSave} isDisabled={!formName.trim() || !formCommand.trim()}>
                    {t('common.save')}
                  </Button>
                </Modal.Footer>
              </Modal.Dialog>
            </Modal.Container>
          </Modal.Backdrop>
        </Modal>

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
