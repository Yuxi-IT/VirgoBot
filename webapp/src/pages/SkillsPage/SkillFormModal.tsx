import { useState, useEffect } from 'react';
import { Button, Modal, TextField, Label, Input, toast, Checkbox, TextArea } from '@heroui/react';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import type { SkillParam, SkillJson, SkillInfo, SkillDetailResponse } from './types';
import { TrashBin } from '@gravity-ui/icons';

const HTTP_METHODS = ['GET', 'POST', 'PUT', 'DELETE', 'PATCH'];

interface SkillFormModalProps {
  isOpen: boolean;
  onOpenChange: () => void;
  onClose: () => void;
  editingSkill: SkillInfo | null;
  onSaved: () => void;
}

function SkillFormModal({ isOpen, onOpenChange, onClose, editingSkill, onSaved }: SkillFormModalProps) {
  const { t } = useI18n();

  const [formName, setFormName] = useState('');
  const [formDescription, setFormDescription] = useState('');
  const [formCommand, setFormCommand] = useState('');
  const [formParams, setFormParams] = useState<SkillParam[]>([]);
  const [formError, setFormError] = useState('');
  const [formMode, setFormMode] = useState<'command' | 'http'>('command');
  const [formHttpMethod, setFormHttpMethod] = useState('GET');
  const [formHttpUrl, setFormHttpUrl] = useState('');
  const [formHttpHeadersText, setFormHttpHeadersText] = useState('');
  const [formHttpBody, setFormHttpBody] = useState('');

  useEffect(() => {
    if (isOpen && editingSkill) {
      loadSkillDetail();
    } else if (isOpen && !editingSkill) {
      resetForm();
    }
  }, [isOpen, editingSkill]);

  const resetForm = () => {
    setFormName('');
    setFormDescription('');
    setFormCommand('');
    setFormParams([]);
    setFormError('');
    setFormMode('command');
    setFormHttpMethod('GET');
    setFormHttpUrl('');
    setFormHttpHeadersText('');
    setFormHttpBody('');
  };

  const loadSkillDetail = async () => {
    if (!editingSkill) return;
    try {
      const skillName = editingSkill.fileName.replace('.json', '');
      const res = await api.get<SkillDetailResponse>(`/api/skills/${skillName}`);
      if (res.success) {
        const parsed: SkillJson = JSON.parse(res.data.content);
        setFormName(parsed.name);
        setFormDescription(parsed.description || '');
        setFormParams(parsed.parameters || []);
        setFormError('');

        const mode = parsed.mode === 'http' ? 'http' : 'command';
        setFormMode(mode);

        if (mode === 'http' && parsed.http) {
          setFormHttpMethod(parsed.http.method || 'GET');
          setFormHttpUrl(parsed.http.url || '');
          setFormHttpBody(parsed.http.body || '');
          const headersText = parsed.http.headers
            ? Object.entries(parsed.http.headers).map(([key, value]) => `${key}: ${value}`).join('\n')
            : '';
          setFormHttpHeadersText(headersText);
          setFormCommand('');
        } else {
          setFormCommand(parsed.command || '');
          setFormHttpMethod('GET');
          setFormHttpUrl('');
          setFormHttpHeadersText('');
          setFormHttpBody('');
        }
      }
    } catch {
      toast.danger(t('common.error'));
    }
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

  const parseHeadersText = (text: string): Record<string, string> => {
    const headers: Record<string, string> = {};
    text.split('\n').forEach(line => {
      const trimmed = line.trim();
      if (!trimmed) return;
      const colonIndex = trimmed.indexOf(':');
      if (colonIndex > 0) {
        const key = trimmed.substring(0, colonIndex).trim();
        const value = trimmed.substring(colonIndex + 1).trim();
        if (key) headers[key] = value;
      }
    });
    return headers;
  };

  const handleSave = async () => {
    setFormError('');

    if (!formName.trim()) {
      setFormError(t('skills.nameRequired'));
      return;
    }

    if (formMode === 'command' && !formCommand.trim()) {
      setFormError(t('skills.commandRequired'));
      return;
    }

    if (formMode === 'http' && !formHttpUrl.trim()) {
      setFormError(t('skills.urlRequired'));
      return;
    }

    let skillJson: SkillJson;

    if (formMode === 'http') {
      const headersObj = parseHeadersText(formHttpHeadersText);

      skillJson = {
        name: formName.trim(),
        description: formDescription.trim(),
        parameters: formParams.filter(p => p.name.trim()),
        mode: 'http',
        http: {
          method: formHttpMethod,
          url: formHttpUrl.trim(),
          headers: headersObj,
          body: formHttpBody,
        },
      };
    } else {
      skillJson = {
        name: formName.trim(),
        description: formDescription.trim(),
        parameters: formParams.filter(p => p.name.trim()),
        command: formCommand.trim(),
      };
    }

    const content = JSON.stringify(skillJson, null, 2);

    try {
      if (editingSkill) {
        const skillName = editingSkill.fileName.replace('.json', '');
        await api.put(`/api/skills/${skillName}`, { name: formName.trim(), content });
        toast.success(t('skills.updateSuccess'));
      } else {
        await api.post('/api/skills', { name: formName.trim(), content });
        toast.success(t('skills.addSuccess'));
      }
      onClose();
      onSaved();
    } catch {
      toast.danger(t('common.error'));
    }
  };

  const isSaveDisabled = !formName.trim() ||
    (formMode === 'command' && !formCommand.trim()) ||
    (formMode === 'http' && !formHttpUrl.trim());

  return (
    <Modal>
      <Modal.Backdrop isOpen={isOpen} onOpenChange={onOpenChange}>
        <Modal.Container size="lg" className="w-5xl">
          <Modal.Dialog>
            <Modal.Header>
              <Modal.Heading>
                {editingSkill ? t('skills.editSkill') : t('skills.addSkill')}
              </Modal.Heading>
            </Modal.Header>
            <Modal.Body>
              <div className="space-y-4 p-2">
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

                {/* Mode Toggle */}
                <div>
                  <Label>{t('skills.mode')}</Label>
                  <div className="flex gap-2 mt-1">
                    <Button
                      size="sm"
                      variant={formMode === 'command' ? 'primary' : 'secondary'}
                      onPress={() => setFormMode('command')}
                    >
                      {t('skills.modeCommand')}
                    </Button>
                    <Button
                      size="sm"
                      variant={formMode === 'http' ? 'primary' : 'secondary'}
                      onPress={() => setFormMode('http')}
                    >
                      {t('skills.modeHttp')}
                    </Button>
                  </div>
                </div>

                {/* Command Mode Fields */}
                {formMode === 'command' && (
                  <TextField isRequired value={formCommand} onChange={setFormCommand}>
                    <Label>{t('skills.command')}</Label>
                    <Input placeholder="e.g. ffmpeg -i {{input}} {{output}}" className="font-mono" />
                  </TextField>
                )}

                {/* HTTP Mode Fields */}
                {formMode === 'http' && (
                  <div className="space-y-4">
                    <div>
                      <Label>{t('skills.httpMethod')}</Label>
                      <div className="flex gap-1 mt-1">
                        {HTTP_METHODS.map(m => (
                          <Button
                            key={m}
                            size="sm"
                            variant={formHttpMethod === m ? 'primary' : 'secondary'}
                            onPress={() => setFormHttpMethod(m)}
                          >
                            {m}
                          </Button>
                        ))}
                      </div>
                    </div>

                    <TextField isRequired value={formHttpUrl} onChange={setFormHttpUrl}>
                      <Label>{t('skills.httpUrl')}</Label>
                      <Input placeholder="https://api.example.com/{{param}}" className="font-mono" />
                    </TextField>

                    <div>
                      <Label>{t('skills.httpHeaders')}</Label>
                      <TextArea
                        className="w-[99%] m-1 p-2 border border-gray-300 dark:border-gray-600 font-mono text-sm bg-transparent min-h-[80px] resize-y"
                        value={formHttpHeadersText}
                        onChange={(e) => setFormHttpHeadersText(e.target.value)}
                        placeholder={"Content-Type: application/json\nAuthorization: Bearer {{token}}"}
                        rows={3}
                      />
                    </div>

                    {['POST', 'PUT', 'PATCH'].includes(formHttpMethod) && (
                      <div>
                        <Label>{t('skills.httpBody')}</Label>
                        <TextArea
                          className="w-[99%] m-1 p-2 border border-gray-300 dark:border-gray-600 font-mono text-sm bg-transparent min-h-[80px] resize-y"
                          value={formHttpBody}
                          onChange={(e) => setFormHttpBody(e.target.value)}
                          placeholder='{"key": "{{value}}"}'
                          rows={4}
                        />
                      </div>
                    )}
                  </div>
                )}

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
                          <div className="flex-1 w-24">
                            <TextField value={param.name} onChange={(v) => updateParam(index, 'name', v)}>
                              <Label className="text-xs">{t('skills.paramName')}</Label>
                              <Input placeholder="name" className="font-mono" />
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
                              <Checkbox isSelected={param.required} onChange={(checked) => updateParam(index, 'required', checked)}>
                                <Checkbox.Control>
                                  <Checkbox.Indicator />
                                </Checkbox.Control>
                                <Checkbox.Content>
                                  <Label htmlFor="basic-terms">{t('skills.paramRequired')}</Label>
                                </Checkbox.Content>
                              </Checkbox>
                              
                            </label>
                            <Button size="sm" isIconOnly variant="danger" onPress={() => removeParam(index)}>
                              <TrashBin/>
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
              <Button variant="secondary" onPress={onClose}>
                {t('common.cancel')}
              </Button>
              <Button onPress={handleSave} isDisabled={isSaveDisabled}>
                {t('common.save')}
              </Button>
            </Modal.Footer>
          </Modal.Dialog>
        </Modal.Container>
      </Modal.Backdrop>
    </Modal>
  );
}

export default SkillFormModal;
