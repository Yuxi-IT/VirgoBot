import { useEffect, useState } from 'react';
import { Button, Spinner, toast } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import ProviderCard from './ProviderCard';
import ProviderFormModal from './ProviderFormModal';
import type { ProviderInfo, ProvidersResponse } from './types';

function ProvidersPage() {
  const { t } = useI18n();
  const [providers, setProviders] = useState<ProviderInfo[]>([]);
  const [currentProvider, setCurrentProvider] = useState('');
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editingProvider, setEditingProvider] = useState<ProviderInfo | null>(null);
  const [saving, setSaving] = useState(false);
  const [switching, setSwitching] = useState(false);

  useEffect(() => { loadProviders(); }, []);

  const loadProviders = async () => {
    try {
      setLoading(true);
      const res = await api.get<ProvidersResponse>('/api/providers');
      if (res.success) {
        setProviders(res.data.providers);
        setCurrentProvider(res.data.currentProvider);
      }
    } catch { /* silent */ } finally { setLoading(false); }
  };

  const handleCreate = async (data: { name: string; apiKey: string; baseUrl: string; currentModel: string; protocol: string }) => {
    setSaving(true);
    try {
      await api.post('/api/providers', data);
      toast.success(t('providers.createSuccess'));
      setShowForm(false);
      await loadProviders();
    } catch { toast.danger(t('providers.createFailed')); } finally { setSaving(false); }
  };

  const handleUpdate = async (data: { name: string; apiKey: string; baseUrl: string; currentModel: string; protocol: string }) => {
    if (!editingProvider) return;
    setSaving(true);
    try {
      const payload: Record<string, unknown> = { baseUrl: data.baseUrl, currentModel: data.currentModel, protocol: data.protocol };
      if (data.apiKey) payload.apiKey = data.apiKey;
      await api.put(`/api/providers/${encodeURIComponent(editingProvider.name)}`, payload);
      toast.success(t('providers.updateSuccess'));
      setEditingProvider(null);
      await loadProviders();
    } catch { toast.danger(t('providers.updateFailed')); } finally { setSaving(false); }
  };

  const handleDelete = async (name: string) => {
    try {
      await api.del(`/api/providers/${encodeURIComponent(name)}`);
      toast.success(t('providers.deleteSuccess'));
      await loadProviders();
    } catch { toast.danger(t('providers.deleteFailed')); }
  };

  const handleSwitch = async (name: string) => {
    setSwitching(true);
    try {
      await api.put('/api/providers/current', { name });
      await api.post('/api/gateway/restart', {});
      toast.success(t('providers.switchSuccess').replace('{name}', name));
      setCurrentProvider(name);
      await loadProviders();
    } catch { toast.danger(t('providers.switchFailed')); } finally { setSwitching(false); }
  };

  if (loading) {
    return (
      <DefaultLayout>
        <div className="flex items-center justify-center h-[60vh]"><Spinner size="lg" /></div>
      </DefaultLayout>
    );
  }

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4">
        <div className="flex items-center justify-between mb-6">
          <h1 className="text-2xl font-bold">{t('providers.title')}</h1>
          <Button onPress={() => setShowForm(true)}>{t('providers.addProvider')}</Button>
        </div>

        <div className="space-y-4">
          {providers.map(p => (
            <ProviderCard
              key={p.name}
              provider={p}
              isCurrent={currentProvider === p.name}
              switching={switching}
              onSwitch={() => handleSwitch(p.name)}
              onEdit={() => setEditingProvider(p)}
              onDelete={() => handleDelete(p.name)}
              onModelsUpdated={() => loadProviders()}
            />
          ))}
          {providers.length === 0 && (
            <div className="text-center text-default-400 py-12">{t('providers.noProviders')}</div>
          )}
        </div>
      </div>

      <ProviderFormModal
        isOpen={showForm}
        onClose={() => setShowForm(false)}
        onSave={handleCreate}
        saving={saving}
      />

      <ProviderFormModal
        isOpen={!!editingProvider}
        editingProvider={editingProvider}
        onClose={() => setEditingProvider(null)}
        onSave={handleUpdate}
        saving={saving}
      />
    </DefaultLayout>
  );
}

export default ProvidersPage;
