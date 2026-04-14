import { Card, Chip, Spinner, Table, Pagination } from '@heroui/react';
import { useI18n } from '../../i18n';
import type { Message } from './types';

interface MessagesTableProps {
  messages: Message[];
  loading: boolean;
  selectedUserId: string | null;
  searchQuery: string;
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

function MessagesTable({ messages, loading, selectedUserId, searchQuery, page, totalPages, onPageChange }: MessagesTableProps) {
  const { t } = useI18n();

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
            <Pagination.Previous isDisabled={page <= 1} onPress={() => onPageChange(Math.max(1, page - 1))}>
              <Pagination.PreviousIcon />
            </Pagination.Previous>
          </Pagination.Item>
          {pages.slice(Math.max(0, page - 3), Math.min(totalPages, page + 2)).map(p => (
            <Pagination.Item key={p}>
              <Pagination.Link isActive={p === page} onPress={() => onPageChange(p)}>{p}</Pagination.Link>
            </Pagination.Item>
          ))}
          <Pagination.Item>
            <Pagination.Next isDisabled={page >= totalPages} onPress={() => onPageChange(Math.min(totalPages, page + 1))}>
              <Pagination.NextIcon />
            </Pagination.Next>
          </Pagination.Item>
        </Pagination.Content>
      </Pagination>
    );
  };

  return (
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
  );
}

export default MessagesTable;
