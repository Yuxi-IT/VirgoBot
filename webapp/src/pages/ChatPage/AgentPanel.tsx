import { useEffect, useState } from 'react';
import { Button, Spinner, Chip, ListBox, Label, Description, toast, Card } from '@heroui/react';
import { api } from '../../services/api';
import AgentFormModal from './AgentFormModal';
import type { AgentInfo, AgentsResponse } from './types';

export default function AgentPanel() {
  const [agents, setAgents] = useState<AgentInfo[]>([]);
  const [currentAgent, setCurrentAgent] = useState('');
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [switching, setSwitching] = useState(false);

  useEffect(() => { loadAgents(); }, []);

  const loadAgents = async () => {
    try {
      setLoading(true);
      const res = await api.get<AgentsResponse>('/api/agents');
      if (res.success) {
        setAgents(res.data.agents);
        setCurrentAgent(res.data.currentAgent);
      }
    } catch { /* silent */ } finally { setLoading(false); }
  };

  const switchAgent = async (agent: AgentInfo, createNewSession: boolean) => {
    try {
      setSwitching(true);
      if (createNewSession) {
        const res = await api.post<{ success: boolean; data: { fileName: string } }>('/api/sessions', {});
        if (res.success) {
          await api.put('/api/sessions/switch', { session: res.data.fileName });
        }
      }
      await api.put('/api/config/agent', { memoryFile: agent.memoryPath });
      await api.post('/api/gateway/restart', {});
      toast.success('设定已切换');
      setCurrentAgent(agent.memoryPath);
    } catch {
      toast.danger('切换失败');
    } finally {
      setSwitching(false);
    }
  };

  const handleSwitch = (agent: AgentInfo) => {
    if (confirm('切换设定时是否创建新会话？\n\n确定 = 新会话\n取消 = 保留当前会话')) {
      switchAgent(agent, true);
    } else {
      switchAgent(agent, false);
    }
  };

  const deleteAgent = async (name: string) => {
    if (!confirm('确定删除此设定？')) return;
    try {
      await api.del(`/api/agents/${encodeURIComponent(name)}`);
      toast.success('设定已删除');
      loadAgents();
    } catch {
      toast.danger('删除失败');
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-32">
        <Spinner size="sm" />
      </div>
    );
  }

  return (
    <div className="flex flex-col h-full">
      <div className="p-3 border-b flex items-center justify-between">
        <span className="font-semibold text-sm">设定</span>
        <Button size="sm" variant="ghost" onPress={() => setShowForm(true)}>新建</Button>
      </div>
      <div className="flex-1 overflow-y-auto">
        {agents.length === 0 ? (
          <div className="text-center text-default-400 text-sm py-8">暂无设定</div>
        ) : (
          <ListBox aria-label="设定列表" selectionMode="none">
            {agents.map(agent => {
              const isCurrent = currentAgent === agent.memoryPath;
              return (
                <ListBox.Item key={agent.name} id={agent.name} textValue={agent.name}>
                  <Card className="w-[320px]">
                    <Card.Header>
                      <Card.Title>
                        {agent.name}
                        {isCurrent && <Chip size="sm" color="accent">当前</Chip>}
                      </Card.Title>
                      <Card.Description>{agent.preview.slice(0, 30) + (agent.preview.length > 100 ? '...' : '')}</Card.Description>
                    </Card.Header>
                    {!isCurrent && (
                      <Card.Content className='flex gap-1 mt-1'>
                        <div className='flex gap-1 mt-1'>
                          <Button size="sm" variant="ghost" onPress={() => handleSwitch(agent)} isDisabled={switching}>
                            {switching ? <Spinner size="sm" /> : '切换'}
                          </Button>
                          <Button size="sm" variant="ghost" onPress={() => deleteAgent(agent.name)}>删除</Button>
                        </div>
                      </Card.Content>
                    )}
                  </Card>
                </ListBox.Item>
              );
            })}
          </ListBox>
        )}
      </div>

      <AgentFormModal
        isOpen={showForm}
        onClose={() => setShowForm(false)}
        onCreated={() => { setShowForm(false); loadAgents(); }}
      />
    </div>
  );
}
