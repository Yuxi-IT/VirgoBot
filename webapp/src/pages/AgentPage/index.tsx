import { useEffect, useState } from 'react';
import { Card, Button, Spinner, toast } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import CreateAgentForm from './CreateAgentForm';
import AgentCard from './AgentCard';
import type { AgentInfo, AgentsResponse } from './types';

function AgentPage() {
  const { t } = useI18n();
  const [agents, setAgents] = useState<AgentInfo[]>([]);
  const [currentAgent, setCurrentAgent] = useState('');
  const [loading, setLoading] = useState(true);
  const [switching, setSwitching] = useState(false);
  const [showCreate, setShowCreate] = useState(false);

  useEffect(() => {
    loadAgents();
  }, []);

  const loadAgents = async () => {
    try {
      setLoading(true);
      const res = await api.get<AgentsResponse>('/api/agents');
      if (res.success) {
        setAgents(res.data.agents);
        setCurrentAgent(res.data.currentAgent);
      }
    } catch (e) {
      console.error('Failed to load agents:', e);
    } finally {
      setLoading(false);
    }
  };

  const switchAgent = async (memoryPath: string) => {
    if (!confirm(t('agent.switchConfirm'))) return;
    try {
      setSwitching(true);
      await api.put('/api/config/agent', { memoryFile: memoryPath });
      await api.post('/api/gateway/restart', {});
      toast.success(t('agent.switchSuccess'));
      setCurrentAgent(memoryPath);
    } catch {
      toast.danger(t('settings.saveFailed'));
    } finally {
      setSwitching(false);
    }
  };

  if (loading) {
    return (
      <DefaultLayout>
        <div className="flex items-center justify-center h-[60vh]">
          <Spinner size="lg" />
        </div>
      </DefaultLayout>
    );
  }

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4">
        <div className="flex items-center justify-between mb-6">
          <h1 className="text-2xl font-bold">{t('agent.title')}</h1>
          <Button onPress={() => setShowCreate(!showCreate)}>
            {showCreate ? t('common.cancel') : t('agent.createAgent')}
          </Button>
        </div>

        <CreateAgentForm
          visible={showCreate}
          onCreated={() => { setShowCreate(false); loadAgents(); }}
          onCancel={() => setShowCreate(false)}
        />

        {agents.length === 0 ? (
          <Card>
            <Card.Content>
              <p className="text-gray-500 text-center py-8">{t('agent.noAgents')}</p>
            </Card.Content>
          </Card>
        ) : (
          <div className="space-y-4">
            {agents.map((agent) => (
              <AgentCard
                key={agent.name}
                agent={agent}
                isCurrent={currentAgent === agent.memoryPath}
                switching={switching}
                onSwitch={switchAgent}
                onDeleted={loadAgents}
              />
            ))}
          </div>
        )}
      </div>
    </DefaultLayout>
  );
}

export default AgentPage;
