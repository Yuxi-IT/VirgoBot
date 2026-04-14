import { Card, Chip, Spinner, Table, Pagination } from '@heroui/react';
import { useI18n } from '../../i18n';
import type { LogEntry } from './types';

interface LogsTableProps {
  logs: LogEntry[];
  loading: boolean;
  searchQuery: string;
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

function LogsTable({ logs, loading, searchQuery, page, totalPages, onPageChange }: LogsTableProps) {
  const { t } = useI18n();

  const getLevelChip = (level: string) => {
    switch (level.toLowerCase()) {
      case 'info':
        return <Chip color="accent" size="sm">{t('logs.info')}</Chip>;
      case 'warn':
        return <Chip color="warning" size="sm">{t('logs.warn')}</Chip>;
      case 'error':
        return <Chip color="danger" size="sm">{t('logs.error')}</Chip>;
      case 'success':
        return <Chip color="success" size="sm">{t('logs.success')}</Chip>;
      default:
        return <Chip size="sm">{level}</Chip>;
    }
  };

  const filteredLogs = searchQuery
    ? logs.filter(l =>
        l.message.toLowerCase().includes(searchQuery.toLowerCase()) ||
        l.component.toLowerCase().includes(searchQuery.toLowerCase())
      )
    : logs;

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
        ) : filteredLogs.length === 0 ? (
          <p className="text-center py-8 text-gray-500">{t('common.noData')}</p>
        ) : (
          <Table>
            <Table.ScrollContainer>
              <Table.Content aria-label="Logs">
                <Table.Header>
                  <Table.Column>ID</Table.Column>
                  <Table.Column>{t('logs.level')}</Table.Column>
                  <Table.Column>{t('logs.component')}</Table.Column>
                  <Table.Column>{t('logs.message')}</Table.Column>
                  <Table.Column>{t('logs.timestamp')}</Table.Column>
                </Table.Header>
                <Table.Body>
                  {filteredLogs.map(log => (
                    <Table.Row key={log.id}>
                      <Table.Cell>{log.id}</Table.Cell>
                      <Table.Cell>{getLevelChip(log.level)}</Table.Cell>
                      <Table.Cell>
                        <Chip size="sm" variant="soft">{log.component}</Chip>
                      </Table.Cell>
                      <Table.Cell>
                        <div className="max-w-lg truncate" title={log.message}>
                          {log.message}
                        </div>
                      </Table.Cell>
                      <Table.Cell>
                        {new Date(log.timestamp).toLocaleString()}
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

export default LogsTable;
