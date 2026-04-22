import { useEffect, useState, useCallback, useRef } from 'react';
import { Button, Modal, Chip, Spinner, Table, Card, toast, SearchField, Toolbar } from '@heroui/react';
import { Select, ListBox, Label } from '@heroui/react';
import { useOverlayState } from '@heroui/react';
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
  data: { logs: LogEntry[]; total: number };
}

const PAGE_SIZE = 50;

export default function LogsTab({ active }: { active: boolean }) {
  const { t } = useI18n();
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [levelFilter, setLevelFilter] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const clearModal = useOverlayState();
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const loadLogs = useCallback(async (silent = false) => {
    try {
      if (!silent) setLoading(true);
      const offset = (page - 1) * PAGE_SIZE;
      let url = `/api/logs?limit=${PAGE_SIZE}&offset=${offset}`;
      if (levelFilter) url += `&level=${levelFilter}`;
      const res = await api.get<LogsResponse>(url);
      if (res.success) {
        setLogs(res.data.logs);
        setTotal(res.data.total);
      }
    } catch { /* silent */ } finally { if (!silent) setLoading(false); }
  }, [page, levelFilter]);

  useEffect(() => {
    if (active) loadLogs();
  }, [active, loadLogs]);

  useEffect(() => {
    if (!active) return;
    intervalRef.current = setInterval(() => loadLogs(true), 1000);
    return () => { if (intervalRef.current) clearInterval(intervalRef.current); };
  }, [active, loadLogs]);

  const handleClear = async () => {
    try {
      await api.del('/api/logs');
      toast.success(t('logs.clearSuccess'));
      clearModal.close();
      setPage(1);
      await loadLogs();
    } catch { toast.danger(t('common.error')); }
  };

  const getLevelChip = (level: string) => {
    switch (level.toLowerCase()) {
      case 'info': return <Chip color="accent" size="sm">{t('logs.info')}</Chip>;
      case 'warn': return <Chip color="warning" size="sm">{t('logs.warn')}</Chip>;
      case 'error': return <Chip color="danger" size="sm">{t('logs.error')}</Chip>;
      case 'success': return <Chip color="success" size="sm">{t('logs.success')}</Chip>;
      default: return <Chip size="sm">{level}</Chip>;
    }
  };

  const filtered = searchQuery
    ? logs.filter(l =>
        l.message.toLowerCase().includes(searchQuery.toLowerCase()) ||
        l.component.toLowerCase().includes(searchQuery.toLowerCase()))
    : logs;

  const totalPages = Math.ceil(total / PAGE_SIZE);

  return (
    <div className="space-y-4 mt-4">
      <Toolbar aria-label="Log actions">
        <div className="flex flex-col sm:flex-row gap-4 w-full items-start sm:items-center">
          <div className="w-full sm:w-48">
            <Select
              placeholder={t('logs.allLevels')}
              onChange={(value) => { setLevelFilter(String(value ?? '')); setPage(1); }}
            >
              <Select.Trigger><Select.Value /></Select.Trigger>
              <Select.Popover>
                <ListBox>
                  <ListBox.Item id="" textValue="All"><Label>{t('logs.allLevels')}</Label></ListBox.Item>
                  <ListBox.Item id="Info" textValue="Info"><Label>{t('logs.info')}</Label></ListBox.Item>
                  <ListBox.Item id="Warn" textValue="Warn"><Label>{t('logs.warn')}</Label></ListBox.Item>
                  <ListBox.Item id="Error" textValue="Error"><Label>{t('logs.error')}</Label></ListBox.Item>
                  <ListBox.Item id="Success" textValue="Success"><Label>{t('logs.success')}</Label></ListBox.Item>
                </ListBox>
              </Select.Popover>
            </Select>
          </div>
          <div className="flex-1">
            <SearchField value={searchQuery} onChange={setSearchQuery}>
              <SearchField.Group>
                <SearchField.SearchIcon />
                <SearchField.Input placeholder={t('common.search')} />
                <SearchField.ClearButton />
              </SearchField.Group>
            </SearchField>
          </div>
          <div className="flex gap-2">
            <Button variant="secondary" onPress={() => loadLogs()}>{t('common.refresh')}</Button>
            <Button variant="danger" onPress={clearModal.open}>{t('logs.clearLogs')}</Button>
          </div>
        </div>
      </Toolbar>

      <Card>
        <Card.Content>
          {loading ? (
            <div className="flex justify-center py-8"><Spinner size="lg" /></div>
          ) : filtered.length === 0 ? (
            <p className="text-center py-8 text-default-500">{t('common.noData')}</p>
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
                    {filtered.map(log => (
                      <Table.Row key={log.id}>
                        <Table.Cell>{log.id}</Table.Cell>
                        <Table.Cell>{getLevelChip(log.level)}</Table.Cell>
                        <Table.Cell><Chip size="sm" variant="soft">{log.component}</Chip></Table.Cell>
                        <Table.Cell>
                          <div className="max-w-lg truncate" title={log.message}>{log.message}</div>
                        </Table.Cell>
                        <Table.Cell>{new Date(log.timestamp).toLocaleString()}</Table.Cell>
                      </Table.Row>
                    ))}
                  </Table.Body>
                </Table.Content>
              </Table.ScrollContainer>
            </Table>
          )}
          {totalPages > 1 && (
            <div className="flex justify-center items-center gap-2 py-2">
              <Button size="sm" variant="ghost" isDisabled={page <= 1} onPress={() => setPage(p => p - 1)}>上一页</Button>
              <span className="text-sm text-default-500">{page} / {totalPages}</span>
              <Button size="sm" variant="ghost" isDisabled={page >= totalPages} onPress={() => setPage(p => p + 1)}>下一页</Button>
            </div>
          )}
        </Card.Content>
      </Card>

      <Modal>
        <Modal.Backdrop isOpen={clearModal.isOpen} onOpenChange={clearModal.toggle}>
          <Modal.Container size="lg">
            <Modal.Dialog role="alertdialog">
              <Modal.Header><Modal.Heading>{t('logs.clearLogs')}</Modal.Heading></Modal.Header>
              <Modal.Body><p>{t('logs.clearConfirm')}</p></Modal.Body>
              <Modal.Footer>
                <Button variant="secondary" onPress={clearModal.close}>{t('common.cancel')}</Button>
                <Button variant="danger" onPress={handleClear}>{t('common.confirm')}</Button>
              </Modal.Footer>
            </Modal.Dialog>
          </Modal.Container>
        </Modal.Backdrop>
      </Modal>
    </div>
  );
}
