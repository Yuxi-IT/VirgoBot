import { useEffect, useState } from 'react';
import { Button, Spinner, Chip, ListBox, toast, Card, Modal } from '@heroui/react';
import { api } from '../../services/api';
import AgentFormModal from './AgentFormModal';
import type { AgentInfo, AgentsResponse } from './types';

export default function AgentPanel() {
  const [agents, setAgents] = useState<AgentInfo[]>([]);
  const [currentAgent, setCurrentAgent] = useState('');
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [switching, setSwitching] = useState(false);

  // Modal states
  const [switchTarget, setSwitchTarget] = useState<AgentInfo | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null);

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

  const handleSwitchConfirm = (createNewSession: boolean) => {
    if (switchTarget) {
      switchAgent(switchTarget, createNewSession);
    }
    setSwitchTarget(null);
  };

  const handleDeleteConfirm = async () => {
    if (!deleteTarget) return;
    try {
      await api.del(`/api/agents/${encodeURIComponent(deleteTarget)}`);
      toast.success('设定已删除');
      loadAgents();
    } catch {
      toast.danger('删除失败');
    }
    setDeleteTarget(null);
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
                          <Button size="sm" variant="ghost" onPress={() => setSwitchTarget(agent)} isDisabled={switching}>
                            {switching ? <Spinner size="sm" /> : '切换'}
                          </Button>
                          <Button size="sm" variant="ghost" onPress={() => setDeleteTarget(agent.name)}>删除</Button>
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

      {/* Switch confirmation modal */}
      <Modal>
        <Modal.Backdrop isOpen={!!switchTarget} onOpenChange={(open) => { if (!open) setSwitchTarget(null); }}>
          <Modal.Container size="sm">
            <Modal.Dialog>
              <Modal.Header>
                <Modal.Heading>切换设定</Modal.Heading>
              </Modal.Header>
              <Modal.Body>
                <p className="text-sm">切换设定时是否创建新会话？</p>
              </Modal.Body>
              <Modal.Footer>
                <Button variant="ghost" size="sm" onPress={() => setSwitchTarget(null)}>取消</Button>
                <Button variant="ghost" size="sm" onPress={() => handleSwitchConfirm(false)}>保留当前会话</Button>
                <Button variant="primary" size="sm" onPress={() => handleSwitchConfirm(true)}>新建会话</Button>
              </Modal.Footer>
            </Modal.Dialog>
          </Modal.Container>
        </Modal.Backdrop>
      </Modal>

      {/* Delete confirmation modal */}
      <Modal>
        <Modal.Backdrop isOpen={!!deleteTarget} onOpenChange={(open) => { if (!open) setDeleteTarget(null); }}>
          <Modal.Container size="sm">
            <Modal.Dialog>
              <Modal.Header>
                <Modal.Heading>确认删除</Modal.Heading>
              </Modal.Header>
              <Modal.Body>
                <p className="text-sm">确定删除设定「{deleteTarget}」？此操作不可撤销。</p>
              </Modal.Body>
              <Modal.Footer>
                <Button variant="ghost" size="sm" onPress={() => setDeleteTarget(null)}>取消</Button>
                <Button variant="danger" size="sm" onPress={handleDeleteConfirm}>删除</Button>
              </Modal.Footer>
            </Modal.Dialog>
          </Modal.Container>
        </Modal.Backdrop>
      </Modal>
    </div>
  );
}
