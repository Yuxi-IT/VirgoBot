import { useState, useEffect, useRef } from 'react';
import { Modal, Button, Spinner } from '@heroui/react';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';

interface ILinkLoginModalProps {
  isOpen: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess: () => void;
}

interface LoginStartData {
  qrCodeUrl: string;
  status: string;
}

interface LoginStatusData {
  status: string;
  token?: string;
}

function ILinkLoginModal({ isOpen, onOpenChange, onSuccess }: ILinkLoginModalProps) {
  const { t } = useI18n();
  const [loading, setLoading] = useState(false);
  const [qrUrl, setQrUrl] = useState<string | null>(null);
  const [status, setStatus] = useState<string>('');
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    if (isOpen) {
      startLogin();
    } else {
      cleanup();
    }
    return cleanup;
  }, [isOpen]);

  const cleanup = () => {
    if (intervalRef.current) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
    setQrUrl(null);
    setStatus('');
  };

  const startLogin = async () => {
    setLoading(true);
    try {
      const res = await api.post<{ success: boolean; data: LoginStartData }>('/api/ilink/login/start', {});
      if (res.success && res.data) {
        setQrUrl(res.data.qrCodeUrl);
        setStatus('waiting');
        if (res.data.qrCodeUrl) {
          window.open(res.data.qrCodeUrl, '_blank', 'width=600,height=700');
        }
        startPolling();
      }
    } catch (error) {
      console.error('Failed to start iLink login:', error);
    } finally {
      setLoading(false);
    }
  };

  const openQrWindow = () => {
    if (qrUrl) {
      window.open(qrUrl, '_blank', 'width=600,height=700');
    }
  };

  const startPolling = () => {
    if (intervalRef.current) clearInterval(intervalRef.current);

    intervalRef.current = setInterval(async () => {
      try {
        const res = await api.get<{ success: boolean; data: LoginStatusData }>('/api/ilink/login/status');
        if (res.success && res.data) {
          setStatus(res.data.status);

          if (res.data.status === 'confirmed') {
            if (intervalRef.current) clearInterval(intervalRef.current);
            intervalRef.current = null;
            onSuccess();
            onOpenChange(false);
          } else if (res.data.status === 'expired') {
            if (intervalRef.current) clearInterval(intervalRef.current);
            intervalRef.current = null;
          }
        }
      } catch (error) {
        console.error('Failed to query login status:', error);
      }
    }, 2000);
  };

  const getStatusText = () => {
    switch (status) {
      case 'waiting': return t('channel.ilinkLoginWait');
      case 'scanned': return t('channel.ilinkLoginScanned');
      case 'confirmed': return t('channel.ilinkLoginConfirmed');
      case 'expired': return t('channel.ilinkLoginExpired');
      default: return '';
    }
  };

  return (
    <Modal.Backdrop isOpen={isOpen} onOpenChange={onOpenChange}>
      <Modal.Container>
        <Modal.Dialog>
          <Modal.Header>
            <Modal.Heading>{t('channel.ilinkLogin')}</Modal.Heading>
            <Modal.CloseTrigger />
          </Modal.Header>
          <Modal.Body>
            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '1rem', padding: '2rem' }}>
              {loading && <Spinner size="lg" />}

              {qrUrl && status !== 'expired' && (
                <>
                  <div style={{ textAlign: 'center', fontSize: '1.1rem' }}>
                    <p>{getStatusText()}</p>
                    {(status === 'waiting' || status === 'scanned') && (
                      <Spinner size="sm" style={{ marginTop: '0.5rem' }} />
                    )}
                  </div>
                  <div style={{ marginTop: '1rem' }}>
                    <Button onPress={openQrWindow}>
                      {t('channel.ilinkLoginReopen')}
                    </Button>
                  </div>
                  <p style={{ fontSize: '0.9rem', color: '#666', marginTop: '0.5rem' }}>
                    {t('channel.ilinkLoginHint')}
                  </p>
                </>
              )}

              {status === 'expired' && (
                <div style={{ textAlign: 'center' }}>
                  <p style={{ marginBottom: '1rem' }}>{getStatusText()}</p>
                  <Button onPress={startLogin}>
                    {t('channel.ilinkLoginRefresh')}
                  </Button>
                </div>
              )}
            </div>
          </Modal.Body>
          <Modal.Footer>
            <Button variant="ghost" onPress={() => onOpenChange(false)}>
              {t('common.cancel')}
            </Button>
          </Modal.Footer>
        </Modal.Dialog>
      </Modal.Container>
    </Modal.Backdrop>
  );
}

export default ILinkLoginModal;
