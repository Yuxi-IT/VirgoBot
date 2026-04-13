import { useEffect, useState } from 'react';
import { Card, Chip, Spinner, Alert } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';

interface ChannelInfo {
  enabled: boolean;
  status: string;
  clients?: number;
}

interface StatusData {
  botName: string;
  model: string;
  uptime: string;
  startTime: string;
  connectedClients: number;
  channels: Record<string, ChannelInfo>;
  server: {
    listenUrl: string;
    maxTokens: number;
    messageLimit: number;
  };
}

interface ApiResponse {
  success: boolean;
  data: StatusData;
}

function DashboardPage() {
  const { t } = useI18n();
  const [status, setStatus] = useState<StatusData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadStatus();
  }, []);

  const loadStatus = async () => {
    try {
      setLoading(true);
      setError(null);
      const res = await api.get<ApiResponse>('/api/status');
      if (res.success) {
        setStatus(res.data);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setLoading(false);
    }
  };

  const getChannelChip = (channelStatus: string) => {
    switch (channelStatus) {
      case 'running':
        return <Chip color="success" size="sm">{t('dashboard.running')}</Chip>;
      case 'monitoring':
        return <Chip color="warning" size="sm">{t('dashboard.monitoring')}</Chip>;
      case 'disabled':
        return <Chip color="danger" size="sm">{t('dashboard.disabled')}</Chip>;
      default:
        return <Chip color="default" size="sm">{channelStatus}</Chip>;
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
        <h1 className="text-2xl font-bold mb-6">{t('dashboard.title')}</h1>

        {/* System Status */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 mb-6">
          <Card>
            <Card.Header>
              <Card.Title>{t('dashboard.botName')}</Card.Title>
            </Card.Header>
            <Card.Content>
              <p className="text-2xl font-bold">{status?.botName}</p>
            </Card.Content>
          </Card>

          <Card>
            <Card.Header>
              <Card.Title>{t('dashboard.model')}</Card.Title>
            </Card.Header>
            <Card.Content>
              <p className="text-2xl font-bold">{status?.model}</p>
            </Card.Content>
          </Card>

          <Card>
            <Card.Header>
              <Card.Title>{t('dashboard.uptime')}</Card.Title>
            </Card.Header>
            <Card.Content>
              <p className="text-2xl font-bold">{status?.uptime}</p>
            </Card.Content>
          </Card>

          <Card>
            <Card.Header>
              <Card.Title>{t('dashboard.startTime')}</Card.Title>
            </Card.Header>
            <Card.Content>
              <p className="text-lg">{status?.startTime ? new Date(status.startTime).toLocaleString() : '-'}</p>
            </Card.Content>
          </Card>

          <Card>
            <Card.Header>
              <Card.Title>{t('dashboard.connectedClients')}</Card.Title>
            </Card.Header>
            <Card.Content>
              <p className="text-2xl font-bold">{status?.connectedClients ?? 0}</p>
            </Card.Content>
          </Card>
        </div>

        {/* Channel Status */}
        <h2 className="text-xl font-semibold mb-4">{t('dashboard.channelStatus')}</h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 mb-6">
          {status?.channels && Object.entries(status.channels).map(([name, channel]) => (
            <Card key={name}>
              <Card.Header>
                <Card.Title className="capitalize">{name}</Card.Title>
              </Card.Header>
              <Card.Content>
                <div className="flex items-center justify-between">
                  <Chip color={channel.enabled ? 'success' : 'default'} size="sm" variant="soft">
                    {channel.enabled ? t('settings.enabled') : t('dashboard.disabled')}
                  </Chip>
                  {getChannelChip(channel.status)}
                </div>
                {channel.clients !== undefined && (
                  <p className="text-sm mt-2 text-gray-500">
                    {t('dashboard.connectedClients')}: {channel.clients}
                  </p>
                )}
              </Card.Content>
            </Card>
          ))}
        </div>

        {/* Server Config */}
        <h2 className="text-xl font-semibold mb-4">{t('dashboard.serverConfig')}</h2>
        <Card>
          <Card.Content>
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <div>
                <p className="text-sm text-gray-500">{t('dashboard.listenUrl')}</p>
                <p className="font-mono">{status?.server.listenUrl}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">{t('dashboard.maxTokens')}</p>
                <p className="font-bold">{status?.server.maxTokens}</p>
              </div>
              <div>
                <p className="text-sm text-gray-500">{t('dashboard.messageLimit')}</p>
                <p className="font-bold">{status?.server.messageLimit}</p>
              </div>
            </div>
          </Card.Content>
        </Card>
      </div>
    </DefaultLayout>
  );
}

export default DashboardPage;
