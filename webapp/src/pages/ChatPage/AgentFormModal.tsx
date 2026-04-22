import { useState } from 'react';
import { Modal, Button, TextField, Label, Input, TextArea, Spinner, toast } from '@heroui/react';
import { api } from '../../services/api';

interface Props {
  isOpen: boolean;
  onClose: () => void;
  onCreated: () => void;
}

export default function AgentFormModal({ isOpen, onClose, onCreated }: Props) {
  const [name, setName] = useState('');
  const [content, setContent] = useState('');
  const [characterName, setCharacterName] = useState('');
  const [creating, setCreating] = useState(false);
  const [generating, setGenerating] = useState(false);

  const handleCreate = async () => {
    if (!name.trim() || !content.trim()) return;
    try {
      setCreating(true);
      await api.post('/api/agents', { name: name.trim(), content });
      toast.success('设定已创建');
      setName(''); setContent(''); setCharacterName('');
      onCreated();
    } catch {
      toast.danger('创建失败');
    } finally {
      setCreating(false);
    }
  };

  const handleGenerate = async () => {
    if (!characterName.trim()) return;
    try {
      setGenerating(true);
      const agentName = name.trim() || characterName.trim();
      await api.post('/api/agents/generate', { characterName: characterName.trim(), agentName });
      toast.success('AI 生成设定成功');
      setName(''); setContent(''); setCharacterName('');
      onCreated();
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : String(e);
      if (msg.includes('409')) {
        toast.danger('设定已存在');
      } else {
        toast.danger('生成失败');
      }
    } finally {
      setGenerating(false);
    }
  };

  return (
    <Modal.Backdrop isOpen={isOpen} onOpenChange={(open) => { if (!open) onClose(); }}>
      <Modal.Container>
        <Modal.Dialog>
          <Modal.Header>
            <Modal.Heading>新建设定</Modal.Heading>
          </Modal.Header>
          <Modal.Body>
            <div className="space-y-4">
              <TextField value={name} onChange={setName} isRequired>
                <Label>名称</Label>
                <Input placeholder="设定名称" />
              </TextField>

              <div className="border rounded-lg p-3 bg-muted/30 space-y-2">
                <p className="text-sm font-medium">AI 生成</p>
                <div className="flex gap-2 items-end">
                  <div className="flex-1">
                    <TextField value={characterName} onChange={setCharacterName}>
                      <Label>角色名</Label>
                      <Input placeholder="输入角色名，AI 自动生成设定" />
                    </TextField>
                  </div>
                  <Button
                    size="sm"
                    variant="secondary"
                    onPress={handleGenerate}
                    isDisabled={generating || !characterName.trim()}
                  >
                    {generating ? <Spinner size="sm" className="mr-1" /> : null}
                    {generating ? '生成中...' : '生成'}
                  </Button>
                </div>
              </div>

              <div>
                <label className="text-sm font-medium mb-1 block">设定内容</label>
                <TextArea
                  value={content}
                  onChange={(e) => setContent(e.target.value)}
                  rows={8}
                  placeholder="手动输入设定内容..."
                  className="font-mono w-full"
                />
              </div>
            </div>
          </Modal.Body>
          <Modal.Footer>
            <Button variant="secondary" onPress={onClose}>取消</Button>
            <Button onPress={handleCreate} isDisabled={creating || !name.trim() || !content.trim()}>
              {creating ? <Spinner size="sm" className="mr-1" /> : null}
              创建
            </Button>
          </Modal.Footer>
        </Modal.Dialog>
      </Modal.Container>
    </Modal.Backdrop>
  );
}
