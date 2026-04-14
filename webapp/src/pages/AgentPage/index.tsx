import { useEffect, useState } from 'react';
import { Card, Button, Spinner, Chip, TextArea, TextField, Label, Input, toast } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';

interface AgentInfo {
  name: string;
  fileName: string;
  memoryPath: string;
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
  const [switching, setSwitching] = useState(false);

  // View/Edit state
  const [expandedAgent, setExpandedAgent] = useState<string | null>(null);
  const [agentContent, setAgentContent] = useState<string>('');
  const [contentLoading, setContentLoading] = useState(false);
  const [editingAgent, setEditingAgent] = useState<string | null>(null);
  const [editContent, setEditContent] = useState('');
  const [savingEdit, setSavingEdit] = useState(false);

  // Create state
  const [showCreate, setShowCreate] = useState(false);
  const [newName, setNewName] = useState('');
  const [newContent, setNewContent] = useState('');
  const [creating, setCreating] = useState(false);

  // Delete state
  const [deleting, setDeleting] = useState<string | null>(null);

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

  const loadAgentContent = async (name: string) => {
    if (expandedAgent === name && editingAgent !== name) {
      setExpandedAgent(null);
      setEditingAgent(null);
      return;
    }
    try {
      setContentLoading(true);
      setExpandedAgent(name);
      setEditingAgent(null);
      const res = await api.get<AgentDetailResponse>(`/api/agents/${encodeURIComponent(name)}`);
      if (res.success) {
        setAgentContent(res.data.content);
      }
    } catch (e) {
      console.error('Failed to load agent content:', e);
    } finally {
      setContentLoading(false);
    }
  };

  const startEditing = (name: string) => {
    setEditingAgent(name);
    setEditContent(agentContent);
  };

  const cancelEditing = () => {
    setEditingAgent(null);
    setEditContent('');
  };

  const saveAgentEdit = async (name: string) => {
    if (!editContent.trim()) return;
    try {
      setSavingEdit(true);
      await api.put(`/api/agents/${encodeURIComponent(name)}`, { content: editContent });
      toast.success(t('agent.updateSuccess'));
      setEditingAgent(null);
      // Refresh agent content and list
      setAgentContent(editContent);
      await loadAgents();
    } catch {
      toast.danger(t('settings.saveFailed'));
    } finally {
      setSavingEdit(false);
    }
  };

  const createAgent = async () => {
    if (!newName.trim() || !newContent.trim()) return;
    try {
      setCreating(true);
      await api.post('/api/agents', { name: newName.trim(), content: newContent });
      toast.success(t('agent.createSuccess'));
      setShowCreate(false);
      setNewName('');
      setNewContent('');
      await loadAgents();
    } catch {
      toast.danger(t('settings.saveFailed'));
    } finally {
      setCreating(false);
    }
  };

  const deleteAgent = async (name: string, memoryPath: string) => {
    if (currentAgent === memoryPath) {
      toast.danger(t('agent.cannotDeleteCurrent'));
      return;
    }
    if (!confirm(t('agent.deleteConfirm'))) return;
    try {
      setDeleting(name);
      await api.del(`/api/agents/${encodeURIComponent(name)}`);
      toast.success(t('agent.deleteSuccess'));
      if (expandedAgent === name) {
        setExpandedAgent(null);
        setEditingAgent(null);
      }
      await loadAgents();
    } catch {
      toast.danger(t('settings.saveFailed'));
    } finally {
      setDeleting(null);
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

        {/* Create Agent Form */}
        {showCreate && (
          <Card className="mb-6">
            <Card.Header>
              <Card.Title>{t('agent.createAgent')}</Card.Title>
            </Card.Header>
            <Card.Content>
              <div className="space-y-4">
                <TextField value={newName} onChange={setNewName}>
                  <Label>{t('agent.agentName')}</Label>
                  <Input placeholder={t('agent.namePlaceholder')} />
                </TextField>
                <div>
                  <label className="text-sm font-medium mb-1 block">{t('agent.agentContent')}</label>
                  <TextArea
                    value={newContent}
                    onChange={(e) => setNewContent(e.target.value)}
                    rows={12}
                    placeholder={t('agent.contentPlaceholder')}
                    className="font-mono w-full"
                  />
                </div>
                <div className="flex gap-2">
                  <Button
                    onPress={createAgent}
                    isDisabled={creating || !newName.trim() || !newContent.trim()}
                  >
                    {creating ? <Spinner size="sm" className="mr-2" /> : null}
                    {t('agent.createAgent')}
                  </Button>
                  <Button variant="secondary" onPress={() => { setShowCreate(false); setNewName(''); setNewContent(''); }}>
                    {t('common.cancel')}
                  </Button>
                </div>
              </div>
            </Card.Content>
          </Card>
        )}

        {agents.length === 0 ? (
          <Card>
            <Card.Content>
              <p className="text-gray-500 text-center py-8">{t('agent.noAgents')}</p>
            </Card.Content>
          </Card>
        ) : (
          <div className="space-y-4">
            {agents.map((agent) => {
              const isCurrent = currentAgent === agent.memoryPath;
              const isExpanded = expandedAgent === agent.name;
              const isEditing = editingAgent === agent.name;

              return (
                <Card key={agent.name} className={`hover:scale-101 transition-all cursor-pointer ${isCurrent ? 'ring-2 ring-blue-500 bg-blue-500/10' : ''}`}>
                  <Card.Header>
                    <div className="flex items-center justify-between w-full">
                      <div className="flex items-center gap-3">
                        <Card.Title className="text-lg">{agent.name}</Card.Title>
                        {isCurrent && (
                          <Chip size="sm" color="accent" variant="soft">
                            {t('agent.current')}
                          </Chip>
                        )}
                        <span className="text-xs text-gray-400">{agent.size} chars</span>
                      </div>
                      <div className="flex gap-2">
                        <Button
                          size="sm"
                          variant="secondary"
                          onPress={() => loadAgentContent(agent.name)}
                        >
                          {isExpanded ? t('common.cancel') : t('agent.preview')}
                        </Button>
                        {!isCurrent && (
                          <>
                            <Button
                              size="sm"
                              onPress={() => switchAgent(agent.memoryPath)}
                              isDisabled={switching}
                            >
                              {switching ? <Spinner size="sm" className="mr-1" /> : null}
                              {t('agent.switchAgent')}
                            </Button>
                            <Button
                              size="sm"
                              variant="danger"
                              onPress={() => deleteAgent(agent.name, agent.memoryPath)}
                              isDisabled={deleting === agent.name}
                            >
                              {deleting === agent.name ? <Spinner size="sm" className="mr-1" /> : null}
                              {t('common.delete')}
                            </Button>
                          </>
                        )}
                      </div>
                    </div>
                  </Card.Header>

                  {!isExpanded && (
                    <Card.Content>
                      <p className="text-sm text-gray-500 line-clamp-3">{agent.preview}</p>
                    </Card.Content>
                  )}

                  {isExpanded && (
                    <Card.Content>
                      {contentLoading ? (
                        <div className="flex justify-center py-4">
                          <Spinner size="sm" />
                        </div>
                      ) : isEditing ? (
                        <div className="space-y-3">
                          <TextArea
                            value={editContent}
                            onChange={(e) => setEditContent(e.target.value)}
                            rows={18}
                            className="font-mono w-full"
                          />
                          <div className="flex gap-2">
                            <Button
                              size="sm"
                              onPress={() => saveAgentEdit(agent.name)}
                              isDisabled={savingEdit || !editContent.trim()}
                            >
                              {savingEdit ? <Spinner size="sm" className="mr-1" /> : null}
                              {t('common.save')}
                            </Button>
                            <Button size="sm" variant="secondary" onPress={cancelEditing}>
                              {t('common.cancel')}
                            </Button>
                          </div>
                        </div>
                      ) : (
                        <div className="space-y-3">
                          <div className="p-3 bg-gray-50 dark:bg-gray-800 rounded-lg max-h-96 overflow-y-auto">
                            <pre className="whitespace-pre-wrap text-sm font-mono">{agentContent}</pre>
                          </div>
                          <Button size="sm" variant="secondary" onPress={() => startEditing(agent.name)}>
                            {t('agent.editAgent')}
                          </Button>
                        </div>
                      )}
                    </Card.Content>
                  )}
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
