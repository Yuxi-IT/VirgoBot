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
  showTime: boolean;
  markdownEnabled: boolean;
  splitDelimiters: string;
  onSend: (text: string, images?: ImageAttachment[]) => void;
  onDeleteMessage: (id: number) => void;
  onLoadMore: () => void;
  onToggleVoiceFeedback: () => void;
  onToggleSplit: () => void;
  onToggleShowTime: () => void;
  onToggleMarkdown: () => void;
}

function splitMessage(text: string, delimiters: string): string[] {
  if (!delimiters) return [text];
  // Skip splitting if text contains multi-line code blocks
  if (/```[\s\S]*```/.test(text)) return [text];
  const parts = text.split(new RegExp(delimiters.split('|').map(d => d.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('|')));
  return parts.map(p => p.trim()).filter(Boolean);
}

export default function ChatPanel({
  messages, loading, sending, loadingMore, hasMore, voiceFeedback, splitEnabled, showTime, markdownEnabled, splitDelimiters,
  onSend, onDeleteMessage, onLoadMore, onToggleVoiceFeedback, onToggleSplit, onToggleShowTime, onToggleMarkdown
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

  // Track scroll position continuously; trigger load more when near top
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

  // After prepending older messages, restore scroll position so view doesn't jump
  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    const newCount = messages.length;
    const added = newCount - prevMsgCountRef.current;

    if (added > 0 && !wasAtBottomRef.current) {
      // Older messages were prepended — restore scroll position
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
      <div ref={scrollRef} className="flex-1 overflow-y-auto p-4">
        {loading && messages.length === 0 ? (
          <div className="flex items-center justify-center h-full">
            <Spinner size="lg" />
          </div>
        ) : messages.length === 0 ? (
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
                      showTime={showTime}
                      markdownEnabled={markdownEnabled}
                    />
                  ));
                }
              }
              return <ChatBubble key={msg.id} message={msg} onDelete={onDeleteMessage} showTime={showTime} markdownEnabled={markdownEnabled} />;
            })}
          </>
        )}
      </div>

      <ChatInput
        sending={sending}
        voiceFeedback={voiceFeedback}
        splitEnabled={splitEnabled}
        showTime={showTime}
        markdownEnabled={markdownEnabled}
        onSend={onSend}
        onToggleVoiceFeedback={onToggleVoiceFeedback}
        onToggleSplit={onToggleSplit}
        onToggleShowTime={onToggleShowTime}
        onToggleMarkdown={onToggleMarkdown}
      />
    </div>
  );
}
