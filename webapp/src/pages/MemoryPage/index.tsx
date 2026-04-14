import { useEffect, useState } from 'react';
import { Card, Tabs, Button, Spinner, Chip, TextArea, toast } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';

interface UserInfo {
  userId: string;
  messageCount: number;
  lastActive: string;
}

interface Message {
  id: number;
  role: string;
  content: string;
  createdAt: string;
}

interface SoulEntry {
  id: number;
  content: string;
  createdAt: string;
}

interface UsersResponse {
  success: boolean;
  data: UserInfo[];
}

interface MessagesResponse {
  success: boolean;
  data: {
    messages: Message[];
    total: number;
    userId: string;
  };
}

interface SoulResponse {
  success: boolean;
  data: SoulEntry[];
}

function MemoryPage() {
  const { t } = useI18n();
  const [, setActiveTab] = useState<string>('sessions');

  // Sessions state
  const [users, setUsers] = useState<UserInfo[]>([]);
  const [selectedUser, setSelectedUser] = useState<string>('');
  const [messages, setMessages] = useState<Message[]>([]);
  const [usersLoading, setUsersLoading] = useState(false);
  const [messagesLoading, setMessagesLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [totalMessages, setTotalMessages] = useState(0);
  const pageSize = 20;

  // Soul state
  const [soulEntries, setSoulEntries] = useState<SoulEntry[]>([]);
  const [soulLoading, setSoulLoading] = useState(false);
  const [newSoulContent, setNewSoulContent] = useState('');
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editContent, setEditContent] = useState('');

  useEffect(() => {
    loadUsers();
  }, []);

  // Sessions methods
  const loadUsers = async () => {
    try {
      setUsersLoading(true);
      const res = await api.get<UsersResponse>('/api/messages/users');
      if (res.success) {
        setUsers(res.data);
        if (res.data.length > 0 && !selectedUser) {
          // Select the most recently active user
          const sorted = [...res.data].sort((a, b) =>
            new Date(b.lastActive).getTime() - new Date(a.lastActive).getTime()
          );
          setSelectedUser(sorted[0].userId);
        }
      }
    } catch {
      // silently fail
    } finally {
      setUsersLoading(false);
    }
  };

  useEffect(() => {
    if (selectedUser) {
      loadMessages();
    }
  }, [selectedUser, page]);

  const loadMessages = async () => {
    if (!selectedUser) return;
    try {
      setMessagesLoading(true);
      const offset = (page - 1) * pageSize;
      const res = await api.get<MessagesResponse>(
        `/api/messages?userId=${selectedUser}&limit=${pageSize}&offset=${offset}`
      );
      if (res.success) {
        setMessages(res.data.messages);
        setTotalMessages(res.data.total);
      }
    } catch {
      // silently fail
    } finally {
      setMessagesLoading(false);
    }
  };

  const totalPages = Math.ceil(totalMessages / pageSize);

  // Soul methods
  const loadSoul = async () => {
    try {
      setSoulLoading(true);
      const res = await api.get<SoulResponse>('/api/soul');
      if (res.success) {
        setSoulEntries(res.data);
      }
    } catch {
      // silently fail
    } finally {
      setSoulLoading(false);
    }
  };

  const addSoulEntry = async () => {
    if (!newSoulContent.trim()) return;
    try {
      await api.post('/api/soul', { content: newSoulContent });
      toast.success(t('memory.addSuccess'));
      setNewSoulContent('');
      loadSoul();
    } catch {
      toast.danger(t('settings.saveFailed'));
    }
  };

  const updateSoulEntry = async (id: number) => {
    if (!editContent.trim()) return;
    try {
      await api.put(`/api/soul/${id}`, { content: editContent });
      toast.success(t('memory.updateSuccess'));
      setEditingId(null);
      setEditContent('');
      loadSoul();
    } catch {
      toast.danger(t('settings.saveFailed'));
    }
  };

  const deleteSoulEntry = async (id: number) => {
    if (!confirm(t('memory.deleteConfirm'))) return;
    try {
      await api.del(`/api/soul/${id}`);
      toast.success(t('memory.deleteSuccess'));
      loadSoul();
    } catch {
      toast.danger(t('settings.saveFailed'));
    }
  };

  const handleTabChange = (key: string | number) => {
    const tabKey = String(key);
    setActiveTab(tabKey);
    if (tabKey === 'soul') {
      loadSoul();
    }
  };

  const getRoleColor = (role: string) => {
    switch (role) {
      case 'user': return 'accent';
      case 'assistant': return 'success';
      case 'tool': return 'warning';
      default: return 'default';
    }
  };

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4">
        <h1 className="text-2xl font-bold mb-6">{t('memory.title')}</h1>

        <Tabs onSelectionChange={handleTabChange}>
          <Tabs.ListContainer>
            <Tabs.List aria-label="Memory tabs">
              <Tabs.Tab id="sessions">
                {t('memory.sessions')}
                <Tabs.Indicator />
              </Tabs.Tab>
              <Tabs.Tab id="soul">
                {t('memory.soul')}
                <Tabs.Indicator />
              </Tabs.Tab>
            </Tabs.List>
          </Tabs.ListContainer>

          {/* Sessions Tab */}
          <Tabs.Panel id="sessions">
            <div className="mt-4 grid grid-cols-1 lg:grid-cols-4 gap-4">
              {/* User list */}
              <Card className="lg:col-span-1">
                <Card.Header>
                  <Card.Title>{t('chat.selectUser')}</Card.Title>
                </Card.Header>
                <Card.Content>
                  {usersLoading ? (
                    <div className="flex justify-center py-4">
                      <Spinner size="sm" />
                    </div>
                  ) : (
                    <div className="space-y-2 max-h-[60vh] overflow-y-auto">
                      {users.map((user) => (
                        <button
                          key={user.userId}
                          className={`w-full text-left p-3 rounded-lg transition-colors ${
                            selectedUser === user.userId
                              ? 'bg-blue-50 dark:bg-blue-900/30 border border-blue-200 dark:border-blue-800'
                              : 'hover:bg-gray-50 dark:hover:bg-gray-800'
                          }`}
                          onClick={() => {
                            setSelectedUser(user.userId);
                            setPage(1);
                          }}
                        >
                          <div className="font-medium text-sm">{user.userId}</div>
                          <div className="text-xs text-gray-500 mt-1">
                            {t('chat.messageCount')}: {user.messageCount}
                          </div>
                          <div className="text-xs text-gray-400">
                            {user.lastActive ? new Date(user.lastActive).toLocaleString() : '-'}
                          </div>
                        </button>
                      ))}
                      {users.length === 0 && (
                        <p className="text-gray-500 text-center py-4">{t('common.noData')}</p>
                      )}
                    </div>
                  )}
                </Card.Content>
              </Card>

              {/* Messages */}
              <Card className="lg:col-span-3">
                <Card.Header>
                  <div className="flex items-center justify-between w-full">
                    <Card.Title>
                      {selectedUser ? `${t('memory.sessions')} - ${selectedUser}` : t('chat.selectUser')}
                    </Card.Title>
                    {totalMessages > 0 && (
                      <Chip size="sm" variant="soft">{totalMessages} {t('chat.messageCount')}</Chip>
                    )}
                  </div>
                </Card.Header>
                <Card.Content>
                  {messagesLoading ? (
                    <div className="flex justify-center py-8">
                      <Spinner size="lg" />
                    </div>
                  ) : messages.length === 0 ? (
                    <p className="text-gray-500 text-center py-8">{t('chat.noMessages')}</p>
                  ) : (
                    <>
                      <div className="space-y-3 max-h-[50vh] overflow-y-auto">
                        {messages.map((msg) => (
                          <div key={msg.id} className="p-3 rounded-lg bg-gray-50 dark:bg-gray-800">
                            <div className="flex items-center gap-2 mb-1">
                              <Chip size="sm" color={getRoleColor(msg.role)} variant="soft">
                                {t(`chat.${msg.role}`) || msg.role}
                              </Chip>
                              <span className="text-xs text-gray-400">
                                {new Date(msg.createdAt).toLocaleString()}
                              </span>
                            </div>
                            <p className="text-sm whitespace-pre-wrap break-words">{msg.content}</p>
                          </div>
                        ))}
                      </div>

                      {/* Pagination */}
                      {totalPages > 1 && (
                        <div className="flex justify-center gap-2 mt-4">
                          <Button
                            size="sm"
                            variant="secondary"
                            isDisabled={page <= 1}
                            onPress={() => setPage(p => p - 1)}
                          >
                            ←
                          </Button>
                          <span className="flex items-center text-sm px-2">
                            {page} / {totalPages}
                          </span>
                          <Button
                            size="sm"
                            variant="secondary"
                            isDisabled={page >= totalPages}
                            onPress={() => setPage(p => p + 1)}
                          >
                            →
                          </Button>
                        </div>
                      )}
                    </>
                  )}
                </Card.Content>
              </Card>
            </div>
          </Tabs.Panel>

          {/* Soul Tab */}
          <Tabs.Panel id="soul">
            <Card className="mt-4">
              <Card.Header>
                <Card.Title>{t('memory.soul')}</Card.Title>
              </Card.Header>
              <Card.Content>
                {soulLoading ? (
                  <div className="flex justify-center py-8">
                    <Spinner size="lg" />
                  </div>
                ) : (
                  <div className="space-y-4">
                    {/* Add new entry */}
                    <div className="p-4 border border-dashed border-gray-300 dark:border-gray-600 rounded-lg">
                      <TextArea
                        value={newSoulContent}
                        onChange={(e) => setNewSoulContent(e.target.value)}
                        rows={3}
                        placeholder={t('memory.content')}
                        className="w-full mb-3"
                      />
                      <Button size="sm" onPress={addSoulEntry} isDisabled={!newSoulContent.trim()}>
                        {t('memory.addSoul')}
                      </Button>
                    </div>

                    {/* Entries list */}
                    {soulEntries.length === 0 ? (
                      <p className="text-gray-500 text-center py-4">{t('common.noData')}</p>
                    ) : (
                      <div className="space-y-3">
                        {soulEntries.map((entry) => (
                          <div key={entry.id} className="p-4 rounded-lg bg-gray-50 dark:bg-gray-800">
                            {editingId === entry.id ? (
                              <div className="space-y-2">
                                <TextArea
                                  value={editContent}
                                  onChange={(e) => setEditContent(e.target.value)}
                                  rows={3}
                                  className="w-full"
                                />
                                <div className="flex gap-2">
                                  <Button size="sm" onPress={() => updateSoulEntry(entry.id)}>
                                    {t('common.save')}
                                  </Button>
                                  <Button size="sm" variant="secondary" onPress={() => setEditingId(null)}>
                                    {t('common.cancel')}
                                  </Button>
                                </div>
                              </div>
                            ) : (
                              <>
                                <p className="text-sm whitespace-pre-wrap mb-2">{entry.content}</p>
                                <div className="flex items-center justify-between">
                                  <span className="text-xs text-gray-400">
                                    {new Date(entry.createdAt).toLocaleString()}
                                  </span>
                                  <div className="flex gap-2">
                                    <Button
                                      size="sm"
                                      variant="secondary"
                                      onPress={() => {
                                        setEditingId(entry.id);
                                        setEditContent(entry.content);
                                      }}
                                    >
                                      {t('common.edit')}
                                    </Button>
                                    <Button
                                      size="sm"
                                      variant="danger"
                                      onPress={() => deleteSoulEntry(entry.id)}
                                    >
                                      {t('common.delete')}
                                    </Button>
                                  </div>
                                </div>
                              </>
                            )}
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                )}
              </Card.Content>
            </Card>
          </Tabs.Panel>
        </Tabs>
      </div>
    </DefaultLayout>
  );
}

export default MemoryPage;
