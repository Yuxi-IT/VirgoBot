import { useEffect, useState, useCallback, useRef } from 'react';
import { Spinner, Alert, Button, Modal } from '@heroui/react';
import { useOverlayState } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import StatusCards from './StatusCards';
import ChannelStatusGrid from './ChannelStatusGrid';
import ServerConfigCard from './ServerConfigCard';
import type { StatusData, ApiResponse } from './types';

function DashboardPage() {
  const { t } = useI18n();
  const [status, setStatus] = useState<StatusData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [restarting, setRestarting] = useState(false);
  const restartModal = useOverlayState();
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const loadStatus = useCallback(async (silent = false) => {
    try {
      if (!silent) {
        setLoading(true);
        setError(null);
      }
      const res = await api.get<ApiResponse>('/api/status');
      if (res.success) {
        setStatus(res.data);
        if (error) setError(null);
      }
    } catch (err) {
      if (!silent) {
        setError(err instanceof Error ? err.message : 'Unknown error');
      }
    } finally {
      if (!silent) setLoading(false);
    }
  }, [error]);

  useEffect(() => {
    loadStatus();
    intervalRef.current = setInterval(() => loadStatus(true), 1000);
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, []);

  const handleRestart = async () => {
    restartModal.close();
    setRestarting(true);
    try {
      await api.post<{ success: boolean }>('/api/gateway/restart', {});
      await loadStatus(true);
    } catch {
      // will be reflected in status
    } finally {
      setRestarting(false);
    }
  };

  if (loading) {
    return (
      <DefaultLayout>
        <div className="flex items-center justify-center h-[60vh]">
          <Spinner size="lg" />
        </div>
      </DefaultLayout>
    );
  }

  if (error) {
    return (
      <DefaultLayout>
        <div className="container mx-auto p-4">
          <Alert status="danger">
            <Alert.Content>
              <Alert.Title>{t('common.error')}</Alert.Title>
              <Alert.Description>{error}</Alert.Description>
            </Alert.Content>
          </Alert>
        </div>
      </DefaultLayout>
    );
  }

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4">
        <div className="flex items-center justify-between mb-6">
          <h1 className="text-2xl font-bold">{t('dashboard.title')}</h1>
          <Button
            variant="danger"
            onPress={restartModal.open}
            isDisabled={restarting}
          >
            {restarting ? (
              <><Spinner size="sm" className="mr-2" />{t('gateway.restarting')}</>
            ) : (
              t('gateway.restart')
            )}
          </Button>
        </div>

        {/* Restart Confirmation Modal */}
        <Modal>
          <Modal.Backdrop isOpen={restartModal.isOpen} onOpenChange={restartModal.toggle}>
            <Modal.Container size="lg">
              <Modal.Dialog role="alertdialog">
                <Modal.Header>
                  <Modal.Heading>{t('gateway.restart')}</Modal.Heading>
                </Modal.Header>
                <Modal.Body>
                  <p>{t('gateway.restartConfirm')}</p>
                </Modal.Body>
                <Modal.Footer>
                  <Button variant="secondary" onPress={restartModal.close}>
                    {t('common.cancel')}
                  </Button>
                  <Button variant="danger" onPress={handleRestart}>
                    {t('common.confirm')}
                  </Button>
                </Modal.Footer>
              </Modal.Dialog>
            </Modal.Container>
          </Modal.Backdrop>
        </Modal>

        {status && (
          <>
            <StatusCards status={status} />
            <ChannelStatusGrid channels={status.channels} />
            <ServerConfigCard server={status.server} />
          </>
        )}
      </div>
    </DefaultLayout>
  );
}

export default DashboardPage;
