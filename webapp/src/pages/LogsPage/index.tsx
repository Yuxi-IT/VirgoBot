import { useEffect, useState, useCallback, useRef } from 'react';
import { Card, Button, Chip, Spinner, Table, Pagination, SearchField, Toolbar, Label, Modal, toast } from '@heroui/react';
import { Select, ListBox } from '@heroui/react';
import { useOverlayState } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';

interface LogEntry {
  id: number;
  level: string;
  component: string;
  message: string;
  timestamp: string;
}

interface LogsResponse {
  success: boolean;
  data: {
    logs: LogEntry[];
    total: number;
  };
}

const PAGE_SIZE = 50;

function LogsPage() {
  const { t } = useI18n();
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [levelFilter, setLevelFilter] = useState<string>('');
  const [searchQuery, setSearchQuery] = useState('');
  const clearModal = useOverlayState();
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const loadLogs = useCallback(async (silent = false) => {
    try {
      if (!silent) setLoading(true);
      const offset = (page - 1) * PAGE_SIZE;
      let url = `/api/logs?limit=${PAGE_SIZE}&offset=${offset}`;
      if (levelFilter) {
        url += `&level=${levelFilter}`;
      }
      const res = await api.get<LogsResponse>(url);
      if (res.success) {
        setLogs(res.data.logs);
        setTotal(res.data.total);
      }
    } catch {
      // silently fail
    } finally {
      if (!silent) setLoading(false);
    }
  }, [page, levelFilter]);

  useEffect(() => {
    loadLogs();
  }, [loadLogs]);

  useEffect(() => {
    intervalRef.current = setInterval(() => loadLogs(true), 1000);
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [loadLogs]);

  const handleClearLogs = async () => {
    try {
      await api.del('/api/logs');
      toast.success(t('logs.clearSuccess'));
      clearModal.close();
      setPage(1);
      await loadLogs();
    } catch {
      toast.danger(t('common.error'));
    }
  };

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

  const totalPages = Math.ceil(total / PAGE_SIZE);

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
        <h1 className="text-2xl font-bold mb-6">{t('logs.title')}</h1>

        {/* Toolbar */}
        <Toolbar aria-label="Log actions" className="mb-4">
          <div className="flex flex-col sm:flex-row gap-4 w-full items-start sm:items-center">
            {/* Level Filter */}
            <div className="w-full sm:w-48">
              <Select
                placeholder={t('logs.allLevels')}
                onChange={(value) => {
                  setLevelFilter(String(value ?? ''));
                  setPage(1);
                }}
              >
                <Select.Trigger>
                  <Select.Value />
                </Select.Trigger>
                <Select.Popover>
                  <ListBox>
                    <ListBox.Item id="" textValue="All">
                      <Label>{t('logs.allLevels')}</Label>
                    </ListBox.Item>
                    <ListBox.Item id="Info" textValue="Info">
                      <Label>{t('logs.info')}</Label>
                    </ListBox.Item>
                    <ListBox.Item id="Warn" textValue="Warn">
                      <Label>{t('logs.warn')}</Label>
                    </ListBox.Item>
                    <ListBox.Item id="Error" textValue="Error">
                      <Label>{t('logs.error')}</Label>
                    </ListBox.Item>
                    <ListBox.Item id="Success" textValue="Success">
                      <Label>{t('logs.success')}</Label>
                    </ListBox.Item>
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

            {/* Actions */}
            <div className="flex gap-2">
              <Button variant="secondary" onPress={() => loadLogs()}>
                {t('common.refresh')}
              </Button>
              <Button variant="danger" onPress={clearModal.open}>
                {t('logs.clearLogs')}
              </Button>
            </div>
          </div>
        </Toolbar>

        {/* Logs Table */}
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

        {/* Clear Confirmation Modal */}
        <Modal>
          <Modal.Backdrop isOpen={clearModal.isOpen} onOpenChange={clearModal.toggle}>
            <Modal.Container>
              <Modal.Dialog role="alertdialog">
                <Modal.Header>
                  <Modal.Heading>{t('logs.clearLogs')}</Modal.Heading>
                </Modal.Header>
                <Modal.Body>
                  <p>{t('logs.clearConfirm')}</p>
                </Modal.Body>
                <Modal.Footer>
                  <Button variant="secondary" onPress={clearModal.close}>
                    {t('common.cancel')}
                  </Button>
                  <Button variant="danger" onPress={handleClearLogs}>
                    {t('common.confirm')}
                  </Button>
                </Modal.Footer>
              </Modal.Dialog>
            </Modal.Container>
          </Modal.Backdrop>
        </Modal>
      </div>
    </DefaultLayout>
  );
}

export default LogsPage;
