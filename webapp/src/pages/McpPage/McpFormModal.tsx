import { useState, useEffect } from 'react';
import { Modal, Button, TextField, Label, Input, TextArea, Switch } from '@heroui/react';
import { useI18n } from '../../i18n';
import type { McpServer } from './types';

interface Props {
  isOpen: boolean;
  editingServer: McpServer | null;
  onClose: () => void;
  onSave: (data: {
    name: string; transport: string; command: string;
    args: string[]; env: Record<string, string>;
    url: string; enabled: boolean;
  }) => void;
}

export default function McpFormModal({ isOpen, editingServer, onClose, onSave }: Props) {
  const { t } = useI18n();
  const [name, setName] = useState('');
  const [transport, setTransport] = useState('stdio');
  const [command, setCommand] = useState('');
  const [argsText, setArgsText] = useState('');
  const [envText, setEnvText] = useState('');
  const [url, setUrl] = useState('');
  const [enabled, setEnabled] = useState(true);

  useEffect(() => {
    if (isOpen) {
      if (editingServer) {
        setName(editingServer.name);
        setTransport(editingServer.transport);
        setCommand(editingServer.command);
        setArgsText(editingServer.args.join('\n'));
        setEnvText(Object.entries(editingServer.env).map(([k, v]) => `${k}=${v}`).join('\n'));
        setUrl(editingServer.url);
        setEnabled(editingServer.enabled);
      } else {
        setName(''); setTransport('stdio'); setCommand('');
        setArgsText(''); setEnvText(''); setUrl(''); setEnabled(true);
      }
    }
  }, [isOpen, editingServer]);

  const handleSubmit = () => {
    if (!name.trim()) return;
    onSave({
      name,
      transport,
      command,
      args: argsText.split('\n').map(s => s.trim()).filter(Boolean),
      env: Object.fromEntries(
        envText.split('\n').map(s => s.trim()).filter(Boolean)
          .map(line => { const i = line.indexOf('='); return i > 0 ? [line.slice(0, i), line.slice(i + 1)] : null; })
          .filter((x): x is [string, string] => x !== null)
      ),
      url,
      enabled,
    });
  };

  return (
    <Modal>
      <Modal.Backdrop isOpen={isOpen} onOpenChange={(open) => { if (!open) onClose(); }}>
        <Modal.Container size="lg">
          <Modal.Dialog>
            <Modal.Header>
              <Modal.Heading>{editingServer ? t('mcp.editServer') : t('mcp.addServer')}</Modal.Heading>
            </Modal.Header>
            <Modal.Body>
              <div className="space-y-4">
                <TextField value={name} onChange={setName} isDisabled={!!editingServer}>
                  <Label>{t('mcp.serverName')}</Label>
                  <Input placeholder="my-server" />
                </TextField>

                <div>
                  <Label>{t('mcp.transport')}</Label>
                  <div className="flex gap-2 mt-2">
                    <Button size="sm" variant={transport === 'stdio' ? 'primary' : 'secondary'} onPress={() => setTransport('stdio')}>
                      Stdio
                    </Button>
                    <Button size="sm" variant={transport === 'sse' ? 'primary' : 'secondary'} onPress={() => setTransport('sse')}>
                      Streamable HTTP
                    </Button>
                  </div>
                </div>

                {transport === 'stdio' ? (
                  <>
                    <TextField value={command} onChange={setCommand}>
                      <Label>{t('mcp.command')}</Label>
                      <br/>
                      <Input placeholder="npx" />
                    </TextField>
                    <div>
                      <Label>{t('mcp.args')}</Label>
                      <br/>
                      <TextArea
                        value={argsText}
                        fullWidth
                        onChange={(e) => setArgsText(e.target.value)}
                        placeholder={t('mcp.argsHint')}
                        rows={2}
                      />
                    </div>
                    <div>
                      <Label>{t('mcp.env')}</Label>
                      <br/>
                      <TextArea
                        value={envText}
                        fullWidth
                        onChange={(e) => setEnvText(e.target.value)}
                        placeholder={t('mcp.envHint')}
                        rows={2}
                      />
                    </div>
                  </>
                ) : (
                  <TextField value={url} onChange={setUrl}>
                    <Label>URL</Label>
                    <Input placeholder="http://localhost:3000/mcp" />
                  </TextField>
                )}

                <div>
                  <Label>{t('mcp.enabled')}</Label>
                  <div className="mt-2">
                    <Switch isSelected={enabled} onChange={setEnabled} />
                  </div>
                </div>
              </div>
            </Modal.Body>
            <Modal.Footer>
              <Button variant="secondary" onPress={onClose}>{t('common.cancel')}</Button>
              <Button onPress={handleSubmit}>{t('common.save')}</Button>
            </Modal.Footer>
          </Modal.Dialog>
        </Modal.Container>
      </Modal.Backdrop>
    </Modal>
  );
}
