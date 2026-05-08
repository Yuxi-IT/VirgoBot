import { useState, useEffect, useRef, useCallback } from 'react';
import { Button, Spinner, Tabs } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { api, BASE_URL } from '../../services/api';
import SessionList from './SessionList';
import ChatPanel from './ChatPanel';
import AgentPanel from './AgentPanel';
import SoulPanel from './SoulPanel';
import type { SessionInfo, SessionsResponse, Message, MessagesResponse } from './types';
import type { ImageAttachment } from './ChatInput';
import { ArrowLeft, ArrowRight } from '@gravity-ui/icons';
import { useI18n } from '../../i18n';

const PAGE_SIZE = 20;

function readFlag(key: string, defaultVal: boolean): boolean {
  try {
    const v = localStorage.getItem(key);
    if (v === null) return defaultVal;
    return v === 'true';
  } catch { return defaultVal; }
}

function ChatPage() {
  const { t } = useI18n();
  const [accessKey, setAccessKey] = useState('');
  const [sessions, setSessions] = useState<SessionInfo[]>([]);
  const [currentSession, setCurrentSession] = useState('');
  const [messages, setMessages] = useState<Message[]>([]);
  const [offset, setOffset] = useState(0);
  const [hasMore, setHasMore] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [loading, setLoading] = useState(true);
  const [msgLoading, setMsgLoading] = useState(false);
  const [sending, setSending] = useState(false);
  const [voiceFeedback, setVoiceFeedback] = useState(() => readFlag('chat.voiceFeedback', false));
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [splitDelimiters, setSplitDelimiters] = useState('。|！|？|?|\n\n|\n');
  const [splitEnabled, setSplitEnabled] = useState(() => readFlag('chat.splitEnabled', true));
  const [markdownEnabled, setMarkdownEnabled] = useState(() => readFlag('chat.markdownEnabled', true));
  const [activeTab, setActiveTab] = useState('chat');
  const [pendingNew, setPendingNew] = useState(false);

  const toggleFlag = (key: string, setter: React.Dispatch<React.SetStateAction<boolean>>) => {
    setter(v => {
      const next = !v;
      try { localStorage.setItem(key, String(next)); } catch { /* ignore */ }
      return next;
    });
  };

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
        if (!pendingNew) {
          const cur = (res.data as unknown as SessionInfo[]).find(s => s.isCurrent);
          if (cur) setCurrentSession(cur.fileName);
        }
      }
    } catch { /* silent */ } finally { if (!silent) setLoading(false); }
  }, [pendingNew]);

  const loadMessages = useCallback(async (silent = false) => {
    if (currentSession === '__new__') return;
    try {
      if (!silent) setMsgLoading(true);
      const res = await api.get<MessagesResponse>(`/api/messages?limit=${PAGE_SIZE}&offset=0`);
      if (res.success) {
        if (silent) {
          // Merge: preserve older messages loaded via loadMore, only update latest
          setMessages(prev => {
            const latestIds = new Set(res.data.messages.map((m: Message) => m.id));
            const minLatestId = res.data.messages.length > 0
              ? Math.min(...res.data.messages.map((m: Message) => m.id))
              : Infinity;
            const historical = prev.filter(m => !latestIds.has(m.id) && m.id < minLatestId);
            return [...historical, ...res.data.messages];
          });
        } else {
          setMessages(res.data.messages);
          setOffset(res.data.messages.length);
          setHasMore(res.data.messages.length < res.data.total);
        }
      }
    } catch { /* silent */ } finally { if (!silent) setMsgLoading(false); }
  }, [currentSession]);
  loadMessagesRef.current = loadMessages;

  const loadMoreMessages = useCallback(async () => {
    if (loadingMore || !hasMore) return;
    try {
      setLoadingMore(true);
      const res = await api.get<MessagesResponse>(`/api/messages?limit=${PAGE_SIZE}&offset=${offset}`);
      if (res.success) {
        const newOffset = offset + res.data.messages.length;
        setMessages(prev => [...res.data.messages, ...prev]);
        setOffset(newOffset);
        setHasMore(newOffset < res.data.total);
      }
    } catch { /* silent */ } finally { setLoadingMore(false); }
  }, [offset, hasMore, loadingMore]);

  useEffect(() => {
    loadSessions();
    api.get<{ success: boolean; data: { server: { messageSplitDelimiters: string } } }>('/api/config')
      .then(res => { if (res.success) setSplitDelimiters(res.data.server.messageSplitDelimiters); })
      .catch(() => {});
    // Pick an enabled AccessKey for WebSocket (voice feedback only)
    api.get<{ success: boolean; data: { key: string; enabled: boolean }[] }>('/api/access-keys')
      .then(res => {
        if (res.success) {
          const enabled = res.data.filter(k => k.enabled);
          if (enabled.length > 0) {
            setAccessKey(enabled[Math.floor(Math.random() * enabled.length)].key);
          }
        }
      })
      .catch(() => {});
  }, [loadSessions]);

  useEffect(() => {
    loadMessages();
  }, [loadMessages]);

  // Auto-refresh messages
  useEffect(() => {
    intervalRef.current = setInterval(() => {
      loadMessages(true);
    }, 2000);
    return () => { if (intervalRef.current) clearInterval(intervalRef.current); };
  }, [loadMessages]);

  // WebSocket connection (wait for accessKey to be available)
  useEffect(() => {
    if (!accessKey) return;
    const wsUrl = BASE_URL.replace(/^http/, 'ws') + '/?key=' + encodeURIComponent(accessKey);
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
  }, [voiceFeedback, accessKey]);

  const abortChat = async () => {
    try {
      await api.post('/api/chat/abort', {});
    } catch { /* ignore */ }
  };

  const sendMessage = async (text: string, images?: ImageAttachment[]) => {
    if (!text.trim() && (!images || images.length === 0)) return;
    if (sending) return;

    // Lazy session creation: create the session on first message
    if (pendingNew) {
      try {
        const res = await api.post<{ success: boolean; data: { fileName: string } }>('/api/sessions', {});
        if (!res.success) return;
        await api.put('/api/sessions/switch', { session: res.data.fileName });
        setCurrentSession(res.data.fileName);
        setPendingNew(false);
        loadSessions(true);
      } catch { return; }
    }

    setSending(true);

    // Optimistic user message
    const optimisticContent = images && images.length > 0
      ? JSON.stringify({ text, images: images.map(i => ({ preview: i.preview })) })
      : text;
    const optimisticId = Date.now();
    const optimisticMsg: Message = {
      id: optimisticId,
      role: 'user',
      content: optimisticContent,
      createdAt: new Date().toISOString(),
    };
    setMessages(prev => [...prev, optimisticMsg]);

    try {
      const payload: Record<string, unknown> = { message: text };
      if (images && images.length > 0) {
        const urlImages = images.filter(i => i.type === 'url').map(i => i.data);
        const b64Images = images.filter(i => i.type === 'base64').map(i => ({ data: i.data, mediaType: i.mediaType ?? 'image/jpeg' }));
        if (urlImages.length > 0) payload.imageUrls = urlImages;
        if (b64Images.length > 0) payload.imageBase64 = b64Images;
      }
      await api.post('/chat', payload);

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
    } finally {
      setSending(false);
      // Remove optimistic message before refresh so the real one from server replaces it
      setMessages(prev => prev.filter(m => m.id !== optimisticId));
      loadMessages(true);
    }
  };

  const deleteMessage = async (id: number) => {
    // Optimistically remove to prevent flicker
    setMessages(prev => prev.filter(m => m.id !== id));
    try {
      await api.del(`/api/messages/${id}`);
      // Sync state to update total count etc.
      loadMessages(true);
    } catch {
      // Full reload on failure to restore correct state
      loadMessages(false);
    }
  };

  const switchSession = async (fileName: string) => {
    setPendingNew(false);
    try {
      await api.put('/api/sessions/switch', { session: fileName });
      setCurrentSession(fileName);
      setOffset(0);
      setHasMore(false);
      loadSessions(true);
      loadMessages();
    } catch { /* ignore */ }
  };

  const createSession = async () => {
    // Lazy: don't create now, wait for first message
    setPendingNew(true);
    setCurrentSession('__new__');
    setMessages([]);
    setOffset(0);
    setHasMore(false);
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

  return (
    <DefaultLayout noPadding>
      <div className="flex h-[calc(100vh-44px)] sm:h-screen overflow-hidden">
        {/* Left: Session List */}
        <div
          className="shrink-0 border-r overflow-hidden hidden sm:block transition-[width] duration-300 ease-in-out"
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
        {/* Center: Chat + Soul Tabs */}
        <div className="flex-1 flex flex-col min-w-0 relative">
          <Button
            onClick={() => setSidebarOpen(v => !v)}
            className="hidden sm:flex absolute -left-4 top-1/2 -translate-y-1/2 z-20"
            variant='tertiary'
            size='lg'
            isIconOnly
          >
            {sidebarOpen ? <ArrowLeft /> : <ArrowRight />}
          </Button>

          <Tabs selectedKey={activeTab} onSelectionChange={(key) => setActiveTab(String(key))} className="flex-1 min-h-0 flex flex-col">
            <Tabs.ListContainer className="px-3 pt-1">
              <Tabs.List aria-label="Chat tabs">
                <Tabs.Tab id="chat">
                  {t('chatPage.tabChat')}
                  <Tabs.Indicator />
                </Tabs.Tab>
                <Tabs.Tab id="soul">
                  {t('chatPage.tabSoul')}
                  <Tabs.Indicator />
                </Tabs.Tab>
              </Tabs.List>
            </Tabs.ListContainer>

            <Tabs.Panel id="chat" className="flex-1 min-h-0">
              <ChatPanel
                messages={messages}
                loading={msgLoading}
                sending={sending}
                loadingMore={loadingMore}
                hasMore={hasMore}
                voiceFeedback={voiceFeedback}
                splitEnabled={splitEnabled}
                markdownEnabled={markdownEnabled}
                splitDelimiters={splitDelimiters}
                onSend={(text, imgs) => sendMessage(text, imgs)}
                onDeleteMessage={deleteMessage}
                onLoadMore={loadMoreMessages}
                onAbort={abortChat}
                onToggleVoiceFeedback={() => toggleFlag('chat.voiceFeedback', setVoiceFeedback)}
                onToggleSplit={() => toggleFlag('chat.splitEnabled', setSplitEnabled)}
                onToggleMarkdown={() => toggleFlag('chat.markdownEnabled', setMarkdownEnabled)}
              />
            </Tabs.Panel>

            <Tabs.Panel id="soul" className="flex-1 min-h-0">
              <SoulPanel />
            </Tabs.Panel>
          </Tabs>
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
