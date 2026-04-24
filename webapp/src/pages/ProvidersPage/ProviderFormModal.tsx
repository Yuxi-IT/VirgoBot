import { useState, useEffect } from 'react';
import { Modal, Button, TextField, Label, Input, Select, ListBox, Spinner } from '@heroui/react';
import { PRESET_PROVIDERS } from './types';

interface Props {
  isOpen: boolean;
  editingProvider?: { name: string; apiKey: string; baseUrl: string; currentModel: string; models?: string[]; protocol: string } | null;
  onClose: () => void;
  onSave: (data: { name: string; apiKey: string; baseUrl: string; currentModel: string; protocol: string }) => void;
  saving: boolean;
}

export default function ProviderFormModal({ isOpen, editingProvider, onClose, onSave, saving }: Props) {
  const isEdit = !!editingProvider;
  const [name, setName] = useState('');
  const [apiKey, setApiKey] = useState('');
  const [baseUrl, setBaseUrl] = useState('');
  const [currentModel, setCurrentModel] = useState('');
  const [protocol, setProtocol] = useState('openai');

  const models = editingProvider?.models ?? [];

  useEffect(() => {
    if (isOpen) {
      setName(editingProvider?.name ?? '');
      setApiKey('');
      setBaseUrl(editingProvider?.baseUrl ?? '');
      setCurrentModel(editingProvider?.currentModel ?? '');
      setProtocol(editingProvider?.protocol ?? 'openai');
    }
  }, [isOpen, editingProvider]);

  const handlePresetChange = (key: string | null) => {
    if (!key) return;
    const found = PRESET_PROVIDERS.find(p => p.label === key);
    if (found) {
      setBaseUrl(found.baseUrl);
      setProtocol(found.protocol);
      if (!name) setName(found.label);
    }
  };

  const handleSubmit = () => {
    if (!name.trim()) return;
    onSave({ name: name.trim(), apiKey, baseUrl, currentModel, protocol });
  };

  return (
    <Modal.Backdrop isOpen={isOpen} onOpenChange={(open) => { if (!open) onClose(); }}>
      <Modal.Container>
        <Modal.Dialog>
          <Modal.Header>
            <Modal.Heading>{isEdit ? '编辑供应商' : '新建供应商'}</Modal.Heading>
          </Modal.Header>
          <Modal.Body>
            <div className="space-y-4">
              {!isEdit && (
                <div>
                  <Label>预设模板</Label>
                  <div className="mt-1">
                    <Select onSelectionChange={(key) => handlePresetChange(String(key))}>
                      <Select.Trigger>
                        <Select.Value />
                      </Select.Trigger>
                      <Select.Popover>
                        <ListBox>
                          {PRESET_PROVIDERS.map(p => (
                            <ListBox.Item key={p.label} id={p.label} textValue={p.label}>{p.label}</ListBox.Item>
                          ))}
                        </ListBox>
                      </Select.Popover>
                    </Select>
                  </div>
                </div>
              )}
              <TextField value={name} onChange={setName} isDisabled={isEdit} isRequired>
                <Label>名称</Label>
                <Input />
              </TextField>
              <TextField value={apiKey} onChange={setApiKey}>
                <Label>API Key</Label>
                <Input type="password" placeholder={isEdit ? '留空则不修改' : '输入 API Key'} />
              </TextField>
              <TextField value={baseUrl} onChange={setBaseUrl} isRequired>
                <Label>Base URL</Label>
                <Input />
              </TextField>
              {isEdit && models.length > 0 ? (
                <div>
                  <Label>模型</Label>
                  <div className="mt-1">
                    <Select selectedKey={currentModel} onSelectionChange={(key) => setCurrentModel(String(key))}>
                      <Select.Trigger>
                        <Select.Value />
                      </Select.Trigger>
                      <Select.Popover className="max-h-60">
                        <ListBox>
                          {models.map(m => (
                            <ListBox.Item key={m} id={m} textValue={m}>{m}</ListBox.Item>
                          ))}
                        </ListBox>
                      </Select.Popover>
                    </Select>
                  </div>
                </div>
              ) : (
                <TextField value={currentModel} onChange={setCurrentModel}>
                  <Label>模型</Label>
                  <Input placeholder="例如 gpt-4o" />
                </TextField>
              )}
              <div>
                <Label>协议</Label>
                <div className="mt-1">
                  <Select selectedKey={protocol} onSelectionChange={(key) => setProtocol(String(key))}>
                    <Select.Trigger>
                      <Select.Value />
                    </Select.Trigger>
                    <Select.Popover>
                      <ListBox>
                        <ListBox.Item id="openai" textValue="OpenAI">OpenAI</ListBox.Item>
                        <ListBox.Item id="anthropic" textValue="Anthropic">Anthropic</ListBox.Item>
                        <ListBox.Item id="gemini" textValue="Gemini">Gemini</ListBox.Item>
                      </ListBox>
                    </Select.Popover>
                  </Select>
                </div>
              </div>
            </div>
          </Modal.Body>
          <Modal.Footer>
            <Button variant="secondary" onPress={onClose} isDisabled={saving}>取消</Button>
            <Button onPress={handleSubmit} isDisabled={saving || !name.trim()}>
              {saving ? <Spinner size="sm" className="mr-2" /> : null}
              {isEdit ? '更新' : '创建'}
            </Button>
          </Modal.Footer>
        </Modal.Dialog>
      </Modal.Container>
    </Modal.Backdrop>
  );
}
