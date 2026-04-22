import { useState } from 'react';
import { Card, Button, Chip, Spinner, toast } from '@heroui/react';
import { api } from '../../services/api';
import type { ProviderInfo, ModelsResponse } from './types';

interface Props {
  provider: ProviderInfo;
  isCurrent: boolean;
  onSwitch: () => void;
  onEdit: () => void;
  onDelete: () => void;
  switching: boolean;
}

export default function ProviderCard({ provider, isCurrent, onSwitch, onEdit, onDelete, switching }: Props) {
  const [fetchingModels, setFetchingModels] = useState(false);
  const [models, setModels] = useState<string[]>(provider.models ?? []);

  const fetchModels = async () => {
    setFetchingModels(true);
    try {
      const res = await api.get<ModelsResponse>(`/api/providers/${encodeURIComponent(provider.name)}/models`);
      if (res.success) {
        setModels(res.data);
        toast.success(`获取到 ${res.data.length} 个模型`);
      }
    } catch {
      toast.danger('获取模型列表失败');
    } finally {
      setFetchingModels(false);
    }
  };

  return (
    <Card className={isCurrent ? 'border-2 border-primary' : ''}>
      <Card.Content>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="text-lg font-semibold">{provider.name}</span>
            <Chip size="sm">{provider.protocol}</Chip>
            {isCurrent && <Chip size="sm" color="accent">当前</Chip>}
          </div>
          <div className="flex gap-2">
            {!isCurrent && (
              <Button size="sm" onPress={onSwitch} isDisabled={switching}>
                {switching ? <Spinner size="sm" className="mr-1" /> : null}
                切换
              </Button>
            )}
            <Button size="sm" variant="secondary" onPress={onEdit}>编辑</Button>
            {!isCurrent && (
              <Button size="sm" variant="danger" onPress={onDelete}>删除</Button>
            )}
          </div>
        </div>
        <div className="mt-2 text-sm text-default-500 space-y-1">
          <div>Base URL: {provider.baseUrl}</div>
          <div>API Key: {provider.apiKey}</div>
          <div>模型: {provider.currentModel || '未设置'}</div>
        </div>
        <div className="mt-3 flex items-center gap-2">
          <Button size="sm" variant="secondary" onPress={fetchModels} isDisabled={fetchingModels}>
            {fetchingModels ? <Spinner size="sm" className="mr-1" /> : null}
            获取模型列表
          </Button>
          {models.length > 0 && (
            <span className="text-xs text-default-400">{models.length} 个模型</span>
          )}
        </div>
        {models.length > 0 && (
          <div className="mt-2 flex flex-wrap gap-1">
            {models.slice(0, 20).map(m => (
              <Chip key={m} size="sm">{m}</Chip>
            ))}
            {models.length > 20 && <Chip size="sm">+{models.length - 20}</Chip>}
          </div>
        )}
      </Card.Content>
    </Card>
  );
}
