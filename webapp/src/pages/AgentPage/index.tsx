import { useEffect, useState } from 'react';
import { Card, Button, Spinner, Chip, toast } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';

interface AgentInfo {
  name: string;
  fileName: string;
  preview: string;
  size: number;
}

interface AgentsResponse {
  success: boolean;
  data: {
    agents: AgentInfo[];
    currentAgent: string;
  };
}

interface AgentDetailResponse {
  success: boolean;
  data: {
    name: string;
    content: string;
  };
}

function AgentPage() {
  const { t } = useI18n();
  const [agents, setAgents] = useState<AgentInfo[]>([]);
  const [currentAgent, setCurrentAgent] = useState('');
  const [loading, setLoading] = useState(true);
  const [expandedAgent, setExpandedAgent] = useState<string | null>(null);
  const [agentContent, setAgentContent] = useState<string>('');
  const [contentLoading, setContentLoading] = useState(false);
  const [switching, setSwitching] = useState(false);

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
    } catch {
      // silently fail
    } finally {
      setLoading(false);
    }
  };

  const loadAgentContent = async (name: string) => {
    if (expandedAgent === name) {
      setExpandedAgent(null);
      return;
    }
    try {
      setContentLoading(true);
      setExpandedAgent(name);
      const res = await api.get<AgentDetailResponse>(`/api/agents/${name}`);
      if (res.success) {
        setAgentContent(res.data.content);
      }
    } catch {
      // silently fail
    } finally {
      setContentLoading(false);
    }
  };

  const switchAgent = async (fileName: string) => {
    if (!confirm(t('agent.switchConfirm'))) return;
    try {
      setSwitching(true);
      await api.put('/api/config/agent', { memoryFile: fileName });
      await api.post('/api/gateway/restart', {});
      toast.success(t('agent.switchSuccess'));
      setCurrentAgent(fileName);
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
        <h1 className="text-2xl font-bold mb-6">{t('agent.title')}</h1>

        {agents.length === 0 ? (
          <Card>
            <Card.Content>
              <p className="text-gray-500 text-center py-8">{t('agent.noAgents')}</p>
            </Card.Content>
          </Card>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {agents.map((agent) => {
              const isCurrent = currentAgent === agent.fileName || currentAgent === `agent/${agent.fileName}`;
              return (
                <Card key={agent.name} className={`${isCurrent ? 'ring-2 ring-blue-500' : ''}`}>
                  <Card.Header>
                    <div className="flex items-center justify-between w-full">
                      <Card.Title className="text-lg">{agent.name}</Card.Title>
                      {isCurrent && (
                        <Chip size="sm" color="accent" variant="soft">
                          {t('agent.current')}
                        </Chip>
                      )}
                    </div>
                  </Card.Header>
                  <Card.Content>
                    <p className="text-sm text-gray-500 mb-3 line-clamp-3">{agent.preview}</p>
                    <p className="text-xs text-gray-400 mb-4">{agent.size} chars</p>

                    <div className="flex gap-2">
                      <Button
                        size="sm"
                        variant="secondary"
                        onPress={() => loadAgentContent(agent.name)}
                      >
                        {t('agent.preview')}
                      </Button>
                      {!isCurrent && (
                        <Button
                          size="sm"
                          onPress={() => switchAgent(`agent/${agent.fileName}`)}
                          isDisabled={switching}
                        >
                          {switching ? <Spinner size="sm" className="mr-1" /> : null}
                          {t('agent.switchAgent')}
                        </Button>
                      )}
                    </div>

                    {expandedAgent === agent.name && (
                      <div className="mt-4 p-3 bg-gray-50 dark:bg-gray-800 rounded-lg max-h-96 overflow-y-auto">
                        {contentLoading ? (
                          <div className="flex justify-center py-4">
                            <Spinner size="sm" />
                          </div>
                        ) : (
                          <pre className="whitespace-pre-wrap text-sm font-mono">{agentContent}</pre>
                        )}
                      </div>
                    )}
                  </Card.Content>
                </Card>
              );
            })}
          </div>
        )}
      </div>
    </DefaultLayout>
  );
}

export default AgentPage;
