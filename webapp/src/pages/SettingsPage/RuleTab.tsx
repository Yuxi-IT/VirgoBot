import { useEffect, useState } from 'react';
import { Card, Button, Spinner, TextArea, toast } from '@heroui/react';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import type { ContentResponse } from './types';

interface RuleTabProps {
  active: boolean;
}

function RuleTab({ active }: RuleTabProps) {
  const { t } = useI18n();
  const [content, setContent] = useState('');
  const [loading, setLoading] = useState(false);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    if (active && !loaded) {
      loadContent();
      setLoaded(true);
    }
  }, [active]);

  const loadContent = async () => {
    try {
      setLoading(true);
      const res = await api.get<ContentResponse>('/api/config/rule');
      if (res.success) {
        setContent(res.data.content);
      }
    } catch {
      // silently fail
    } finally {
      setLoading(false);
    }
  };

  const saveContent = async () => {
    try {
      await api.put('/api/config/rule', { content });
      toast.success(t('settings.saveSuccess'));
    } catch {
      toast.danger(t('settings.saveFailed'));
    }
  };

  return (
    <Card className="mt-4">
      <Card.Header>
        <Card.Title>{t('settings.rule')}</Card.Title>
      </Card.Header>
      <Card.Content>
        {loading ? (
          <div className="flex justify-center py-8">
            <Spinner size="lg" />
          </div>
        ) : (
          <div className="space-y-4">
            <TextArea
              value={content}
              onChange={(e) => setContent(e.target.value)}
              rows={15}
              className="font-mono w-full"
            />
            <Button onPress={saveContent}>
              {t('common.save')}
            </Button>
          </div>
        )}
      </Card.Content>
    </Card>
  );
}

export default RuleTab;
