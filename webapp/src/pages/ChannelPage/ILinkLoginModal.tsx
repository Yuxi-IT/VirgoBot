import { useState, useEffect } from 'react';
import { Modal, Button, Spinner } from '@heroui/react';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';

interface ILinkLoginModalProps {
  isOpen: boolean;
  onClose: () => void;
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

function ILinkLoginModal({ isOpen, onClose, onSuccess }: ILinkLoginModalProps) {
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
        startPolling(res.data.qrCode);
      }
    } catch (error) {
      console.error('Failed to create QR code:', error);
    } finally {
      setLoading(false);
    }
  };

  const startPolling = (qrCodeValue: string) => {
    setPolling(true);
    const interval = setInterval(async () => {
      try {
        const res = await api.get<{ success: boolean; data: StatusData }>(
          `/api/ilink/login/status?qrcode=${encodeURIComponent(qrCodeValue)}`
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
      onClose();
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
    <Modal isOpen={isOpen} onClose={onClose}>
      <Modal.Content>
        <Modal.Header>
          <Modal.Title>{t('channel.ilinkLogin')}</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '1rem' }}>
            {loading && <Spinner size="lg" />}

            {qrCode && (
              <>
                <img
                  src={qrCode.qrCodeImageUri}
                  alt="QR Code"
                  style={{ width: '256px', height: '256px' }}
                />
                <div style={{ textAlign: 'center' }}>
                  <p>{getStatusText()}</p>
                  {polling && <Spinner size="sm" style={{ marginTop: '0.5rem' }} />}
                </div>
              </>
            )}

            {status === 'Expired' && (
              <Button onPress={createQrCode}>
                {t('channel.ilinkLoginRefresh')}
              </Button>
            )}
          </div>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="ghost" onPress={onClose}>
            {t('common.cancel')}
          </Button>
        </Modal.Footer>
      </Modal.Content>
    </Modal>
  );
}

export default ILinkLoginModal;
