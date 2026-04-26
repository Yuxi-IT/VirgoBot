import { useState, useEffect, useRef, useCallback } from 'react';
import { Button, Spinner, Tabs, Card } from '@heroui/react';
import { useNavigate } from 'react-router-dom';
import DefaultLayout from '../../layout/DefaultLayout';
import { api, BASE_URL } from '../../services/api';
import SessionList from './SessionList';
import ChatPanel from './ChatPanel';
import AgentPanel from './AgentPanel';
import SoulPanel from './SoulPanel';
import type { SessionInfo, SessionsResponse, Message, MessagesResponse } from './types';
import type { ImageAttachment } from './ChatInput';
import { ArrowLeft, ArrowRight, ShieldKeyhole } from '@gravity-ui/icons';
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
  const navigate = useNavigate();
  const [hasAccessKey, setHasAccessKey] = useState<boolean | null>(null);
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
  const [showTime, setShowTime] = useState(() => readFlag('chat.showTime', true));
  const [markdownEnabled, setMarkdownEnabled] = useState(() => readFlag('chat.markdownEnabled', true));
  const [activeTab, setActiveTab] = useState('chat');

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
        const cur = (res.data as unknown as SessionInfo[]).find(s => s.isCurrent);
        if (cur) setCurrentSession(cur.fileName);
      }
    } catch { /* silent */ } finally { if (!silent) setLoading(false); }
  }, []);

  const loadMessages = useCallback(async (silent = false) => {
    try {
      if (!silent) setMsgLoading(true);
      const res = await api.get<MessagesResponse>(`/api/messages?limit=${PAGE_SIZE}&offset=0`);
      if (res.success) {
        setMessages(res.data.messages);
        setOffset(res.data.messages.length);
        setHasMore(res.data.messages.length < res.data.total);
      }
    } catch { /* silent */ } finally { if (!silent) setMsgLoading(false); }
  }, []);
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
    // Check if any enabled AccessKey exists and pick one for WebSocket
    api.get<{ success: boolean; data: { key: string; enabled: boolean }[] }>('/api/access-keys')
      .then(res => {
        if (res.success) {
          const enabled = res.data.filter(k => k.enabled);
          setHasAccessKey(enabled.length > 0);
          if (enabled.length > 0) {
            setAccessKey(enabled[Math.floor(Math.random() * enabled.length)].key);
          }
        } else {
          setHasAccessKey(false);
        }
      })
      .catch(() => setHasAccessKey(false));
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

  const sendMessage = async (text: string, images?: ImageAttachment[]) => {
    if (!text.trim() && (!images || images.length === 0)) return;
    if (sending) return;
    setSending(true);
    try {
      if (wsRef.current?.readyState === WebSocket.OPEN) {
        const payload: Record<string, unknown> = { type: 'message', message: text };

        if (images && images.length > 0) {
          const urlImages = images.filter(i => i.type === 'url').map(i => i.data);
          const b64Images = images.filter(i => i.type === 'base64').map(i => ({ data: i.data, mediaType: i.mediaType ?? 'image/jpeg' }));
          if (urlImages.length > 0) payload.imageUrls = urlImages;
          if (b64Images.length > 0) payload.imageBase64 = b64Images;
        }

        wsRef.current.send(JSON.stringify(payload));

        // Optimistic user message — show text + image previews
        const optimisticContent = images && images.length > 0
          ? JSON.stringify({ text, images: images.map(i => ({ preview: i.preview })) })
          : text;
        const optimisticMsg: Message = {
          id: Date.now(),
          role: 'user',
          content: optimisticContent,
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
      setOffset(0);
      setHasMore(false);
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

  if (hasAccessKey === false) {
    return (
      <DefaultLayout>
        <div className="flex items-center justify-center h-[60vh]">
          <Card className="max-w-md w-full">
            <div className="p-8 text-center flex flex-col items-center gap-4">
              <ShieldKeyhole className="w-12 h-12 text-gray-400" />
              <h2 className="text-lg font-semibold">{t('chatPage.noAccessKey')}</h2>
              <p className="text-sm text-gray-500">{t('chatPage.noAccessKeyHint')}</p>
              <Button variant="primary" onPress={() => navigate('/security')}>
                {t('chatPage.goToSecurity')}
              </Button>
            </div>
          </Card>
        </div>
      </DefaultLayout>
    );
  }

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
                showTime={showTime}
                markdownEnabled={markdownEnabled}
                splitDelimiters={splitDelimiters}
                onSend={(text, imgs) => sendMessage(text, imgs)}
                onDeleteMessage={deleteMessage}
                onLoadMore={loadMoreMessages}
                onToggleVoiceFeedback={() => toggleFlag('chat.voiceFeedback', setVoiceFeedback)}
                onToggleSplit={() => toggleFlag('chat.splitEnabled', setSplitEnabled)}
                onToggleShowTime={() => toggleFlag('chat.showTime', setShowTime)}
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
