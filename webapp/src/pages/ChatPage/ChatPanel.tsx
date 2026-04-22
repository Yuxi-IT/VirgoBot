import { useEffect, useRef, useCallback } from 'react';
import { Spinner, Button } from '@heroui/react';
import { ChevronLeft, ChevronRight } from '@gravity-ui/icons';
import { useI18n } from '../../i18n';
import ChatBubble from './ChatBubble';
import ChatInput from './ChatInput';
import type { Message } from './types';

interface Props {
  messages: Message[];
  loading: boolean;
  sending: boolean;
  page: number;
  totalPages: number;
  voiceFeedback: boolean;
  splitEnabled: boolean;
  showTime: boolean;
  splitDelimiters: string;
  onSend: (text: string) => void;
  onDeleteMessage: (id: number) => void;
  onPageChange: (page: number) => void;
  onToggleVoiceFeedback: () => void;
  onToggleSplit: () => void;
  onToggleShowTime: () => void;
}

function splitMessage(text: string, delimiters: string): string[] {
  if (!delimiters) return [text];
  const parts = text.split(new RegExp(delimiters.split('|').map(d => d.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('|')));
  return parts.map(p => p.trim()).filter(Boolean);
}

export default function ChatPanel({
  messages, loading, sending, page, totalPages, voiceFeedback, splitEnabled, showTime, splitDelimiters,
  onSend, onDeleteMessage, onPageChange, onToggleVoiceFeedback, onToggleSplit, onToggleShowTime
}: Props) {
  const { t } = useI18n();
  const scrollRef = useRef<HTMLDivElement>(null);
  const wasAtBottomRef = useRef(true);
  const prevMsgCountRef = useRef(0);

  const isAtBottom = useCallback(() => {
    const el = scrollRef.current;
    if (!el) return true;
    return el.scrollHeight - el.scrollTop - el.clientHeight < 40;
  }, []);

  // Track scroll position continuously
  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    const onScroll = () => { wasAtBottomRef.current = isAtBottom(); };
    el.addEventListener('scroll', onScroll, { passive: true });
    return () => el.removeEventListener('scroll', onScroll);
  }, [isAtBottom]);

  useEffect(() => {
    const newCount = messages.length;
    const hasNew = newCount > prevMsgCountRef.current;
    prevMsgCountRef.current = newCount;

    if (hasNew && wasAtBottomRef.current && scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
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
            {totalPages > 1 && (
              <div className="flex justify-center items-center gap-2 mb-4">
                <Button size="sm" variant="ghost" isDisabled={page <= 1} onPress={() => onPageChange(page - 1)}>
                  <ChevronLeft />
                </Button>
                <span className="text-sm text-default-500">{page} / {totalPages}</span>
                <Button size="sm" variant="ghost" isDisabled={page >= totalPages} onPress={() => onPageChange(page + 1)}>
                  <ChevronRight />
                </Button>
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
                    />
                  ));
                }
              }
              return <ChatBubble key={msg.id} message={msg} onDelete={onDeleteMessage} showTime={showTime} />;
            })}
          </>
        )}
      </div>

      <ChatInput
        sending={sending}
        voiceFeedback={voiceFeedback}
        splitEnabled={splitEnabled}
        showTime={showTime}
        onSend={onSend}
        onToggleVoiceFeedback={onToggleVoiceFeedback}
        onToggleSplit={onToggleSplit}
        onToggleShowTime={onToggleShowTime}
      />
    </div>
  );
}

