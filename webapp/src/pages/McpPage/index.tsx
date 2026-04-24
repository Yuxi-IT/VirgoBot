import { useEffect, useState } from 'react';
import { Button, Card, Chip, Modal, TextField, Label, Input, TextArea, Switch, Spinner, toast } from '@heroui/react';
import { useOverlayState } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import { Plus, ArrowsRotateRight, Pencil, TrashBin, ChevronDown, ChevronUp } from '@gravity-ui/icons';

interface McpServer {
  name: string;
  transport: string;
  command: string;
  args: string[];
  env: Record<string, string>;
  url: string;
  enabled: boolean;
  status: string;
  toolCount: number;
  error?: string;
}

interface McpTool {
  name: string;
  description?: string;
  inputSchema?: Record<string, unknown>;
}

interface McpServersResponse {
  success: boolean;
  data: McpServer[];
}

interface McpToolsResponse {
  success: boolean;
  data: McpTool[];
}

function McpPage() {
  const { t } = useI18n();
  const [servers, setServers] = useState<McpServer[]>([]);
  const [loading, setLoading] = useState(true);
  const [formName, setFormName] = useState('');
  const [formTransport, setFormTransport] = useState('stdio');
  const [formCommand, setFormCommand] = useState('');
  const [formArgsText, setFormArgsText] = useState('');
  const [formEnvText, setFormEnvText] = useState('');
  const [formUrl, setFormUrl] = useState('');
  const [formEnabled, setFormEnabled] = useState(true);
  const [editingName, setEditingName] = useState<string | null>(null);
  const [deletingName, setDeletingName] = useState<string | null>(null);
  const [expandedServer, setExpandedServer] = useState<string | null>(null);
  const [expandedTools, setExpandedTools] = useState<Record<string, McpTool[]>>({});

  const formModal = useOverlayState();
  const deleteModal = useOverlayState();

  useEffect(() => { loadServers(); }, []);

  const loadServers = async () => {
    try {
      setLoading(true);
      const res = await api.get<McpServersResponse>('/api/mcp/servers');
      if (res.success) setServers(res.data);
    } catch { /* ignore */ } finally { setLoading(false); }
  };

  const resetForm = () => {
    setFormName('');
    setFormTransport('stdio');
    setFormCommand('');
    setFormArgsText('');
    setFormEnvText('');
    setFormUrl('');
    setFormEnabled(true);
  };

  const openAdd = () => {
    resetForm();
    setEditingName(null);
    formModal.open();
  };

  const openEdit = (s: McpServer) => {
    setFormName(s.name);
    setFormTransport(s.transport);
    setFormCommand(s.command);
    setFormArgsText(s.args.join('\n'));
    setFormEnvText(Object.entries(s.env).map(([k, v]) => `${k}=${v}`).join('\n'));
    setFormUrl(s.url);
    setFormEnabled(s.enabled);
    setEditingName(s.name);
    formModal.open();
  };

  const openDelete = (name: string) => {
    setDeletingName(name);
    deleteModal.open();
  };

  const handleSave = async () => {
    if (!formName.trim()) {
      toast.danger(t('mcp.nameRequired'));
      return;
    }

    const payload = {
      name: formName,
      transport: formTransport,
      command: formCommand,
      args: formArgsText.split('\n').map((s: string) => s.trim()).filter(Boolean),
      env: Object.fromEntries(
        formEnvText.split('\n').map((s: string) => s.trim()).filter(Boolean)
          .map((line: string) => { const i = line.indexOf('='); return i > 0 ? [line.slice(0, i), line.slice(i + 1)] : null; })
          .filter((x): x is [string, string] => x !== null)
      ),
      url: formUrl,
      enabled: formEnabled,
    };

    try {
      if (editingName) {
        await api.put(`/api/mcp/servers/${encodeURIComponent(editingName)}`, payload);
        toast.success(t('mcp.updateSuccess'));
      } else {
        await api.post('/api/mcp/servers', payload);
        toast.success(t('mcp.addSuccess'));
      }
      formModal.close();
      await loadServers();
    } catch {
      toast.danger(t('common.error'));
    }
  };

  const handleDelete = async () => {
    if (!deletingName) return;
    try {
      await api.del(`/api/mcp/servers/${encodeURIComponent(deletingName)}`);
      toast.success(t('mcp.deleteSuccess'));
      deleteModal.close();
      await loadServers();
    } catch {
      toast.danger(t('common.error'));
    }
  };

  const handleRestart = async (name: string) => {
    try {
      await api.post(`/api/mcp/servers/${encodeURIComponent(name)}/restart`, {});
      toast.success(t('mcp.restartSuccess'));
      await loadServers();
    } catch {
      toast.danger(t('common.error'));
    }
  };

  const toggleTools = async (name: string) => {
    if (expandedServer === name) {
      setExpandedServer(null);
      return;
    }
    setExpandedServer(name);
    if (!expandedTools[name]) {
      try {
        const res = await api.get<McpToolsResponse>(`/api/mcp/servers/${encodeURIComponent(name)}/tools`);
        if (res.success) setExpandedTools(prev => ({ ...prev, [name]: res.data }));
      } catch { /* ignore */ }
    }
  };

  const statusColor = (status: string): 'success' | 'warning' | 'danger' | 'default' | 'accent' => {
    switch (status) {
      case 'connected': return 'success';
      case 'connecting': return 'warning';
      case 'error': return 'danger';
      default: return 'default';
    }
  };

  const statusLabel = (status: string) => {
    switch (status) {
      case 'connected': return t('mcp.connected');
      case 'connecting': return t('mcp.connecting');
      case 'error': return t('mcp.error');
      case 'disabled': return t('mcp.disabled');
      case 'disconnected': return t('mcp.disconnected');
      default: return status;
    }
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
      <div className="max-w-4xl mx-auto p-4 sm:p-6 space-y-6">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-bold">{t('mcp.title')}</h1>
          <div className="flex gap-2">
            <Button size="sm" variant="secondary" onPress={loadServers}>
              <ArrowsRotateRight className="w-4 h-4" />
              {t('common.refresh')}
            </Button>
            <Button size="sm" onPress={openAdd}>
              <Plus className="w-4 h-4" />
              {t('mcp.addServer')}
            </Button>
          </div>
        </div>

        {servers.length === 0 ? (
          <Card>
            <div className="p-8 text-center text-gray-500">{t('mcp.noServers')}</div>
          </Card>
        ) : (
          <div className="space-y-4">
            {servers.map(s => (
              <Card key={s.name}>
                <div className="p-4">
                  <div className="flex justify-between items-center">
                    <div className="flex items-center gap-3 flex-wrap">
                      <span className="font-semibold text-lg">{s.name}</span>
                      <Chip size="sm" color={statusColor(s.status)}>
                        {statusLabel(s.status)}
                      </Chip>
                      <Chip size="sm" variant="soft">{s.transport.toUpperCase()}</Chip>
                      {s.status === 'connected' && (
                        <Chip size="sm" variant="soft" color="accent">{s.toolCount} {t('mcp.tools')}</Chip>
                      )}
                    </div>
                    <div className="flex gap-1">
                      <Button size="sm" variant="ghost" onPress={() => handleRestart(s.name)}>
                        <ArrowsRotateRight className="w-4 h-4" />
                      </Button>
                      <Button size="sm" variant="ghost" onPress={() => openEdit(s)}>
                        <Pencil className="w-4 h-4" />
                      </Button>
                      <Button size="sm" variant="ghost" onPress={() => openDelete(s.name)}>
                        <TrashBin className="w-4 h-4" />
                      </Button>
                    </div>
                  </div>
                  {s.error && <p className="text-red-500 text-sm mt-2">{s.error}</p>}
                  <p className="text-sm text-gray-500 mt-1">
                    {s.transport === 'stdio' ? `${s.command} ${s.args.join(' ')}` : s.url}
                  </p>
                  {s.status === 'connected' && s.toolCount > 0 && (
                    <div className="mt-3">
                      <Button size="sm" variant="secondary" onPress={() => toggleTools(s.name)}>
                        {expandedServer === s.name ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
                        {t('mcp.viewTools')} ({s.toolCount})
                      </Button>
                      {expandedServer === s.name && (
                        <div className="mt-3 space-y-2">
                          {expandedTools[s.name] ? (
                            expandedTools[s.name].map(tool => (
                              <div key={tool.name} className="border border-gray-200 dark:border-gray-700 rounded-lg p-3">
                                <p className="font-mono text-sm font-semibold">{tool.name}</p>
                                {tool.description && <p className="text-sm text-gray-500 mt-1">{tool.description}</p>}
                                {tool.inputSchema && (
                                  <pre className="text-xs bg-gray-100 dark:bg-gray-800 rounded p-2 mt-2 overflow-x-auto">
                                    {JSON.stringify(tool.inputSchema, null, 2)}
                                  </pre>
                                )}
                              </div>
                            ))
                          ) : (
                            <Spinner size="sm" />
                          )}
                        </div>
                      )}
                    </div>
                  )}
                </div>
              </Card>
            ))}
          </div>
        )}
      </div>

      {/* Add/Edit Modal */}
      <Modal>
        <Modal.Backdrop isOpen={formModal.isOpen} onOpenChange={formModal.close}>
          <Modal.Container size="lg">
            <Modal.Dialog>
              <Modal.Header>
                <Modal.Heading>{editingName ? t('mcp.editServer') : t('mcp.addServer')}</Modal.Heading>
              </Modal.Header>
              <Modal.Body>
                <div className="space-y-4">
                  <TextField value={formName} onChange={setFormName} isDisabled={!!editingName}>
                    <Label>{t('mcp.serverName')}</Label>
                    <Input placeholder="my-server" />
                  </TextField>

                  <div>
                    <Label>{t('mcp.transport')}</Label>
                    <div className="flex gap-2 mt-2">
                      <Button size="sm" variant={formTransport === 'stdio' ? 'primary' : 'secondary'} onPress={() => setFormTransport('stdio')}>
                        Stdio
                      </Button>
                      <Button size="sm" variant={formTransport === 'sse' ? 'primary' : 'secondary'} onPress={() => setFormTransport('sse')}>
                        Streamable HTTP
                      </Button>
                    </div>
                  </div>

                  {formTransport === 'stdio' ? (
                    <>
                      <TextField value={formCommand} onChange={setFormCommand}>
                        <Label>{t('mcp.command')}</Label>
                        <br/>
                        <Input placeholder="npx" />
                      </TextField>
                      <div>
                        <Label>{t('mcp.args')}</Label>
                        <br/>
                        <TextArea
                          value={formArgsText}
                          fullWidth
                          onChange={(e) => setFormArgsText(e.target.value)}
                          placeholder={t('mcp.argsHint')}
                          rows={2}
                        />
                      </div>
                      <div>
                        <Label>{t('mcp.env')}</Label>
                        <br/>
                        <TextArea
                          value={formEnvText}
                          fullWidth
                          onChange={(e) => setFormEnvText(e.target.value)}
                          placeholder={t('mcp.envHint')}
                          rows={2}
                        />
                      </div>
                    </>
                  ) : (
                    <TextField value={formUrl} onChange={setFormUrl}>
                      <Label>URL</Label>
                      <Input placeholder="http://localhost:3000/mcp" />
                    </TextField>
                  )}

                  <div>
                    <Label>{t('mcp.enabled')}</Label>
                    <div className="mt-2">
                      <Switch isSelected={formEnabled} onChange={setFormEnabled} />
                    </div>
                  </div>
                </div>
              </Modal.Body>
              <Modal.Footer>
                <Button variant="secondary" onPress={formModal.close}>{t('common.cancel')}</Button>
                <Button onPress={handleSave}>{t('common.save')}</Button>
              </Modal.Footer>
            </Modal.Dialog>
          </Modal.Container>
        </Modal.Backdrop>
      </Modal>

      {/* Delete Modal */}
      <Modal>
        <Modal.Backdrop isOpen={deleteModal.isOpen} onOpenChange={deleteModal.close}>
          <Modal.Container>
            <Modal.Dialog>
              <Modal.Header>
                <Modal.Heading>{t('mcp.deleteServer')}</Modal.Heading>
              </Modal.Header>
              <Modal.Body>
                <p>{t('mcp.deleteConfirm')}</p>
              </Modal.Body>
              <Modal.Footer>
                <Button variant="secondary" onPress={deleteModal.close}>{t('common.cancel')}</Button>
                <Button variant="danger" onPress={handleDelete}>{t('common.delete')}</Button>
              </Modal.Footer>
            </Modal.Dialog>
          </Modal.Container>
        </Modal.Backdrop>
      </Modal>
    </DefaultLayout>
  );
}

export default McpPage;
