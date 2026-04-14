import { useEffect, useState } from 'react';
import { Card, Button, Spinner, Chip } from '@heroui/react';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import type { UserInfo, Message, UsersResponse, MessagesResponse } from './types';

interface MessagesPanelProps {
  refreshKey: number;
}

const getRoleColor = (role: string) => {
  switch (role) {
    case 'user': return 'accent';
    case 'assistant': return 'success';
    case 'tool': return 'warning';
    default: return 'default';
  }
};

const PAGE_SIZE = 20;

function MessagesPanel({ refreshKey }: MessagesPanelProps) {
  const { t } = useI18n();
  const [users, setUsers] = useState<UserInfo[]>([]);
  const [selectedUser, setSelectedUser] = useState<string>('');
  const [messages, setMessages] = useState<Message[]>([]);
  const [usersLoading, setUsersLoading] = useState(false);
  const [messagesLoading, setMessagesLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [totalMessages, setTotalMessages] = useState(0);

  useEffect(() => {
    loadUsers();
    setSelectedUser('');
    setMessages([]);
  }, [refreshKey]);

  const loadUsers = async () => {
    try {
      setUsersLoading(true);
      const res = await api.get<UsersResponse>('/api/messages/users');
      if (res.success) {
        setUsers(res.data);
        if (res.data.length > 0 && !selectedUser) {
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
      const offset = (page - 1) * PAGE_SIZE;
      const res = await api.get<MessagesResponse>(
        `/api/messages?userId=${selectedUser}&limit=${PAGE_SIZE}&offset=${offset}`
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

  const totalPages = Math.ceil(totalMessages / PAGE_SIZE);

  return (
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
  );
}

export default MessagesPanel;
