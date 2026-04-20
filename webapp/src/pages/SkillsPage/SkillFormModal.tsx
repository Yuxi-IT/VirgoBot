import { useState, useEffect } from 'react';
import { Button, Modal, TextField, Label, Input, toast, Checkbox, TextArea } from '@heroui/react';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import type { SkillParam, SkillJson, SubSkillJson, SkillInfo, SkillDetailResponse } from './types';
import { TrashBin, Plus } from '@gravity-ui/icons';

const HTTP_METHODS = ['GET', 'POST', 'PUT', 'DELETE', 'PATCH'];

interface SkillFormModalProps {
  isOpen: boolean;
  onOpenChange: () => void;
  onClose: () => void;
  editingSkill: SkillInfo | null;
  onSaved: () => void;
}

interface SubSkillState {
  name: string;
  description: string;
  mode: 'command' | 'http';
  command: string;
  httpMethod: string;
  httpUrl: string;
  httpHeadersText: string;
  httpBody: string;
  params: SkillParam[];
}

function emptySubSkill(): SubSkillState {
  return {
    name: '',
    description: '',
    mode: 'command',
    command: '',
    httpMethod: 'GET',
    httpUrl: '',
    httpHeadersText: '',
    httpBody: '',
    params: [],
  };
}

function parseHeadersText(text: string): Record<string, string> {
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
}

function headersToText(headers: Record<string, string>): string {
  return Object.entries(headers).map(([k, v]) => `${k}: ${v}`).join('\n');
}

// ---- Sub-skill editor ----

interface SubSkillEditorProps {
  sub: SubSkillState;
  index: number;
  onChange: (index: number, updated: SubSkillState) => void;
  onRemove: (index: number) => void;
}

function SubSkillEditor({ sub, index, onChange, onRemove }: SubSkillEditorProps) {
  const { t } = useI18n();
  const set = (patch: Partial<SubSkillState>) => onChange(index, { ...sub, ...patch });

  const addParam = () => set({ params: [...sub.params, { name: '', type: 'string', description: '', required: false }] });
  const updateParam = (i: number, field: keyof SkillParam, value: string | boolean) => {
    const updated = [...sub.params];
    (updated[i] as any)[field] = value;
    set({ params: updated });
  };
  const removeParam = (i: number) => set({ params: sub.params.filter((_, idx) => idx !== i) });

  return (
    <div className="border border-gray-200 dark:border-gray-700 rounded-lg p-4 space-y-3">
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium text-gray-500">#{index + 1}</span>
        <Button size="sm" isIconOnly variant="danger" onPress={() => onRemove(index)}>
          <TrashBin />
        </Button>
      </div>

      <div className="grid grid-cols-2 gap-3">
        <TextField isRequired value={sub.name} onChange={v => set({ name: v })}>
          <Label className="text-xs">{t('skills.name')}</Label>
          <Input placeholder="word_read" className="font-mono" />
        </TextField>
        <TextField value={sub.description} onChange={v => set({ description: v })}>
          <Label className="text-xs">{t('skills.description')}</Label>
          <Input placeholder={t('skills.description')} />
        </TextField>
      </div>

      <div>
        <Label className="text-xs">{t('skills.mode')}</Label>
        <div className="flex gap-2 mt-1">
          <Button size="sm" variant={sub.mode === 'command' ? 'primary' : 'secondary'} onPress={() => set({ mode: 'command' })}>
            {t('skills.modeCommand')}
          </Button>
          <Button size="sm" variant={sub.mode === 'http' ? 'primary' : 'secondary'} onPress={() => set({ mode: 'http' })}>
            {t('skills.modeHttp')}
          </Button>
        </div>
      </div>

      {sub.mode === 'command' ? (
        <TextField isRequired value={sub.command} onChange={v => set({ command: v })}>
          <Label className="text-xs">{t('skills.command')}</Label>
          <Input placeholder="e.g. python read_word.py {{file_path}}" className="font-mono" />
        </TextField>
      ) : (
        <div className="space-y-3">
          <div>
            <Label className="text-xs">{t('skills.httpMethod')}</Label>
            <div className="flex gap-1 mt-1">
              {HTTP_METHODS.map(m => (
                <Button key={m} size="sm" variant={sub.httpMethod === m ? 'primary' : 'secondary'} onPress={() => set({ httpMethod: m })}>
                  {m}
                </Button>
              ))}
            </div>
          </div>
          <TextField isRequired value={sub.httpUrl} onChange={v => set({ httpUrl: v })}>
            <Label className="text-xs">{t('skills.httpUrl')}</Label>
            <Input placeholder="https://api.example.com/{{param}}" className="font-mono" />
          </TextField>
          <div>
            <Label className="text-xs">{t('skills.httpHeaders')}</Label>
            <TextArea
              className="w-[99%] m-1 p-2 border border-gray-300 dark:border-gray-600 font-mono text-sm bg-transparent min-h-[60px] resize-y"
              value={sub.httpHeadersText}
              onChange={e => set({ httpHeadersText: e.target.value })}
              placeholder={"Content-Type: application/json"}
              rows={2}
            />
          </div>
          {['POST', 'PUT', 'PATCH'].includes(sub.httpMethod) && (
            <div>
              <Label className="text-xs">{t('skills.httpBody')}</Label>
              <TextArea
                className="w-[99%] m-1 p-2 border border-gray-300 dark:border-gray-600 font-mono text-sm bg-transparent min-h-[60px] resize-y"
                value={sub.httpBody}
                onChange={e => set({ httpBody: e.target.value })}
                placeholder='{"key": "{{value}}"}'
                rows={3}
              />
            </div>
          )}
        </div>
      )}

      <div>
        <div className="flex items-center justify-between mb-2">
          <Label className="text-xs">{t('skills.parameters')}</Label>
          <Button size="sm" variant="secondary" onPress={addParam}>{t('skills.addParam')}</Button>
        </div>
        {sub.params.length === 0 ? (
          <p className="text-xs text-gray-400">{t('common.noData')}</p>
        ) : (
          <div className="space-y-2">
            {sub.params.map((param, i) => (
              <div key={i} className="flex gap-2 items-end p-2 bg-gray-50 dark:bg-gray-800/50 rounded">
                <div className="flex-1">
                  <TextField value={param.name} onChange={v => updateParam(i, 'name', v)}>
                    <Label className="text-xs">{t('skills.paramName')}</Label>
                    <Input placeholder="name" className="font-mono" />
                  </TextField>
                </div>
                <div className="flex-1">
                  <TextField value={param.description} onChange={v => updateParam(i, 'description', v)}>
                    <Label className="text-xs">{t('skills.paramDesc')}</Label>
                    <Input placeholder={t('skills.paramDesc')} />
                  </TextField>
                </div>
                <div className="flex items-center gap-2 pb-1">
                  <Checkbox isSelected={param.required} onChange={checked => updateParam(i, 'required', checked)}>
                    <Checkbox.Control><Checkbox.Indicator /></Checkbox.Control>
                    <Checkbox.Content><Label htmlFor="">{t('skills.paramRequired')}</Label></Checkbox.Content>
                  </Checkbox>
                  <Button size="sm" isIconOnly variant="danger" onPress={() => removeParam(i)}>
                    <TrashBin />
                  </Button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

// ---- Main modal ----

function SkillFormModal({ isOpen, onOpenChange, onClose, editingSkill, onSaved }: SkillFormModalProps) {
  const { t } = useI18n();

  const [formName, setFormName] = useState('');
  const [formDescription, setFormDescription] = useState('');
  const [formSkillMode, setFormSkillMode] = useState<'single' | 'multi'>('single');

  // single skill state
  const [formCommand, setFormCommand] = useState('');
  const [formParams, setFormParams] = useState<SkillParam[]>([]);
  const [formMode, setFormMode] = useState<'command' | 'http'>('command');
  const [formHttpMethod, setFormHttpMethod] = useState('GET');
  const [formHttpUrl, setFormHttpUrl] = useState('');
  const [formHttpHeadersText, setFormHttpHeadersText] = useState('');
  const [formHttpBody, setFormHttpBody] = useState('');

  // multi skill state
  const [subSkills, setSubSkills] = useState<SubSkillState[]>([emptySubSkill()]);

  const [formError, setFormError] = useState('');

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
    setFormSkillMode('single');
    setFormCommand('');
    setFormParams([]);
    setFormError('');
    setFormMode('command');
    setFormHttpMethod('GET');
    setFormHttpUrl('');
    setFormHttpHeadersText('');
    setFormHttpBody('');
    setSubSkills([emptySubSkill()]);
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
        setFormError('');

        if (parsed.subSkills && parsed.subSkills.length > 0) {
          setFormSkillMode('multi');
          setSubSkills(parsed.subSkills.map(s => ({
            name: s.name,
            description: s.description || '',
            mode: s.mode === 'http' ? 'http' : 'command',
            command: s.command || '',
            httpMethod: s.http?.method || 'GET',
            httpUrl: s.http?.url || '',
            httpHeadersText: s.http?.headers ? headersToText(s.http.headers) : '',
            httpBody: s.http?.body || '',
            params: s.parameters || [],
          })));
        } else {
          setFormSkillMode('single');
          setFormParams(parsed.parameters || []);
          const mode = parsed.mode === 'http' ? 'http' : 'command';
          setFormMode(mode);
          if (mode === 'http' && parsed.http) {
            setFormHttpMethod(parsed.http.method || 'GET');
            setFormHttpUrl(parsed.http.url || '');
            setFormHttpBody(parsed.http.body || '');
            setFormHttpHeadersText(parsed.http.headers ? headersToText(parsed.http.headers) : '');
            setFormCommand('');
          } else {
            setFormCommand(parsed.command || '');
            setFormHttpMethod('GET');
            setFormHttpUrl('');
            setFormHttpHeadersText('');
            setFormHttpBody('');
          }
        }
      }
    } catch {
      toast.danger(t('common.error'));
    }
  };

  const addParam = () => setFormParams([...formParams, { name: '', type: 'string', description: '', required: false }]);
  const updateParam = (index: number, field: keyof SkillParam, value: string | boolean) => {
    const updated = [...formParams];
    (updated[index] as any)[field] = value;
    setFormParams(updated);
  };
  const removeParam = (index: number) => setFormParams(formParams.filter((_, i) => i !== index));

  const handleSave = async () => {
    setFormError('');

    if (!formName.trim()) {
      setFormError(t('skills.nameRequired'));
      return;
    }

    let skillJson: SkillJson;

    if (formSkillMode === 'multi') {
      if (subSkills.length === 0) {
        setFormError('至少需要一个子功能');
        return;
      }
      const builtSubSkills: SubSkillJson[] = subSkills.map(s => {
        if (s.mode === 'http') {
          return {
            name: s.name.trim(),
            description: s.description.trim(),
            parameters: s.params.filter(p => p.name.trim()),
            mode: 'http',
            http: {
              method: s.httpMethod,
              url: s.httpUrl.trim(),
              headers: parseHeadersText(s.httpHeadersText),
              body: s.httpBody,
            },
          };
        }
        return {
          name: s.name.trim(),
          description: s.description.trim(),
          parameters: s.params.filter(p => p.name.trim()),
          command: s.command.trim(),
        };
      });
      skillJson = {
        name: formName.trim(),
        description: formDescription.trim(),
        parameters: [],
        subSkills: builtSubSkills,
      };
    } else {
      if (formMode === 'command' && !formCommand.trim()) {
        setFormError(t('skills.commandRequired'));
        return;
      }
      if (formMode === 'http' && !formHttpUrl.trim()) {
        setFormError(t('skills.urlRequired'));
        return;
      }
      if (formMode === 'http') {
        skillJson = {
          name: formName.trim(),
          description: formDescription.trim(),
          parameters: formParams.filter(p => p.name.trim()),
          mode: 'http',
          http: {
            method: formHttpMethod,
            url: formHttpUrl.trim(),
            headers: parseHeadersText(formHttpHeadersText),
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
    (formSkillMode === 'single' && formMode === 'command' && !formCommand.trim()) ||
    (formSkillMode === 'single' && formMode === 'http' && !formHttpUrl.trim());

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
                  <Input placeholder="e.g. office" className="font-mono" />
                </TextField>

                <TextField value={formDescription} onChange={setFormDescription}>
                  <Label>{t('skills.description')}</Label>
                  <Input placeholder={t('skills.description')} />
                </TextField>

                {/* Skill type toggle */}
                <div>
                  <Label>类型</Label>
                  <div className="flex gap-2 mt-1">
                    <Button
                      size="sm"
                      variant={formSkillMode === 'single' ? 'primary' : 'secondary'}
                      onPress={() => setFormSkillMode('single')}
                    >
                      单功能
                    </Button>
                    <Button
                      size="sm"
                      variant={formSkillMode === 'multi' ? 'primary' : 'secondary'}
                      onPress={() => setFormSkillMode('multi')}
                    >
                      多子功能
                    </Button>
                  </div>
                </div>

                {formSkillMode === 'single' && (
                  <>
                    {/* Mode Toggle */}
                    <div>
                      <Label>{t('skills.mode')}</Label>
                      <div className="flex gap-2 mt-1">
                        <Button size="sm" variant={formMode === 'command' ? 'primary' : 'secondary'} onPress={() => setFormMode('command')}>
                          {t('skills.modeCommand')}
                        </Button>
                        <Button size="sm" variant={formMode === 'http' ? 'primary' : 'secondary'} onPress={() => setFormMode('http')}>
                          {t('skills.modeHttp')}
                        </Button>
                      </div>
                    </div>

                    {formMode === 'command' && (
                      <TextField isRequired value={formCommand} onChange={setFormCommand}>
                        <Label>{t('skills.command')}</Label>
                        <Input placeholder="e.g. ffmpeg -i {{input}} {{output}}" className="font-mono" />
                      </TextField>
                    )}

                    {formMode === 'http' && (
                      <div className="space-y-4">
                        <div>
                          <Label>{t('skills.httpMethod')}</Label>
                          <div className="flex gap-1 mt-1">
                            {HTTP_METHODS.map(m => (
                              <Button key={m} size="sm" variant={formHttpMethod === m ? 'primary' : 'secondary'} onPress={() => setFormHttpMethod(m)}>
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
                            onChange={e => setFormHttpHeadersText(e.target.value)}
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
                              onChange={e => setFormHttpBody(e.target.value)}
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
                        <Button size="sm" variant="secondary" onPress={addParam}>{t('skills.addParam')}</Button>
                      </div>
                      {formParams.length === 0 ? (
                        <p className="text-sm text-gray-400">{t('common.noData')}</p>
                      ) : (
                        <div className="space-y-3">
                          {formParams.map((param, index) => (
                            <div key={index} className="flex gap-2 items-end p-3 bg-gray-50 dark:bg-gray-800/50 rounded-lg">
                              <div className="flex-1 w-24">
                                <TextField value={param.name} onChange={v => updateParam(index, 'name', v)}>
                                  <Label className="text-xs">{t('skills.paramName')}</Label>
                                  <Input placeholder="name" className="font-mono" />
                                </TextField>
                              </div>
                              <div className="flex-1">
                                <TextField value={param.description} onChange={v => updateParam(index, 'description', v)}>
                                  <Label className="text-xs">{t('skills.paramDesc')}</Label>
                                  <Input placeholder={t('skills.paramDesc')} />
                                </TextField>
                              </div>
                              <div className="flex items-center gap-2 pb-1">
                                <Checkbox isSelected={param.required} onChange={checked => updateParam(index, 'required', checked)}>
                                  <Checkbox.Control><Checkbox.Indicator /></Checkbox.Control>
                                  <Checkbox.Content><Label htmlFor="">{t('skills.paramRequired')}</Label></Checkbox.Content>
                                </Checkbox>
                                <Button size="sm" isIconOnly variant="danger" onPress={() => removeParam(index)}>
                                  <TrashBin />
                                </Button>
                              </div>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  </>
                )}

                {formSkillMode === 'multi' && (
                  <div className="space-y-3">
                    <div className="flex items-center justify-between">
                      <Label>子功能列表</Label>
                      <Button size="sm" variant="secondary" onPress={() => setSubSkills([...subSkills, emptySubSkill()])}>
                        <Plus /> 添加子功能
                      </Button>
                    </div>
                    {subSkills.map((sub, i) => (
                      <SubSkillEditor
                        key={i}
                        sub={sub}
                        index={i}
                        onChange={(idx, updated) => {
                          const next = [...subSkills];
                          next[idx] = updated;
                          setSubSkills(next);
                        }}
                        onRemove={idx => setSubSkills(subSkills.filter((_, si) => si !== idx))}
                      />
                    ))}
                  </div>
                )}
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
