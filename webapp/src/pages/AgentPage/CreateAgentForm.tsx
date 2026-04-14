import { useState } from 'react';
import { Card, Button, Spinner, TextField, Label, Input, TextArea, toast } from '@heroui/react';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';

interface CreateAgentFormProps {
  visible: boolean;
  onCreated: () => void;
  onCancel: () => void;
}

function CreateAgentForm({ visible, onCreated, onCancel }: CreateAgentFormProps) {
  const { t } = useI18n();
  const [name, setName] = useState('');
  const [content, setContent] = useState('');
  const [creating, setCreating] = useState(false);

  if (!visible) return null;

  const handleCreate = async () => {
    if (!name.trim() || !content.trim()) return;
    try {
      setCreating(true);
      await api.post('/api/agents', { name: name.trim(), content });
      toast.success(t('agent.createSuccess'));
      setName('');
      setContent('');
      onCreated();
    } catch {
      toast.danger(t('settings.saveFailed'));
    } finally {
      setCreating(false);
    }
  };

  return (
    <Card className="mb-6">
      <Card.Header>
        <Card.Title>{t('agent.createAgent')}</Card.Title>
      </Card.Header>
      <Card.Content>
        <div className="space-y-4">
          <TextField value={name} onChange={setName}>
            <Label>{t('agent.agentName')}</Label>
            <Input placeholder={t('agent.namePlaceholder')} />
          </TextField>
          <div>
            <label className="text-sm font-medium mb-1 block">{t('agent.agentContent')}</label>
            <TextArea
              value={content}
              onChange={(e) => setContent(e.target.value)}
              rows={12}
              placeholder={t('agent.contentPlaceholder')}
              className="font-mono w-full"
            />
          </div>
          <div className="flex gap-2">
            <Button
              onPress={handleCreate}
              isDisabled={creating || !name.trim() || !content.trim()}
            >
              {creating ? <Spinner size="sm" className="mr-2" /> : null}
              {t('agent.createAgent')}
            </Button>
            <Button variant="secondary" onPress={() => { setName(''); setContent(''); onCancel(); }}>
              {t('common.cancel')}
            </Button>
          </div>
        </div>
      </Card.Content>
    </Card>
  );
}

export default CreateAgentForm;
