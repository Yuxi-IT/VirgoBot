import { useEffect, useState, useCallback, useRef } from 'react';
import { Card, Chip, Spinner, Table, Pagination, SearchField, Label } from '@heroui/react';
import { Select, ListBox } from '@heroui/react';
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

const PAGE_SIZE = 20;

function ChatPage() {
  const { t } = useI18n();
  const [users, setUsers] = useState<UserInfo[]>([]);
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);
  const [messages, setMessages] = useState<Message[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [usersLoading, setUsersLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const loadUsers = useCallback(async (silent = false) => {
    try {
      if (!silent) setUsersLoading(true);
      const res = await api.get<UsersResponse>('/api/messages/users');
      if (res.success) {
        setUsers(res.data);
      }
    } catch {
      // silently fail
    } finally {
      if (!silent) setUsersLoading(false);
    }
  }, []);

  const loadMessages = useCallback(async (userId: string, pageNum: number, silent = false) => {
    try {
      if (!silent) setLoading(true);
      const offset = (pageNum - 1) * PAGE_SIZE;
      const res = await api.get<MessagesResponse>(`/api/messages?userId=${userId}&limit=${PAGE_SIZE}&offset=${offset}`);
      if (res.success) {
        setMessages(res.data.messages);
        setTotal(res.data.total);
      }
    } catch {
      // silently fail
    } finally {
      if (!silent) setLoading(false);
    }
  }, []);

  // Store refs for polling access to latest state
  const selectedUserIdRef = useRef(selectedUserId);
  const pageRef = useRef(page);
  selectedUserIdRef.current = selectedUserId;
  pageRef.current = page;

  useEffect(() => {
    loadUsers();
    intervalRef.current = setInterval(() => {
      loadUsers(true);
      if (selectedUserIdRef.current) {
        loadMessages(selectedUserIdRef.current, pageRef.current, true);
      }
    }, 1000);
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [loadUsers, loadMessages]);

  useEffect(() => {
    if (selectedUserId) {
      loadMessages(selectedUserId, page);
    }
  }, [selectedUserId, page, loadMessages]);

  const getRoleChip = (role: string) => {
    switch (role) {
      case 'user':
        return <Chip color="accent" size="sm">{t('chat.user')}</Chip>;
      case 'assistant':
        return <Chip color="success" size="sm">{t('chat.assistant')}</Chip>;
      case 'tool':
        return <Chip color="warning" size="sm">{t('chat.tool')}</Chip>;
      case 'system':
        return <Chip color="default" size="sm">{t('chat.system')}</Chip>;
      default:
        return <Chip size="sm">{role}</Chip>;
    }
  };

  const totalPages = Math.ceil(total / PAGE_SIZE);

  const filteredMessages = searchQuery
    ? messages.filter(m => m.content.toLowerCase().includes(searchQuery.toLowerCase()))
    : messages;

  const renderPagination = () => {
    if (totalPages <= 1) return null;
    const pages: number[] = [];
    for (let i = 1; i <= totalPages; i++) pages.push(i);

    return (
      <Pagination>
        <Pagination.Content>
          <Pagination.Item>
            <Pagination.Previous isDisabled={page <= 1} onPress={() => setPage(p => Math.max(1, p - 1))}>
              <Pagination.PreviousIcon />
            </Pagination.Previous>
          </Pagination.Item>
          {pages.slice(Math.max(0, page - 3), Math.min(totalPages, page + 2)).map(p => (
            <Pagination.Item key={p}>
              <Pagination.Link isActive={p === page} onPress={() => setPage(p)}>{p}</Pagination.Link>
            </Pagination.Item>
          ))}
          <Pagination.Item>
            <Pagination.Next isDisabled={page >= totalPages} onPress={() => setPage(p => Math.min(totalPages, p + 1))}>
              <Pagination.NextIcon />
            </Pagination.Next>
          </Pagination.Item>
        </Pagination.Content>
      </Pagination>
    );
  };

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4">
        <h1 className="text-2xl font-bold mb-6">{t('chat.title')}</h1>

        <div className="flex flex-col sm:flex-row gap-4 mb-6">
          {/* User Selector */}
          <div className="w-full sm:w-64">
            <Select
              placeholder={t('chat.selectUser')}
              onChange={(value) => {
                const val = String(value);
                setSelectedUserId(val);
                setPage(1);
              }}
            >
              <Select.Trigger>
                <Select.Value />
              </Select.Trigger>
              <Select.Popover>
                <ListBox>
                  {usersLoading ? (
                    <ListBox.Item id="loading" textValue="Loading">
                      <Label>{t('common.loading')}</Label>
                    </ListBox.Item>
                  ) : users.map(user => (
                    <ListBox.Item key={user.userId} id={user.userId} textValue={user.userId}>
                      <Label>{user.userId}</Label>
                    </ListBox.Item>
                  ))}
                </ListBox>
              </Select.Popover>
            </Select>
          </div>

          {/* Search */}
          <div className="flex-1">
            <SearchField value={searchQuery} onChange={setSearchQuery}>
              <SearchField.Group>
                <SearchField.SearchIcon />
                <SearchField.Input placeholder={t('common.search')} />
                <SearchField.ClearButton />
              </SearchField.Group>
            </SearchField>
          </div>
        </div>

        {/* Messages Table */}
        <Card>
          <Card.Content>
            {loading ? (
              <div className="flex justify-center py-8">
                <Spinner size="lg" />
              </div>
            ) : !selectedUserId ? (
              <p className="text-center py-8 text-gray-500">{t('chat.selectUser')}</p>
            ) : filteredMessages.length === 0 ? (
              <p className="text-center py-8 text-gray-500">{t('chat.noMessages')}</p>
            ) : (
              <Table>
                <Table.ScrollContainer>
                  <Table.Content aria-label="Messages">
                    <Table.Header>
                      <Table.Column>ID</Table.Column>
                      <Table.Column>{t('chat.role')}</Table.Column>
                      <Table.Column>{t('chat.content')}</Table.Column>
                      <Table.Column>{t('chat.time')}</Table.Column>
                    </Table.Header>
                    <Table.Body>
                      {filteredMessages.map(msg => (
                        <Table.Row key={msg.id}>
                          <Table.Cell>{msg.id}</Table.Cell>
                          <Table.Cell>{getRoleChip(msg.role)}</Table.Cell>
                          <Table.Cell>
                            <div className="max-w-md truncate" title={msg.content}>
                              {msg.content}
                            </div>
                          </Table.Cell>
                          <Table.Cell>
                            {new Date(msg.createdAt).toLocaleString()}
                          </Table.Cell>
                        </Table.Row>
                      ))}
                    </Table.Body>
                  </Table.Content>
                </Table.ScrollContainer>
                <Table.Footer>
                  <div className="flex justify-center py-2">
                    {renderPagination()}
                  </div>
                </Table.Footer>
              </Table>
            )}
          </Card.Content>
        </Card>
      </div>
    </DefaultLayout>
  );
}

export default ChatPage;
