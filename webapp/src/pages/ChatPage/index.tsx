import { useEffect, useState, useCallback, useRef } from 'react';
import { SearchField } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import UserSelector from './UserSelector';
import MessagesTable from './MessagesTable';
import type { UserInfo, Message, UsersResponse, MessagesResponse } from './types';

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

  const totalPages = Math.ceil(total / PAGE_SIZE);

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4">
        <h1 className="text-2xl font-bold mb-6">{t('chat.title')}</h1>

        <div className="flex flex-col sm:flex-row gap-4 mb-6">
          <UserSelector
            users={users}
            loading={usersLoading}
            onSelect={(userId) => {
              setSelectedUserId(userId);
              setPage(1);
            }}
          />

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

        <MessagesTable
          messages={messages}
          loading={loading}
          selectedUserId={selectedUserId}
          searchQuery={searchQuery}
          page={page}
          totalPages={totalPages}
          onPageChange={setPage}
        />
      </div>
    </DefaultLayout>
  );
}

export default ChatPage;
