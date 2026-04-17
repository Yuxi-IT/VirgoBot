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
  const [formTaskType, setFormTaskType] = useState<'http' | 'shell'>('http');
  const [formScheduleType, setFormScheduleType] = useState<'interval' | 'daily'>('interval');
  const [formIntervalMinutes, setFormIntervalMinutes] = useState(60);
  const [formDailyTime, setFormDailyTime] = useState('09:00');
  const [formTaskRequirement, setFormTaskRequirement] = useState('');
  const [formHttpMethod, setFormHttpMethod] = useState('GET');
  const [formHttpUrl, setFormHttpUrl] = useState('');
  const [formHttpHeadersText, setFormHttpHeadersText] = useState('');
  const [formHttpBody, setFormHttpBody] = useState('');
  const [formShellCommand, setFormShellCommand] = useState('');

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
    setFormTaskRequirement('');
    setFormHttpMethod('GET');
    setFormHttpUrl('');
    setFormHttpHeadersText('');
    setFormHttpBody('');
    setFormShellCommand('');
  };

  const loadTaskData = (task: ScheduledTask) => {
    setFormName(task.name);
    setFormDescription(task.description);
    setFormEnabled(task.enabled);
    setFormTaskType(task.taskType);
    setFormScheduleType(task.scheduleType === 'cron' ? 'interval' : task.scheduleType);
    setFormIntervalMinutes(task.intervalMinutes);
    setFormDailyTime(task.dailyTime);
    setFormTaskRequirement(task.taskRequirement);
    setFormHttpMethod(task.httpMethod);
    setFormHttpUrl(task.httpUrl);
    setFormHttpBody(task.httpBody);
    setFormShellCommand(task.shellCommand);

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
    };

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
