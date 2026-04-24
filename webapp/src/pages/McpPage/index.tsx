import { useEffect, useState } from 'react';
import { Button, Card, CardBody, CardHeader, Chip, Modal, ModalContent, ModalHeader, ModalBody, ModalFooter, Input, Select, SelectItem, Switch, Spinner, Accordion, AccordionItem, toast, Textarea } from '@heroui/react';
import { useOverlayState } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import { Plus, ArrowsRotateRight, Pencil, TrashBin } from '@gravity-ui/icons';

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

const emptyForm = (): McpServer => ({
  name: '',
  transport: 'stdio',
  command: '',
  args: [],
  env: {},
  url: '',
  enabled: true,
  status: 'disconnected',
  toolCount: 0,
});

function McpPage() {
  const { t } = useI18n();
  const [servers, setServers] = useState<McpServer[]>([]);
  const [loading, setLoading] = useState(true);
  const [form, setForm] = useState<McpServer>(emptyForm());
  const [editingName, setEditingName] = useState<string | null>(null);
  const [deletingName, setDeletingName] = useState<string | null>(null);
  const [expandedTools, setExpandedTools] = useState<Record<string, McpTool[]>>({});
  const [argsText, setArgsText] = useState('');
  const [envText, setEnvText] = useState('');

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

  const openAdd = () => {
    setForm(emptyForm());
    setEditingName(null);
    setArgsText('');
    setEnvText('');
    formModal.open();
  };

  const openEdit = (s: McpServer) => {
    setForm({ ...s });
    setEditingName(s.name);
    setArgsText(s.args.join('\n'));
    setEnvText(Object.entries(s.env).map(([k, v]) => `${k}=${v}`).join('\n'));
    formModal.open();
  };

  const openDelete = (name: string) => {
    setDeletingName(name);
    deleteModal.open();
  };

  const handleSave = async () => {
    if (!form.name.trim()) {
      toast.error(t('mcp.nameRequired'));
      return;
    }

    const payload = {
      ...form,
      args: argsText.split('\n').map(s => s.trim()).filter(Boolean),
      env: Object.fromEntries(
        envText.split('\n').map(s => s.trim()).filter(Boolean)
          .map(line => { const i = line.indexOf('='); return i > 0 ? [line.slice(0, i), line.slice(i + 1)] : null; })
          .filter((x): x is [string, string] => x !== null)
      ),
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
      toast.error(t('common.error'));
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
      toast.error(t('common.error'));
    }
  };

  const handleRestart = async (name: string) => {
    try {
      await api.post(`/api/mcp/servers/${encodeURIComponent(name)}/restart`, {});
      toast.success(t('mcp.restartSuccess'));
      await loadServers();
    } catch {
      toast.error(t('common.error'));
    }
  };

  const loadTools = async (name: string) => {
    try {
      const res = await api.get<McpToolsResponse>(`/api/mcp/servers/${encodeURIComponent(name)}/tools`);
      if (res.success) setExpandedTools(prev => ({ ...prev, [name]: res.data }));
    } catch { /* ignore */ }
  };

  const statusColor = (status: string) => {
    switch (status) {
      case 'connected': return 'success';
      case 'connecting': return 'warning';
      case 'error': return 'danger';
      case 'disabled': return 'default';
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
            <Button size="sm" variant="flat" onPress={loadServers}>
              <ArrowsRotateRight className="w-4 h-4" />
              {t('common.refresh')}
            </Button>
            <Button size="sm" color="primary" onPress={openAdd}>
              <Plus className="w-4 h-4" />
              {t('mcp.addServer')}
            </Button>
          </div>
        </div>

        {servers.length === 0 ? (
          <Card>
            <CardBody>
              <p className="text-center text-default-500 py-8">{t('mcp.noServers')}</p>
            </CardBody>
          </Card>
        ) : (
          <div className="space-y-4">
            {servers.map(s => (
              <Card key={s.name} className="shadow-sm">
                <CardHeader className="flex justify-between items-center pb-1">
                  <div className="flex items-center gap-3">
                    <span className="font-semibold text-lg">{s.name}</span>
                    <Chip size="sm" variant="flat" color={statusColor(s.status)}>
                      {statusLabel(s.status)}
                    </Chip>
                    <Chip size="sm" variant="flat">{s.transport.toUpperCase()}</Chip>
                    {s.status === 'connected' && (
                      <Chip size="sm" variant="flat" color="primary">{s.toolCount} {t('mcp.tools')}</Chip>
                    )}
                  </div>
                  <div className="flex gap-1">
                    <Button isIconOnly size="sm" variant="light" onPress={() => handleRestart(s.name)} title={t('mcp.restart')}>
                      <ArrowsRotateRight className="w-4 h-4" />
                    </Button>
                    <Button isIconOnly size="sm" variant="light" onPress={() => openEdit(s)} title={t('common.edit')}>
                      <Pencil className="w-4 h-4" />
                    </Button>
                    <Button isIconOnly size="sm" variant="light" color="danger" onPress={() => openDelete(s.name)} title={t('common.delete')}>
                      <TrashBin className="w-4 h-4" />
                    </Button>
                  </div>
                </CardHeader>
                <CardBody className="pt-0">
                  {s.error && <p className="text-danger text-sm mb-2">{s.error}</p>}
                  <p className="text-sm text-default-500">
                    {s.transport === 'stdio' ? `${s.command} ${s.args.join(' ')}` : s.url}
                  </p>
                  {s.status === 'connected' && s.toolCount > 0 && (
                    <Accordion variant="light" className="mt-2 px-0">
                      <AccordionItem
                        key="tools"
                        title={<span className="text-sm font-medium">{t('mcp.viewTools')} ({s.toolCount})</span>}
                        onPress={() => { if (!expandedTools[s.name]) loadTools(s.name); }}
                      >
                        {expandedTools[s.name] ? (
                          <div className="space-y-2">
                            {expandedTools[s.name].map(tool => (
                              <div key={tool.name} className="border border-default-200 rounded-lg p-3">
                                <p className="font-mono text-sm font-semibold">{tool.name}</p>
                                {tool.description && <p className="text-sm text-default-500 mt-1">{tool.description}</p>}
                                {tool.inputSchema && (
                                  <pre className="text-xs bg-default-100 rounded p-2 mt-2 overflow-x-auto">
                                    {JSON.stringify(tool.inputSchema, null, 2)}
                                  </pre>
                                )}
                              </div>
                            ))}
                          </div>
                        ) : (
                          <Spinner size="sm" />
                        )}
                      </AccordionItem>
                    </Accordion>
                  )}
                </CardBody>
              </Card>
            ))}
          </div>
        )}
      </div>

      {/* Add/Edit Modal */}
      <Modal isOpen={formModal.isOpen} onClose={formModal.close} size="lg">
        <ModalContent>
          <ModalHeader>{editingName ? t('mcp.editServer') : t('mcp.addServer')}</ModalHeader>
          <ModalBody className="space-y-4">
            <Input
              label={t('mcp.serverName')}
              value={form.name}
              onValueChange={v => setForm(f => ({ ...f, name: v }))}
              isDisabled={!!editingName}
            />
            <Select
              label={t('mcp.transport')}
              selectedKeys={[form.transport]}
              onSelectionChange={keys => {
                const val = Array.from(keys)[0] as string;
                setForm(f => ({ ...f, transport: val }));
              }}
            >
              <SelectItem key="stdio">stdio</SelectItem>
              <SelectItem key="sse">Streamable HTTP</SelectItem>
            </Select>

            {form.transport === 'stdio' ? (
              <>
                <Input
                  label={t('mcp.command')}
                  value={form.command}
                  onValueChange={v => setForm(f => ({ ...f, command: v }))}
                  placeholder="npx"
                />
                <Textarea
                  label={t('mcp.args')}
                  value={argsText}
                  onValueChange={setArgsText}
                  placeholder={t('mcp.argsHint')}
                  minRows={2}
                />
                <Textarea
                  label={t('mcp.env')}
                  value={envText}
                  onValueChange={setEnvText}
                  placeholder={t('mcp.envHint')}
                  minRows={2}
                />
              </>
            ) : (
              <Input
                label="URL"
                value={form.url}
                onValueChange={v => setForm(f => ({ ...f, url: v }))}
                placeholder="http://localhost:3000/mcp"
              />
            )}

            <Switch isSelected={form.enabled} onValueChange={v => setForm(f => ({ ...f, enabled: v }))}>
              {t('mcp.enabled')}
            </Switch>
          </ModalBody>
          <ModalFooter>
            <Button variant="flat" onPress={formModal.close}>{t('common.cancel')}</Button>
            <Button color="primary" onPress={handleSave}>{t('common.save')}</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>

      {/* Delete Modal */}
      <Modal isOpen={deleteModal.isOpen} onClose={deleteModal.close}>
        <ModalContent>
          <ModalHeader>{t('mcp.deleteServer')}</ModalHeader>
          <ModalBody>
            <p>{t('mcp.deleteConfirm')}</p>
          </ModalBody>
          <ModalFooter>
            <Button variant="flat" onPress={deleteModal.close}>{t('common.cancel')}</Button>
            <Button color="danger" onPress={handleDelete}>{t('common.delete')}</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    </DefaultLayout>
  );
}

export default McpPage;
