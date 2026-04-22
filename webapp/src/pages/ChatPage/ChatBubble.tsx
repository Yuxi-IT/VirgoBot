import { Dropdown, Disclosure, Label } from '@heroui/react';
import type { Message } from './types';

interface Props {
  message: Message;
  onDelete: (id: number) => void;
}

export default function ChatBubble({ message, onDelete }: Props) {
  const isUser = message.role === 'user';
  const isTool = message.role === 'tool';

  const renderContent = () => {
    if (isTool) {
      return (
        <Disclosure>
          <Disclosure.Heading>
            <Disclosure.Trigger className="text-xs text-default-400 cursor-pointer flex items-center gap-1">
              调用了工具
              <Disclosure.Indicator />
            </Disclosure.Trigger>
          </Disclosure.Heading>
          <Disclosure.Content>
            <Disclosure.Body>
              <pre className="mt-1 whitespace-pre-wrap break-all text-default-500 text-xs">{message.content}</pre>
            </Disclosure.Body>
          </Disclosure.Content>
        </Disclosure>
      );
    }

    const toolMatch = message.content.match(/\[tool: .+?\]/g);
    if (toolMatch && message.role === 'assistant') {
      const textPart = message.content.replace(/\[tool: .+?\]/g, '').trim();
      return (
        <>
          {textPart && <div className="whitespace-pre-wrap break-words">{textPart}</div>}
          <Disclosure>
            <Disclosure.Heading>
              <Disclosure.Trigger className="text-xs text-default-400 cursor-pointer flex items-center gap-1 mt-1">
                调用了 {toolMatch.length} 个工具
                <Disclosure.Indicator />
              </Disclosure.Trigger>
            </Disclosure.Heading>
            <Disclosure.Content>
              <Disclosure.Body>
                <div className="mt-1 text-default-500 text-xs">{toolMatch.join(', ')}</div>
              </Disclosure.Body>
            </Disclosure.Content>
          </Disclosure>
        </>
      );
    }

    return <div className="whitespace-pre-wrap break-words">{message.content}</div>;
  };

  const bubbleDiv = (
    <div
      className={`relative max-w-[80%] px-3 py-2 rounded-lg text-sm ${
        isUser
          ? 'bg-primary text-primary-foreground rounded-br-none'
          : isTool
            ? 'bg-default-100 rounded-bl-none'
            : 'bg-default-200 rounded-bl-none'
      }`}
    >
      {renderContent()}
      <div className={`text-[10px] mt-1 ${isUser ? 'text-primary-foreground/60' : 'text-default-400'}`}>
        {new Date(message.createdAt).toLocaleTimeString()}
      </div>
    </div>
  );

  return (
    <div className={`flex ${isUser ? 'justify-end' : 'justify-start'} mb-3`}>
      <Dropdown trigger="longPress">
        <Dropdown.Trigger>
          {bubbleDiv}
        </Dropdown.Trigger>
        <Dropdown.Popover>
          <Dropdown.Menu onAction={(key) => { if (key === 'delete') onDelete(message.id); }}>
            <Dropdown.Item id="delete" textValue="删除" variant="danger">
              <Label>删除</Label>
            </Dropdown.Item>
          </Dropdown.Menu>
        </Dropdown.Popover>
      </Dropdown>
    </div>
  );
}
