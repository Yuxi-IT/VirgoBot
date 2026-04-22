import { useState, useEffect, useRef, useCallback } from 'react';
import { Button, Spinner } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { api, BASE_URL } from '../../services/api';
import SessionList from './SessionList';
import ChatPanel from './ChatPanel';
import AgentPanel from './AgentPanel';
import type { SessionInfo, SessionsResponse, Message, MessagesResponse } from './types';
import { ArrowLeft, ArrowRight } from '@gravity-ui/icons';

const PAGE_SIZE = 50;

function ChatPage() {
  const [sessions, setSessions] = useState<SessionInfo[]>([]);
  const [currentSession, setCurrentSession] = useState('');
  const [messages, setMessages] = useState<Message[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [msgLoading, setMsgLoading] = useState(false);
  const [sending, setSending] = useState(false);
  const [voiceFeedback, setVoiceFeedback] = useState(false);
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const [splitDelimiters, setSplitDelimiters] = useState('。|！|？|?|\n\n|\n');

  const wsRef = useRef<WebSocket | null>(null);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const currentSessionRef = useRef(currentSession);
  const loadMessagesRef = useRef<(silent?: boolean) => Promise<void>>(async () => {});
  currentSessionRef.current = currentSession;

  const loadSessions = useCallback(async (silent = false) => {
    try {
      if (!silent) setLoading(true);
      const res = await api.get<SessionsResponse>('/api/sessions');
      if (res.success) {
        setSessions(res.data as unknown as SessionInfo[]);
        const cur = (res.data as unknown as SessionInfo[]).find(s => s.isCurrent);
        if (cur) setCurrentSession(cur.fileName);
      }
    } catch { /* silent */ } finally { if (!silent) setLoading(false); }
  }, []);

  const loadMessages = useCallback(async (silent = false) => {
    try {
      if (!silent) setMsgLoading(true);
      const offset = (page - 1) * PAGE_SIZE;
      const res = await api.get<MessagesResponse>(`/api/messages?limit=${PAGE_SIZE}&offset=${offset}`);
      if (res.success) {
        setMessages(res.data.messages);
        setTotal(res.data.total);
      }
    } catch { /* silent */ } finally { if (!silent) setMsgLoading(false); }
  }, [page]);
  loadMessagesRef.current = loadMessages;

  useEffect(() => {
    loadSessions();
    api.get<{ success: boolean; data: { server: { messageSplitDelimiters: string } } }>('/api/config')
      .then(res => { if (res.success) setSplitDelimiters(res.data.server.messageSplitDelimiters); })
      .catch(() => {});
  }, [loadSessions]);

  useEffect(() => {
    loadMessages();
  }, [loadMessages, currentSession]);

  // Auto-refresh messages
  useEffect(() => {
    intervalRef.current = setInterval(() => {
      loadMessages(true);
    }, 2000);
    return () => { if (intervalRef.current) clearInterval(intervalRef.current); };
  }, [loadMessages]);

  // WebSocket connection
  useEffect(() => {
    const wsUrl = BASE_URL.replace(/^http/, 'ws') + '/';
    const ws = new WebSocket(wsUrl);
    ws.onmessage = (event) => {
      try {
        const data = JSON.parse(event.data);
        if (data.type === 'sendMessage' && data.content) {
          loadMessagesRef.current(true);
          if (voiceFeedback) {
            convertTTS(data.content);
          }
        }
      } catch { /* ignore */ }
    };
    wsRef.current = ws;
    return () => { ws.close(); };
  }, [voiceFeedback]);

  const sendMessage = async (text: string) => {
    if (!text.trim() || sending) return;
    setSending(true);
    try {
      if (wsRef.current?.readyState === WebSocket.OPEN) {
        wsRef.current.send(JSON.stringify({ type: 'message', message: text }));
        // Optimistically add user message
        const optimisticMsg: Message = {
          id: Date.now(),
          role: 'user',
          content: text,
          createdAt: new Date().toISOString(),
        };
        setMessages(prev => [...prev, optimisticMsg]);
        // Generate session name on first message
        const cur = sessions.find(s => s.isCurrent);
        if (cur && !cur.sessionName && cur.messageCount === 0) {
          setTimeout(async () => {
            try {
              await api.post('/api/sessions/generate-name', { message: text });
              loadSessions(true);
            } catch { /* ignore */ }
          }, 1000);
        }
      }
    } finally {
      setTimeout(() => {
        setSending(false);
        loadMessages(true);
      }, 1000);
    }
  };

  const deleteMessage = async (id: number) => {
    try {
      await api.del(`/api/messages/${id}`);
      loadMessages(true);
    } catch { /* ignore */ }
  };

  const switchSession = async (fileName: string) => {
    try {
      await api.put('/api/sessions/switch', { session: fileName });
      setCurrentSession(fileName);
      setPage(1);
      loadSessions(true);
      loadMessages();
    } catch { /* ignore */ }
  };

  const createSession = async () => {
    try {
      const res = await api.post<{ success: boolean; data: { fileName: string } }>('/api/sessions', {});
      if (res.success) {
        await switchSession(res.data.fileName);
      }
    } catch { /* ignore */ }
  };

  const convertTTS = async (text: string) => {
    try {
      const res = await api.post<{ success: boolean; data: { audioBase64: string } }>('/api/voice/tts', { text });
      if (res.success && res.data.audioBase64) {
        const audioData = atob(res.data.audioBase64);
        const buf = new ArrayBuffer(audioData.length);
        const view = new Uint8Array(buf);
        for (let i = 0; i < audioData.length; i++) view[i] = audioData.charCodeAt(i);
        const blob = new Blob([buf], { type: 'audio/mpeg' });
        const audio = new Audio(URL.createObjectURL(blob));
        audio.play();
      }
    } catch { /* ignore */ }
  };

  if (loading) {
    return (
      <DefaultLayout>
        <div className="flex items-center justify-center h-[60vh]"><Spinner size="lg" /></div>
      </DefaultLayout>
    );
  }

  const totalPages = Math.ceil(total / PAGE_SIZE);

  return (
    <DefaultLayout noPadding>
      <div className="flex h-[calc(100vh-44px)] sm:h-screen overflow-hidden">
        {/* Left: Session List */}
        <div
          className="shrink-0 border-r overflow-y-auto hidden sm:block transition-[width] duration-300 ease-in-out"
          style={{ width: sidebarOpen ? 256 : 0, overflow: sidebarOpen ? undefined : 'hidden' }}
        >
          <div className="w-64">
            <SessionList
              sessions={sessions}
              currentSession={currentSession}
              onSwitch={switchSession}
              onCreate={createSession}
              onReload={() => loadSessions(true)}
            />
          </div>
        </div>

        {/* Center: Chat Panel */}
        <div className="flex-1 flex flex-col min-w-0 relative">
          <Button
            onClick={() => setSidebarOpen(v => !v)}
            className="hidden sm:flex absolute -left-4 top-1/2 -translate-y-1/2 z-20"
            variant='tertiary'
            size='lg'
            isIconOnly
          >
            {sidebarOpen ? <ArrowRight /> : <ArrowLeft />}
          </Button>
          <ChatPanel
            messages={messages}
            loading={msgLoading}
            sending={sending}
            page={page}
            totalPages={totalPages}
            voiceFeedback={voiceFeedback}
            splitDelimiters={splitDelimiters}
            onSend={sendMessage}
            onDeleteMessage={deleteMessage}
            onPageChange={setPage}
            onToggleVoiceFeedback={() => setVoiceFeedback(v => !v)}
          />
        </div>

        {/* Right: Agent Panel */}
        <div className="w-72 shrink-0 border-l overflow-y-auto hidden md:block">
          <AgentPanel />
        </div>
      </div>
    </DefaultLayout>
  );
}

export default ChatPage;
