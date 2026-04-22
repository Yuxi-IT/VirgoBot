import { useEffect, useRef, useCallback } from 'react';
import { Spinner, Button } from '@heroui/react';
import { ChevronLeft, ChevronRight } from '@gravity-ui/icons';
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
  onSend: (text: string) => void;
  onDeleteMessage: (id: number) => void;
  onPageChange: (page: number) => void;
  onToggleVoiceFeedback: () => void;
}

export default function ChatPanel({
  messages, loading, sending, page, totalPages, voiceFeedback,
  onSend, onDeleteMessage, onPageChange, onToggleVoiceFeedback
}: Props) {
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

  // Only auto-scroll when new messages arrive AND user was already at bottom
  useEffect(() => {
    const newCount = messages.length;
    const hasNew = newCount > prevMsgCountRef.current;
    prevMsgCountRef.current = newCount;

    if (hasNew && wasAtBottomRef.current && scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [messages]);

  return (
    <div className="flex flex-col h-full">
      <div ref={scrollRef} className="flex-1 overflow-y-auto p-4">
        {loading && messages.length === 0 ? (
          <div className="flex items-center justify-center h-full">
            <Spinner size="lg" />
          </div>
        ) : messages.length === 0 ? (
          <div className="flex items-center justify-center h-full text-default-400">
            开始对话吧
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
            {messages.map(msg => (
              <ChatBubble key={msg.id} message={msg} onDelete={onDeleteMessage} />
            ))}
          </>
        )}
      </div>

      {/* Input area */}
      <ChatInput
        sending={sending}
        voiceFeedback={voiceFeedback}
        onSend={onSend}
        onToggleVoiceFeedback={onToggleVoiceFeedback}
      />
    </div>
  );
}
