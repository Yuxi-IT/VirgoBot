import { useState } from 'react';
import type { Message } from './types';

interface Props {
  message: Message;
  onDelete: (id: number) => void;
}

export default function ChatBubble({ message, onDelete }: Props) {
  const [showMenu, setShowMenu] = useState(false);
  const isUser = message.role === 'user';
  const isTool = message.role === 'tool';

  const handleContextMenu = (e: React.MouseEvent) => {
    e.preventDefault();
    setShowMenu(true);
    setTimeout(() => setShowMenu(false), 3000);
  };

  // Parse tool calls from content
  const renderContent = () => {
    if (isTool) {
      return (
        <details className="text-xs">
          <summary className="cursor-pointer text-default-400">调用了工具</summary>
          <pre className="mt-1 whitespace-pre-wrap break-all text-default-500">{message.content}</pre>
        </details>
      );
    }

    // Check if content contains tool_use markers
    const toolMatch = message.content.match(/\[tool: .+?\]/g);
    if (toolMatch && message.role === 'assistant') {
      const textPart = message.content.replace(/\[tool: .+?\]/g, '').trim();
      return (
        <>
          {textPart && <div className="whitespace-pre-wrap break-words">{textPart}</div>}
          <details className="text-xs mt-1">
            <summary className="cursor-pointer text-default-400">调用了 {toolMatch.length} 个工具</summary>
            <div className="mt-1 text-default-500">{toolMatch.join(', ')}</div>
          </details>
        </>
      );
    }

    return <div className="whitespace-pre-wrap break-words">{message.content}</div>;
  };

  return (
    <div className={`flex ${isUser ? 'justify-end' : 'justify-start'} mb-3`}>
      <div
        className={`relative max-w-[80%] px-3 py-2 rounded-lg text-sm ${
          isUser
            ? 'bg-primary text-primary-foreground rounded-br-none'
            : isTool
              ? 'bg-default-100 rounded-bl-none'
              : 'bg-default-200 rounded-bl-none'
        }`}
        onContextMenu={handleContextMenu}
      >
        {renderContent()}
        <div className={`text-[10px] mt-1 ${isUser ? 'text-primary-foreground/60' : 'text-default-400'}`}>
          {new Date(message.createdAt).toLocaleTimeString()}
        </div>

        {showMenu && (
          <div
            className="absolute top-0 right-0 -mt-8 bg-background border rounded shadow-lg z-10"
          >
            <button
              className="px-3 py-1 text-xs text-danger hover:bg-danger/10 rounded"
              onClick={() => { onDelete(message.id); setShowMenu(false); }}
            >
              删除
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
