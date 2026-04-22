import { useEffect, useState } from 'react';
import { Button, Spinner, Chip, ListBox, toast, Card, Modal, TextArea } from '@heroui/react';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import AgentFormModal from './AgentFormModal';
import type { AgentInfo, AgentsResponse } from './types';

export default function AgentPanel() {
  const { t } = useI18n();
  const [agents, setAgents] = useState<AgentInfo[]>([]);
  const [currentAgent, setCurrentAgent] = useState('');
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [switching, setSwitching] = useState(false);

  const [switchTarget, setSwitchTarget] = useState<AgentInfo | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null);
  const [editTarget, setEditTarget] = useState<AgentInfo | null>(null);
  const [editContent, setEditContent] = useState('');
  const [editLoading, setEditLoading] = useState(false);
  const [editSaving, setEditSaving] = useState(false);

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
      toast.success(t('chatPage.agentSwitched'));
      setCurrentAgent(agent.memoryPath);
    } catch {
      toast.danger(t('chatPage.agentSwitchFailed'));
    } finally {
      setSwitching(false);
    }
  };

  const handleSwitchConfirm = (createNewSession: boolean) => {
    if (switchTarget) switchAgent(switchTarget, createNewSession);
    setSwitchTarget(null);
  };

  const handleDeleteConfirm = async () => {
    if (!deleteTarget) return;
    try {
      await api.del(`/api/agents/${encodeURIComponent(deleteTarget)}`);
      toast.success(t('chatPage.agentDeleted'));
      loadAgents();
    } catch {
      toast.danger(t('chatPage.agentDeleteFailed'));
    }
    setDeleteTarget(null);
  };

  const openEdit = async (agent: AgentInfo) => {
    setEditTarget(agent);
    setEditLoading(true);
    try {
      const res = await api.get<{ success: boolean; data: { name: string; content: string } }>(
        `/api/agents/${encodeURIComponent(agent.name)}`
      );
      if (res.success) setEditContent(res.data.content);
      else toast.danger(t('chatPage.agentLoadFailed'));
    } catch {
      toast.danger(t('chatPage.agentLoadFailed'));
    } finally {
      setEditLoading(false);
    }
  };

  const saveEdit = async () => {
    if (!editTarget) return;
    setEditSaving(true);
    try {
      await api.put(`/api/agents/${encodeURIComponent(editTarget.name)}`, { content: editContent });
      toast.success(t('chatPage.agentSaved'));
      await api.post('/api/gateway/restart', {});
      setEditTarget(null);
      loadAgents();
    } catch {
      toast.danger(t('chatPage.agentSaveFailed'));
    } finally {
      setEditSaving(false);
    }
  };

  if (loading) {
    return <div className="flex items-center justify-center h-32"><Spinner size="sm" /></div>;
  }

  return (
    <div className="flex flex-col h-full">
      <div className="p-3 border-b flex items-center justify-between">
        <span className="font-semibold text-sm">{t('chatPage.agentTitle')}</span>
        <Button size="sm" variant="ghost" onPress={() => setShowForm(true)}>{t('chatPage.agentCreate')}</Button>
      </div>
      <div className="flex-1 overflow-y-auto">
        {agents.length === 0 ? (
          <div className="text-center text-default-400 text-sm py-8">{t('chatPage.noAgents')}</div>
        ) : (
          <ListBox aria-label="agents" selectionMode="none">
            {agents.map(agent => {
              const isCurrent = currentAgent === agent.memoryPath;
              return (
                <ListBox.Item key={agent.name} id={agent.name} textValue={agent.name}>
                  <Card className="w-[320px]">
                    <Card.Header>
                      <Card.Title>
                        {agent.name}
                        {isCurrent && <Chip size="sm" color="accent">{t('chatPage.agentCurrent')}</Chip>}
                      </Card.Title>
                      <Card.Description className='text-xs'>{agent.preview.slice(0, 60) + (agent.preview.length > 60 ? '...' : '')}</Card.Description>
                    </Card.Header>
                    <Card.Content>
                      <div className="flex gap-1 mt-1">
                        <Button size="sm" variant="ghost" onPress={() => openEdit(agent)}>{t('chatPage.agentEdit')}</Button>
                        {!isCurrent && (
                          <>
                            <Button size="sm" variant="ghost" onPress={() => setSwitchTarget(agent)} isDisabled={switching}>
                              {switching ? <Spinner size="sm" /> : t('chatPage.agentSwitch')}
                            </Button>
                            <Button size="sm" variant="ghost" onPress={() => setDeleteTarget(agent.name)}>{t('common.delete')}</Button>
                          </>
                        )}
                      </div>
                    </Card.Content>
                  </Card>
                </ListBox.Item>
              );
            })}
          </ListBox>
        )}
      </div>

      <AgentFormModal isOpen={showForm} onClose={() => setShowForm(false)} onCreated={() => { setShowForm(false); loadAgents(); }} />

      {/* Edit modal */}
      <Modal>
        <Modal.Backdrop isOpen={!!editTarget} onOpenChange={(open) => { if (!open) setEditTarget(null); }}>
          <Modal.Container size="lg">
            <Modal.Dialog>
              <Modal.Header>
                <Modal.Heading>{editTarget?.name}</Modal.Heading>
              </Modal.Header>
              <Modal.Body>
                {editLoading ? (
                  <div className="flex justify-center py-8"><Spinner size="sm" /></div>
                ) : (
                  <TextArea
                    className="font-mono w-full"
                    rows={16}
                    value={editContent}
                    onChange={(e) => setEditContent(e.target.value)}
                  />
                )}
              </Modal.Body>
              <Modal.Footer>
                <Button variant="ghost" size="sm" onPress={() => setEditTarget(null)}>{t('common.cancel')}</Button>
                <Button variant="primary" size="sm" onPress={saveEdit} isDisabled={editSaving || editLoading}>
                  {editSaving ? <Spinner size="sm" /> : t('common.save')}
                </Button>
              </Modal.Footer>
            </Modal.Dialog>
          </Modal.Container>
        </Modal.Backdrop>
      </Modal>

      {/* Switch confirmation modal */}
      <Modal>
        <Modal.Backdrop isOpen={!!switchTarget} onOpenChange={(open) => { if (!open) setSwitchTarget(null); }}>
          <Modal.Container size="sm">
            <Modal.Dialog>
              <Modal.Header><Modal.Heading>{t('chatPage.switchAgentTitle')}</Modal.Heading></Modal.Header>
              <Modal.Body><p className="text-sm">{t('chatPage.switchAgentDesc')}</p></Modal.Body>
              <Modal.Footer>
                <Button variant="ghost" size="sm" onPress={() => setSwitchTarget(null)}>{t('common.cancel')}</Button>
                <Button variant="ghost" size="sm" onPress={() => handleSwitchConfirm(false)}>{t('chatPage.keepCurrentSession')}</Button>
                <Button variant="primary" size="sm" onPress={() => handleSwitchConfirm(true)}>{t('chatPage.createNewSession')}</Button>
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
              <Modal.Header><Modal.Heading>{t('chatPage.confirmDelete')}</Modal.Heading></Modal.Header>
              <Modal.Body><p className="text-sm">{t('chatPage.confirmDeleteDesc').replace('{name}', deleteTarget || '')}</p></Modal.Body>
              <Modal.Footer>
                <Button variant="ghost" size="sm" onPress={() => setDeleteTarget(null)}>{t('common.cancel')}</Button>
                <Button variant="danger" size="sm" onPress={handleDeleteConfirm}>{t('common.delete')}</Button>
              </Modal.Footer>
            </Modal.Dialog>
          </Modal.Container>
        </Modal.Backdrop>
      </Modal>
    </div>
  );
}
