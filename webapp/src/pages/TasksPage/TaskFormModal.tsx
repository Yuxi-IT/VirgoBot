import { useState, useEffect } from 'react';
import { Button, Modal, TextField, Label, Input, toast, TextArea, Switch } from '@heroui/react';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import type { ScheduledTask } from './types';

interface TaskFormModalProps {
  isOpen: boolean;
  onOpenChange: () => void;
  onClose: () => void;
  editingTask: ScheduledTask | null;
  onSaved: () => void;
}

const HTTP_METHODS = ['GET', 'POST', 'PUT', 'DELETE', 'PATCH'];

function TaskFormModal({ isOpen, onOpenChange, onClose, editingTask, onSaved }: TaskFormModalProps) {
  const { t } = useI18n();

  const [formName, setFormName] = useState('');
  const [formDescription, setFormDescription] = useState('');
  const [formEnabled, setFormEnabled] = useState(true);
  const [formTaskType, setFormTaskType] = useState<'http' | 'shell' | 'text'>('http');
  const [formScheduleType, setFormScheduleType] = useState<'interval' | 'daily' | 'once'>('interval');
  const [formIntervalMinutes, setFormIntervalMinutes] = useState(60);
  const [formDailyTime, setFormDailyTime] = useState('09:00');
  const [formOnceMode, setFormOnceMode] = useState<'delay' | 'at'>('delay');
  const [formOnceDelayMinutes, setFormOnceDelayMinutes] = useState(30);
  const [formOnceAt, setFormOnceAt] = useState('');
  const [formTaskRequirement, setFormTaskRequirement] = useState('');
  const [formHttpMethod, setFormHttpMethod] = useState('GET');
  const [formHttpUrl, setFormHttpUrl] = useState('');
  const [formHttpHeadersText, setFormHttpHeadersText] = useState('');
  const [formHttpBody, setFormHttpBody] = useState('');
  const [formShellCommand, setFormShellCommand] = useState('');
  const [formTextInstruction, setFormTextInstruction] = useState('');

  useEffect(() => {
    if (isOpen && editingTask) {
      loadTaskData(editingTask);
    } else if (isOpen && !editingTask) {
      resetForm();
    }
  }, [isOpen, editingTask]);

  const resetForm = () => {
    setFormName('');
    setFormDescription('');
    setFormEnabled(true);
    setFormTaskType('http');
    setFormScheduleType('interval');
    setFormIntervalMinutes(60);
    setFormDailyTime('09:00');
    setFormOnceMode('delay');
    setFormOnceDelayMinutes(30);
    setFormOnceAt('');
    setFormTaskRequirement('');
    setFormHttpMethod('GET');
    setFormHttpUrl('');
    setFormHttpHeadersText('');
    setFormHttpBody('');
    setFormShellCommand('');
    setFormTextInstruction('');
  };

  const loadTaskData = (task: ScheduledTask) => {
    setFormName(task.name);
    setFormDescription(task.description);
    setFormEnabled(task.enabled);
    setFormTaskType(task.taskType);
    setFormScheduleType(task.scheduleType);
    setFormIntervalMinutes(task.intervalMinutes);
    setFormDailyTime(task.dailyTime);
    if (task.scheduleType === 'once') {
      if (task.onceAt) {
        setFormOnceMode('at');
        // 转为 datetime-local 格式 (YYYY-MM-DDTHH:mm)
        setFormOnceAt(new Date(task.onceAt).toISOString().slice(0, 16));
      } else {
        setFormOnceMode('delay');
        setFormOnceDelayMinutes(task.onceDelayMinutes ?? 30);
      }
    }
    setFormTaskRequirement(task.taskRequirement);
    setFormHttpMethod(task.httpMethod);
    setFormHttpUrl(task.httpUrl);
    setFormHttpBody(task.httpBody);
    setFormShellCommand(task.shellCommand);
    setFormTextInstruction(task.textInstruction || '');

    const headersText = task.httpHeaders
      ? Object.entries(task.httpHeaders).map(([key, value]) => `${key}: ${value}`).join('\n')
      : '';
    setFormHttpHeadersText(headersText);
  };

  const handleSave = async () => {
    if (!formName.trim()) {
      toast.danger(t('tasks.nameRequired'));
      return;
    }

    if (!formTaskRequirement.trim()) {
      toast.danger(t('tasks.requirementRequired'));
      return;
    }

    if (formTaskType === 'http' && !formHttpUrl.trim()) {
      toast.danger(t('tasks.urlRequired'));
      return;
    }

    if (formTaskType === 'shell' && !formShellCommand.trim()) {
      toast.danger(t('tasks.commandRequired'));
      return;
    }

    if (formTaskType === 'text' && !formTextInstruction.trim()) {
      toast.danger(t('tasks.textInstructionRequired'));
      return;
    }

    const headers: Record<string, string> = {};
    if (formHttpHeadersText.trim()) {
      formHttpHeadersText.split('\n').forEach(line => {
        const [key, ...valueParts] = line.split(':');
        if (key && valueParts.length > 0) {
          headers[key.trim()] = valueParts.join(':').trim();
        }
      });
    }

    const taskData: Partial<ScheduledTask> = {
      name: formName,
      description: formDescription,
      enabled: formEnabled,
      taskType: formTaskType,
      scheduleType: formScheduleType,
      intervalMinutes: formIntervalMinutes,
      dailyTime: formDailyTime,
      cronExpression: '',
      taskRequirement: formTaskRequirement,
      httpMethod: formHttpMethod,
      httpUrl: formHttpUrl,
      httpHeaders: headers,
      httpBody: formHttpBody,
      shellCommand: formShellCommand,
      textInstruction: formTextInstruction,
    };

    if (formScheduleType === 'once') {
      if (formOnceMode === 'delay') {
        taskData.onceDelayMinutes = formOnceDelayMinutes;
      } else {
        taskData.onceAt = new Date(formOnceAt).toISOString();
      }
    }

    try {
      if (editingTask) {
        await api.put(`/api/tasks/${editingTask.id}`, taskData);
        toast.success(t('tasks.updateSuccess'));
      } else {
        await api.post('/api/tasks', taskData);
        toast.success(t('tasks.createSuccess'));
      }
      onClose();
      await onSaved();
    } catch {
      toast.danger(t('common.error'));
    }
  };

  return (
    <Modal>
      <Modal.Backdrop isOpen={isOpen} onOpenChange={onOpenChange}>
        <Modal.Container size="lg">
          <Modal.Dialog>
            <Modal.Header>
              <Modal.Heading>
                {editingTask ? t('tasks.editTask') : t('tasks.addTask')}
              </Modal.Heading>
            </Modal.Header>
            <Modal.Body>
              <div className="space-y-4 max-h-[60vh] overflow-y-auto">
                <TextField value={formName} onChange={setFormName}>
                  <Label>{t('tasks.name')}</Label>
                  <Input placeholder={t('tasks.namePlaceholder')} />
                </TextField>

                <TextField value={formDescription} onChange={setFormDescription}>
                  <Label>{t('tasks.description')}</Label>
                  <TextArea
                    value={formDescription}
                    onChange={(e) => setFormDescription(e.target.value)}
                    placeholder={t('tasks.descriptionPlaceholder')}
                    rows={2}
                  />
                </TextField>

                <TextField value={formTaskRequirement} onChange={setFormTaskRequirement}>
                  <Label>{t('tasks.taskRequirement')}</Label>
                  <TextArea
                    value={formTaskRequirement}
                    onChange={(e) => setFormTaskRequirement(e.target.value)}
                    placeholder={t('tasks.taskRequirementHint')}
                    rows={3}
                  />
                </TextField>

                <div>
                  <Label>{t('tasks.enabled')}</Label>
                  <div className="mt-2">
                    <Switch isSelected={formEnabled} onChange={setFormEnabled} />
                  </div>
                </div>

                <div>
                  <Label>{t('tasks.taskType')}</Label>
                  <div className="flex gap-2 mt-2">
                    <Button
                      size="sm"
                      variant={formTaskType === 'http' ? 'primary' : 'secondary'}
                      onPress={() => setFormTaskType('http')}
                    >
                      {t('tasks.http')}
                    </Button>
                    <Button
                      size="sm"
                      variant={formTaskType === 'shell' ? 'primary' : 'secondary'}
                      onPress={() => setFormTaskType('shell')}
                    >
                      {t('tasks.shell')}
                    </Button>
                    <Button
                      size="sm"
                      variant={formTaskType === 'text' ? 'primary' : 'secondary'}
                      onPress={() => setFormTaskType('text')}
                    >
                      {t('tasks.text')}
                    </Button>
                  </div>
                </div>

                <div>
                  <Label>{t('tasks.scheduleType')}</Label>
                  <div className="flex gap-2 mt-2">
                    <Button
                      size="sm"
                      variant={formScheduleType === 'interval' ? 'primary' : 'secondary'}
                      onPress={() => setFormScheduleType('interval')}
                    >
                      {t('tasks.interval')}
                    </Button>
                    <Button
                      size="sm"
                      variant={formScheduleType === 'daily' ? 'primary' : 'secondary'}
                      onPress={() => setFormScheduleType('daily')}
                    >
                      {t('tasks.daily')}
                    </Button>
                    <Button
                      size="sm"
                      variant={formScheduleType === 'once' ? 'primary' : 'secondary'}
                      onPress={() => setFormScheduleType('once')}
                    >
                      一次性
                    </Button>
                  </div>
                </div>

                {formScheduleType === 'interval' && (
                  <TextField value={String(formIntervalMinutes)} onChange={(v) => setFormIntervalMinutes(Number(v) || 60)}>
                    <Label>{t('tasks.intervalMinutes')}</Label>
                    <Input type="number" min="1" />
                  </TextField>
                )}

                {formScheduleType === 'daily' && (
                  <TextField value={formDailyTime} onChange={setFormDailyTime}>
                    <Label>{t('tasks.dailyTime')}</Label>
                    <Input type="time" />
                  </TextField>
                )}

                {formScheduleType === 'once' && (
                  <div className="space-y-3">
                    <div>
                      <Label>执行时机</Label>
                      <div className="flex gap-2 mt-2">
                        <Button size="sm" variant={formOnceMode === 'delay' ? 'primary' : 'secondary'} onPress={() => setFormOnceMode('delay')}>
                          延迟执行
                        </Button>
                        <Button size="sm" variant={formOnceMode === 'at' ? 'primary' : 'secondary'} onPress={() => setFormOnceMode('at')}>
                          指定时间
                        </Button>
                      </div>
                    </div>
                    {formOnceMode === 'delay' ? (
                      <TextField value={String(formOnceDelayMinutes)} onChange={v => setFormOnceDelayMinutes(Number(v) || 1)}>
                        <Label>延迟分钟数</Label>
                        <Input type="number" min="1" placeholder="30" />
                      </TextField>
                    ) : (
                      <TextField value={formOnceAt} onChange={setFormOnceAt}>
                        <Label>执行时间</Label>
                        <Input type="datetime-local" />
                      </TextField>
                    )}
                    <p className="text-xs text-gray-400">一次性任务执行完成后会自动关闭</p>
                  </div>
                )}

                {formTaskType === 'http' && (
                  <div className="space-y-4">
                    <div>
                      <Label>{t('tasks.httpMethod')}</Label>
                      <div className="flex gap-1 mt-2">
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

                    <TextField value={formHttpUrl} onChange={setFormHttpUrl}>
                      <Label>{t('tasks.httpUrl')}</Label>
                      <Input placeholder={t('tasks.urlPlaceholder')} />
                    </TextField>

                    <TextField value={formHttpHeadersText} onChange={setFormHttpHeadersText}>
                      <Label>{t('tasks.httpHeaders')}</Label>
                      <TextArea
                        value={formHttpHeadersText}
                        onChange={(e) => setFormHttpHeadersText(e.target.value)}
                        placeholder={t('tasks.httpHeadersHint')}
                        rows={3}
                      />
                    </TextField>

                    <TextField value={formHttpBody} onChange={setFormHttpBody}>
                      <Label>{t('tasks.httpBody')}</Label>
                      <TextArea
                        value={formHttpBody}
                        onChange={(e) => setFormHttpBody(e.target.value)}
                        rows={4}
                      />
                    </TextField>
                  </div>
                )}

                {formTaskType === 'shell' && (
                  <TextField value={formShellCommand} onChange={setFormShellCommand}>
                    <Label>{t('tasks.shellCommand')}</Label>
                    <TextArea
                      value={formShellCommand}
                      onChange={(e) => setFormShellCommand(e.target.value)}
                      placeholder={t('tasks.commandPlaceholder')}
                      rows={4}
                      className="font-mono"
                    />
                  </TextField>
                )}

                {formTaskType === 'text' && (
                  <TextField value={formTextInstruction} onChange={setFormTextInstruction}>
                    <Label>{t('tasks.textInstruction')}</Label>
                    <TextArea
                      value={formTextInstruction}
                      onChange={(e) => setFormTextInstruction(e.target.value)}
                      placeholder={t('tasks.textInstructionPlaceholder')}
                      rows={4}
                    />
                  </TextField>
                )}
              </div>
            </Modal.Body>
            <Modal.Footer>
              <Button variant="secondary" onPress={onClose}>
                {t('common.cancel')}
              </Button>
              <Button onPress={handleSave}>
                {t('common.save')}
              </Button>
            </Modal.Footer>
          </Modal.Dialog>
        </Modal.Container>
      </Modal.Backdrop>
    </Modal>
  );
}

export default TaskFormModal;
