import { useState, useEffect } from 'react';
import { Button, Modal, TextField, Label, Input, toast } from '@heroui/react';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import type { SkillParam, SkillJson, HttpHeader, SkillInfo, SkillDetailResponse } from './types';

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
  const [formHttpHeaders, setFormHttpHeaders] = useState<HttpHeader[]>([]);
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
    setFormHttpHeaders([]);
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
          const headers: HttpHeader[] = parsed.http.headers
            ? Object.entries(parsed.http.headers).map(([key, value]) => ({ key, value }))
            : [];
          setFormHttpHeaders(headers);
          setFormCommand('');
        } else {
          setFormCommand(parsed.command || '');
          setFormHttpMethod('GET');
          setFormHttpUrl('');
          setFormHttpHeaders([]);
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

  const addHttpHeader = () => {
    setFormHttpHeaders([...formHttpHeaders, { key: '', value: '' }]);
  };

  const updateHttpHeader = (index: number, field: 'key' | 'value', value: string) => {
    const updated = [...formHttpHeaders];
    updated[index][field] = value;
    setFormHttpHeaders(updated);
  };

  const removeHttpHeader = (index: number) => {
    setFormHttpHeaders(formHttpHeaders.filter((_, i) => i !== index));
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
      const headersObj: Record<string, string> = {};
      formHttpHeaders.filter(h => h.key.trim()).forEach(h => {
        headersObj[h.key.trim()] = h.value;
      });

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
                      <div className="flex items-center justify-between mb-2">
                        <Label>{t('skills.httpHeaders')}</Label>
                        <Button size="sm" variant="secondary" onPress={addHttpHeader}>
                          {t('skills.addHeader')}
                        </Button>
                      </div>
                      {formHttpHeaders.length === 0 ? (
                        <p className="text-sm text-gray-400">{t('skills.noHeaders')}</p>
                      ) : (
                        <div className="space-y-2">
                          {formHttpHeaders.map((header, index) => (
                            <div key={index} className="flex gap-2 items-end">
                              <div className="flex-1">
                                <TextField value={header.key} onChange={(v) => updateHttpHeader(index, 'key', v)}>
                                  <Label className="text-xs">{t('skills.headerKey')}</Label>
                                  <Input placeholder="Content-Type" className="font-mono" />
                                </TextField>
                              </div>
                              <div className="flex-1">
                                <TextField value={header.value} onChange={(v) => updateHttpHeader(index, 'value', v)}>
                                  <Label className="text-xs">{t('skills.headerValue')}</Label>
                                  <Input placeholder="application/json" className="font-mono" />
                                </TextField>
                              </div>
                              <Button size="sm" variant="danger" onPress={() => removeHttpHeader(index)}>
                                ×
                              </Button>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>

                    {['POST', 'PUT', 'PATCH'].includes(formHttpMethod) && (
                      <div>
                        <Label>{t('skills.httpBody')}</Label>
                        <textarea
                          className="w-full mt-1 p-2 border rounded-lg font-mono text-sm bg-transparent dark:border-gray-600 min-h-[80px] resize-y"
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
