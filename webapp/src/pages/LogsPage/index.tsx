import { useEffect, useState, useCallback, useRef } from 'react';
import { Button, Modal, toast } from '@heroui/react';
import { useOverlayState } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import LogToolbar from './LogToolbar';
import LogsTable from './LogsTable';
import type { LogsResponse } from './types';

const PAGE_SIZE = 50;

function LogsPage() {
  const { t } = useI18n();
  const [logs, setLogs] = useState<LogsResponse['data']['logs']>([]);
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

  const totalPages = Math.ceil(total / PAGE_SIZE);

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4">
        <h1 className="text-2xl font-bold mb-6">{t('logs.title')}</h1>

        <LogToolbar
          levelFilter={levelFilter}
          searchQuery={searchQuery}
          onLevelFilterChange={(level) => { setLevelFilter(level); setPage(1); }}
          onSearchChange={setSearchQuery}
          onRefresh={() => loadLogs()}
          onClear={clearModal.open}
        />

        <LogsTable
          logs={logs}
          loading={loading}
          searchQuery={searchQuery}
          page={page}
          totalPages={totalPages}
          onPageChange={setPage}
        />

        {/* Clear Confirmation Modal */}
        <Modal>
          <Modal.Backdrop isOpen={clearModal.isOpen} onOpenChange={clearModal.toggle}>
            <Modal.Container size="lg">
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
