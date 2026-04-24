import { useState } from 'react';
import { Card, Button, Chip, Spinner, toast } from '@heroui/react';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import type { ProviderInfo, ModelsResponse } from './types';

interface Props {
  provider: ProviderInfo;
  isCurrent: boolean;
  onSwitch: () => void;
  onEdit: () => void;
  onDelete: () => void;
  onModelsUpdated: (models: string[]) => void;
  switching: boolean;
}

export default function ProviderCard({ provider, isCurrent, onSwitch, onEdit, onDelete, onModelsUpdated, switching }: Props) {
  const { t } = useI18n();
  const [fetchingModels, setFetchingModels] = useState(false);
  const [models, setModels] = useState<string[]>(provider.models ?? []);

  const fetchModels = async () => {
    setFetchingModels(true);
    try {
      const res = await api.get<ModelsResponse>(`/api/providers/${encodeURIComponent(provider.name)}/models`);
      if (res.success) {
        setModels(res.data);
        await api.put(`/api/providers/${encodeURIComponent(provider.name)}`, { models: res.data });
        onModelsUpdated(res.data);
        toast.success(t('providers.fetchModelsSuccess').replace('{n}', String(res.data.length)));
      }
    } catch {
      toast.danger(t('providers.fetchModelsFailed'));
    } finally {
      setFetchingModels(false);
    }
  };

  const selectModel = async (model: string) => {
    try {
      await api.put(`/api/providers/${encodeURIComponent(provider.name)}`, { currentModel: model });
      onModelsUpdated(models);
      toast.success(t('providers.modelSwitchSuccess').replace('{name}', model));
    } catch {
      toast.danger(t('providers.modelSwitchFailed'));
    }
  };

  return (
    <Card className={`${isCurrent ? 'border-2 border-sky-500' : ''}`}>
      <Card.Content>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="text-lg font-semibold">{provider.name}</span>
            <Chip size="sm">{provider.protocol}</Chip>
            {isCurrent && <Chip size="sm" color="accent">{t('providers.current')}</Chip>}
          </div>
          <div className="flex gap-2">
            {!isCurrent && (
              <Button size="sm" onPress={onSwitch} isDisabled={switching}>
                {switching ? <Spinner size="sm" className="mr-1" /> : null}
                {t('providers.switch')}
              </Button>
            )}
            <Button size="sm" variant="secondary" onPress={onEdit}>{t('common.edit')}</Button>
            {!isCurrent && (
              <Button size="sm" variant="danger" onPress={onDelete}>{t('common.delete')}</Button>
            )}
          </div>
        </div>
        <div className="mt-2 text-sm text-default-500 space-y-1">
          <div>Base URL: {provider.baseUrl}</div>
          <div>API Key: {provider.apiKey}</div>
          <div>{t('providers.model')}: {provider.currentModel || t('providers.notSet')}</div>
        </div>
        <div className="mt-3 flex items-center gap-2">
          <Button size="sm" variant="secondary" onPress={fetchModels} isDisabled={fetchingModels}>
            {fetchingModels ? <Spinner size="sm" className="mr-1" /> : null}
            {t('providers.fetchModels')}
          </Button>
          {models.length > 0 && (
            <span className="text-xs text-default-400">{t('providers.modelCount').replace('{n}', String(models.length))}</span>
          )}
        </div>
        {models.length > 0 && (
          <div className="mt-2 flex flex-wrap gap-1">
            {models.slice(0, 20).map(m => (
              <Chip
                key={m}
                size="sm"
                color={m === provider.currentModel ? 'accent' : undefined}
                className="cursor-pointer"
                onClick={() => selectModel(m)}
              >
                {m}
              </Chip>
            ))}
            {models.length > 20 && <Chip size="sm">+{models.length - 20}</Chip>}
          </div>
        )}
      </Card.Content>
    </Card>
  );
}
