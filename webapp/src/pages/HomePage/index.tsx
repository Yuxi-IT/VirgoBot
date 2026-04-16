import { useEffect, useState } from 'react';
import { Card, Spinner, Badge } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';

interface ChannelStatus {
  name: string;
  enabled: boolean;
  status: string;
}

interface GatewayStatusData {
  isRunning: boolean;
  channels: Record<string, ChannelStatus>;
  config: {
    model: string;
    baseUrl: string;
    maxTokens: number;
    messageLimit: number;
  };
}

interface GatewayStatusResponse {
  success: boolean;
  data: GatewayStatusData;
}

function Home() {
  const { t } = useI18n();
  const [loading, setLoading] = useState(true);
  const [status, setStatus] = useState<GatewayStatusData | null>(null);

  useEffect(() => {
    loadStatus();
    const interval = setInterval(loadStatus, 5000);
    return () => clearInterval(interval);
  }, []);

  const loadStatus = async () => {
    try {
      const res = await api.get<GatewayStatusResponse>('/api/gateway/status');
      if (res.success && res.data) {
        setStatus(res.data);
      }
    } catch (error) {
      console.error('Failed to load gateway status:', error);
    } finally {
      setLoading(false);
    }
  };

  const getStatusBadge = (channelStatus: ChannelStatus) => {
    if (!channelStatus.enabled) {
      return <Badge variant="secondary">{t('dashboard.disabled')}</Badge>;
    }
    if (channelStatus.status === 'Running') {
      return <Badge variant="success">{t('dashboard.running')}</Badge>;
    }
    if (channelStatus.status === 'Stopped') {
      return <Badge variant="danger">{t('dashboard.stopped')}</Badge>;
    }
    return <Badge variant="warning">{channelStatus.status}</Badge>;
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

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4">
        <h1 className="text-2xl font-bold mb-6">{t('dashboard.title')}</h1>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <Card>
            <Card.Header>
              <Card.Title>{t('dashboard.systemStatus')}</Card.Title>
            </Card.Header>
            <Card.Content>
              <div className="space-y-2">
                <div className="flex justify-between">
                  <span>{t('dashboard.model')}:</span>
                  <span className="font-mono">{status?.config.model}</span>
                </div>
                <div className="flex justify-between">
                  <span>{t('dashboard.maxTokens')}:</span>
                  <span>{status?.config.maxTokens}</span>
                </div>
                <div className="flex justify-between">
                  <span>{t('dashboard.messageLimit')}:</span>
                  <span>{status?.config.messageLimit}</span>
                </div>
              </div>
            </Card.Content>
          </Card>

          <Card>
            <Card.Header>
              <Card.Title>{t('dashboard.channelStatus')}</Card.Title>
            </Card.Header>
            <Card.Content>
              <div className="space-y-3">
                {status?.channels && Object.entries(status.channels).map(([key, channel]) => (
                  <div key={key} className="flex justify-between items-center">
                    <span>{channel.name}</span>
                    {getStatusBadge(channel)}
                  </div>
                ))}
              </div>
            </Card.Content>
          </Card>
        </div>
      </div>
    </DefaultLayout>
  );
}

export default Home;


