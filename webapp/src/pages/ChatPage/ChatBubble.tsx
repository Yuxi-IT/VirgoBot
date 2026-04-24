import { useState, useEffect, useRef } from 'react';
import { Disclosure, Surface, ListBox } from '@heroui/react';
import { useI18n } from '../../i18n';
import type { Message } from './types';

interface Props {
  message: Message;
  onDelete: (id: number) => void;
  showTime: boolean;
}

export default function ChatBubble({ message, onDelete, showTime }: Props) {
  const { t } = useI18n();
  const isUser = message.role === 'user';
  const isTool = message.role === 'tool';

  const [menu, setMenu] = useState<{ x: number; y: number } | null>(null);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!menu) return;
    const close = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) setMenu(null);
    };
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, [menu]);

  const handleContextMenu = (e: React.MouseEvent) => {
    e.preventDefault();
    setMenu({ x: e.clientX, y: e.clientY });
  };

  const handleAction = (keys: Set<string>) => {
    const key = [...keys][0];
    if (key === 'copy') navigator.clipboard.writeText(message.content).catch(() => {});
    if (key === 'delete') onDelete(message.id);
    setMenu(null);
  };

  const decodeUnicode = (s: string) =>
    s.replace(/\\u([0-9a-fA-F]{4})/g, (_, hex: string) => String.fromCharCode(parseInt(hex, 16)));

  const renderContent = () => {
    if (isTool) {
      return (
        <Disclosure>
          <Disclosure.Heading>
            <Disclosure.Trigger className="text-default-500 min-w-[130px] cursor-pointer flex gap-1">
              {t('chatPage.calledTool')}
            </Disclosure.Trigger>
          </Disclosure.Heading>
          <Disclosure.Content>
            <Disclosure.Body>
              <pre className="whitespace-pre-wrap break-all text-xs text-default-600">{decodeUnicode(message.content)}</pre>
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
              <Disclosure.Trigger className="text-default-500 cursor-pointer flex gap-1 min-w-[170px] w-auto mt-1">
                {t('chatPage.calledNTools').replace('{n}', String(toolMatch.length))}
              </Disclosure.Trigger>
            </Disclosure.Heading>
            <Disclosure.Content>
              <Disclosure.Body>
                <div className="mt-1 text-default-600 text-xs">{toolMatch.join(', ')}</div>
              </Disclosure.Body>
            </Disclosure.Content>
          </Disclosure>
        </>
      );
    }

    // Detect error messages from assistant
    if (!isUser && !isTool && message.content.startsWith('错误:')) {
      const isReasoningError = message.content.includes('reasoning_content');
      return (
        <div>
          <div className="whitespace-pre-wrap break-words text-danger">{message.content}</div>
          {isReasoningError && (
            <div className="mt-1 text-xs text-default-500">
              该错误通常由旧会话的历史消息格式不兼容导致，请新建会话后重试。
            </div>
          )}
        </div>
      );
    }

    return <div className="whitespace-pre-wrap break-words">{isUser ? message.content.split('\n').slice(0, -1).join('\n') : message.content}</div>;
  };

  return (
    <div className={`flex mb-3 ${isUser ? 'justify-end' : 'justify-start'}`}>
      <div
        className={`relative max-w-[80%] rounded-2xl px-3 py-2 text-sm text-left ${
          isUser
            ? 'bg-sky-700/30 rounded-br-none'
            : isTool
              ? 'bg-default-200 border border-default-300 rounded-bl-none'
              : 'bg-content2 shadow-sky-200/10 shadow-sm rounded-bl-none'
        }`}
        onContextMenu={handleContextMenu}
      >
        {renderContent()}
        {showTime && (
          <div className={`text-[10px] mt-1 ${isUser ? 'opacity-60' : 'text-default-400'}`}>
            {new Date(message.createdAt).toLocaleTimeString()}
          </div>
        )}
      </div>

      {menu && (
        <div
          ref={menuRef}
          className="fixed z-50"
          style={{ left: menu.x, top: menu.y }}
        >
          <Surface className="w-[160px] rounded-2xl shadow-surface">
            <ListBox
              aria-label="actions"
              selectionMode="single"
              className='text-[14px]'
              onSelectionChange={(keys) => handleAction(keys as Set<string>)}
            >
              <ListBox.Item id="copy" textValue={t('chatPage.copy')}>
                {t('chatPage.copy')}
              </ListBox.Item>
              <ListBox.Item id="delete" textValue={t('common.delete')}>
                {t('common.delete')}
              </ListBox.Item>
            </ListBox>
          </Surface>
        </div>
      )}
    </div>
  );
}
