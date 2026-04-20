import { useState, useEffect } from 'react';
import { Modal, Button, Spinner } from '@heroui/react';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';

interface ILinkLoginModalProps {
  isOpen: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess: () => void;
}

interface QrCodeData {
  qrCode: string;
  qrCodeImageUri: string;
}

interface StatusData {
  status: string;
  credentials?: {
    botToken: string;
    iLinkBotId: string;
    iLinkUserId: string;
    apiBaseUri: string;
  };
}

function ILinkLoginModal({ isOpen, onOpenChange, onSuccess }: ILinkLoginModalProps) {
  const { t } = useI18n();
  const [loading, setLoading] = useState(false);
  const [qrCode, setQrCode] = useState<QrCodeData | null>(null);
  const [status, setStatus] = useState<string>('');
  const [polling, setPolling] = useState(false);

  useEffect(() => {
    if (isOpen) {
      createQrCode();
    } else {
      setQrCode(null);
      setStatus('');
      setPolling(false);
    }
  }, [isOpen]);

  const createQrCode = async () => {
    setLoading(true);
    try {
      const res = await api.post<{ success: boolean; data: QrCodeData }>('/api/ilink/login/qrcode', {});
      if (res.success && res.data) {
        setQrCode(res.data);
        setStatus('Wait');
        // 直接在新窗口打开 QR 码页面
        window.open(res.data.qrCodeImageUri, '_blank', 'width=600,height=700');
        startPolling(res.data.qrCode);
      }
    } catch (error) {
      console.error('Failed to create QR code:', error);
    } finally {
      setLoading(false);
    }
  };

  const openQrCodeInNewWindow = () => {
    if (qrCode?.qrCodeImageUri) {
      window.open(qrCode.qrCodeImageUri, '_blank', 'width=600,height=700');
    }
  };

  const startPolling = (qrCodeValue: string) => {
    setPolling(true);
    const interval = setInterval(async () => {
      try {
        const res = await api.get<{ success: boolean; data: StatusData }>(
          `/api/ilink/login/status?qrId=${encodeURIComponent(qrCodeValue)}`
        );
        if (res.success && res.data) {
          setStatus(res.data.status);

          if (res.data.status === 'Confirmed' && res.data.credentials) {
            clearInterval(interval);
            setPolling(false);
            await saveCredentials(res.data.credentials);
          } else if (res.data.status === 'Expired') {
            clearInterval(interval);
            setPolling(false);
          }
        }
      } catch (error) {
        console.error('Failed to query status:', error);
      }
    }, 2000);

    return () => clearInterval(interval);
  };

  const saveCredentials = async (credentials: StatusData['credentials']) => {
    if (!credentials) return;

    try {
      await api.post('/api/ilink/login/save', credentials);
      onSuccess();
      onOpenChange(false);
    } catch (error) {
      console.error('Failed to save credentials:', error);
    }
  };

  const getStatusText = () => {
    switch (status) {
      case 'Wait':
        return t('channel.ilinkLoginWait');
      case 'Scanned':
        return t('channel.ilinkLoginScanned');
      case 'Confirmed':
        return t('channel.ilinkLoginConfirmed');
      case 'Expired':
        return t('channel.ilinkLoginExpired');
      default:
        return '';
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

              {qrCode && status !== 'Expired' && (
                <>
                  <div style={{ textAlign: 'center', fontSize: '1.1rem' }}>
                    <p>{getStatusText()}</p>
                    {polling && <Spinner size="sm" style={{ marginTop: '0.5rem' }} />}
                  </div>
                  <div style={{ marginTop: '1rem' }}>
                    <Button onPress={openQrCodeInNewWindow}>
                      {t('channel.ilinkLoginReopen')}
                    </Button>
                  </div>
                  <p style={{ fontSize: '0.9rem', color: '#666', marginTop: '0.5rem' }}>
                    {t('channel.ilinkLoginHint')}
                  </p>
                </>
              )}

              {status === 'Expired' && (
                <div style={{ textAlign: 'center' }}>
                  <p style={{ marginBottom: '1rem' }}>{getStatusText()}</p>
                  <Button onPress={createQrCode}>
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
