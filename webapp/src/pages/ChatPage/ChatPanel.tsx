import { useEffect, useRef, useCallback } from 'react';
import { Spinner } from '@heroui/react';
import { useI18n } from '../../i18n';
import ChatBubble from './ChatBubble';
import ChatInput, { type ImageAttachment } from './ChatInput';
import type { Message } from './types';

interface Props {
  messages: Message[];
  loading: boolean;
  sending: boolean;
  loadingMore: boolean;
  hasMore: boolean;
  voiceFeedback: boolean;
  splitEnabled: boolean;
  markdownEnabled: boolean;
  splitDelimiters: string;
  onSend: (text: string, images?: ImageAttachment[]) => void;
  onDeleteMessage: (id: number) => void;
  onLoadMore: () => void;
  onAbort: () => void;
  onToggleVoiceFeedback: () => void;
  onToggleSplit: () => void;
  onToggleMarkdown: () => void;
}

function splitMessage(text: string, delimiters: string): string[] {
  if (!delimiters) return [text];
  if (/```[\s\S]*```/.test(text)) return [text];
  const parts = text.split(new RegExp(delimiters.split('|').map(d => d.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('|')));
  return parts.map(p => p.trim()).filter(Boolean);
}

export default function ChatPanel({
  messages, loading, sending, loadingMore, hasMore, voiceFeedback, splitEnabled, markdownEnabled, splitDelimiters,
  onSend, onDeleteMessage, onLoadMore, onAbort, onToggleVoiceFeedback, onToggleSplit, onToggleMarkdown
}: Props) {
  const { t } = useI18n();
  const scrollRef = useRef<HTMLDivElement>(null);
  const wasAtBottomRef = useRef(true);
  const prevMsgCountRef = useRef(0);
  const prevScrollHeightRef = useRef(0);

  const isAtBottom = useCallback(() => {
    const el = scrollRef.current;
    if (!el) return true;
    return el.scrollHeight - el.scrollTop - el.clientHeight < 40;
  }, []);

  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    const onScroll = () => {
      wasAtBottomRef.current = isAtBottom();
      if (el.scrollTop < 80 && hasMore && !loadingMore) {
        onLoadMore();
      }
    };
    el.addEventListener('scroll', onScroll, { passive: true });
    return () => el.removeEventListener('scroll', onScroll);
  }, [isAtBottom, hasMore, loadingMore, onLoadMore]);

  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    const newCount = messages.length;
    const added = newCount - prevMsgCountRef.current;

    if (added > 0 && !wasAtBottomRef.current) {
      const diff = el.scrollHeight - prevScrollHeightRef.current;
      el.scrollTop += diff;
    } else if (added > 0 && wasAtBottomRef.current) {
      el.scrollTop = el.scrollHeight;
    }

    prevMsgCountRef.current = newCount;
    prevScrollHeightRef.current = el.scrollHeight;
  }, [messages]);

  return (
    <div className="flex flex-col h-full pb-2">
      <style>{`
        @keyframes thinking-bounce {
          0%, 80%, 100% { transform: scale(0.4); opacity: 0.3; }
          40% { transform: scale(1); opacity: 1; }
        }
        .thinking-dot {
          display: inline-block;
          width: 6px;
          height: 6px;
          border-radius: 50%;
          background-color: currentColor;
          animation: thinking-bounce 1.4s infinite ease-in-out both;
        }
        .thinking-dot:nth-child(1) { animation-delay: -0.32s; }
        .thinking-dot:nth-child(2) { animation-delay: -0.16s; }
        .thinking-dot:nth-child(3) { animation-delay: 0s; }
      `}</style>
      <div ref={scrollRef} className="flex-1 overflow-y-auto p-4">
        {loading && messages.length === 0 ? (
          <div className="flex items-center justify-center h-full">
            <Spinner size="lg" />
          </div>
        ) : messages.length === 0 && !sending ? (
          <div className="flex items-center justify-center h-full text-xl text-default-400">
            {t('chatPage.greeting')}
          </div>
        ) : (
          <>
            {loadingMore && (
              <div className="flex justify-center py-2">
                <Spinner size="sm" />
              </div>
            )}
            {messages.map(msg => {
              if (splitEnabled && msg.role === 'assistant' && splitDelimiters) {
                const parts = splitMessage(msg.content, splitDelimiters);
                if (parts.length > 1) {
                  return parts.map((part, i) => (
                    <ChatBubble
                      key={`${msg.id}-${i}`}
                      message={{ ...msg, content: part }}
                      onDelete={onDeleteMessage}
                      markdownEnabled={markdownEnabled}
                    />
                  ));
                }
              }
              return <ChatBubble key={msg.id} message={msg} onDelete={onDeleteMessage} markdownEnabled={markdownEnabled} />;
            })}
            {sending && (
              <div className="flex mb-3 justify-start">
                <div className="relative max-w-[80%] rounded-2xl rounded-bl-none px-4 py-3 text-sm bg-content2 shadow-sky-200/10 shadow-sm">
                  <span className="text-default-500 mr-1">{t('chatPage.thinking')}</span>
                  <span className="thinking-dot" />
                  <span className="thinking-dot mx-[3px]" />
                  <span className="thinking-dot" />
                </div>
              </div>
            )}
          </>
        )}
      </div>

      <ChatInput
        sending={sending}
        voiceFeedback={voiceFeedback}
        splitEnabled={splitEnabled}
        markdownEnabled={markdownEnabled}
        onSend={onSend}
        onAbort={onAbort}
        onToggleVoiceFeedback={onToggleVoiceFeedback}
        onToggleSplit={onToggleSplit}
        onToggleMarkdown={onToggleMarkdown}
      />
    </div>
  );
}
