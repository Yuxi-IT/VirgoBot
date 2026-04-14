import { Card, Chip } from '@heroui/react';
import { useI18n } from '../../i18n';
import type { ChannelInfo } from './types';

interface ChannelStatusGridProps {
  channels: Record<string, ChannelInfo>;
}

function ChannelStatusGrid({ channels }: ChannelStatusGridProps) {
  const { t } = useI18n();

  const getChannelChip = (channelStatus: string) => {
    switch (channelStatus) {
      case 'running':
        return <Chip color="success" size="sm">{t('dashboard.running')}</Chip>;
      case 'monitoring':
        return <Chip color="warning" size="sm">{t('dashboard.monitoring')}</Chip>;
      case 'disabled':
        return <Chip color="danger" size="sm">{t('dashboard.disabled')}</Chip>;
      case 'stopped':
        return <Chip color="default" size="sm">{t('dashboard.stopped')}</Chip>;
      default:
        return <Chip color="default" size="sm">{channelStatus}</Chip>;
    }
  };

  return (
    <>
      <h2 className="text-xl font-semibold mb-4">{t('dashboard.channelStatus')}</h2>
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 mb-6">
        {Object.entries(channels).map(([name, channel]) => (
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
    </>
  );
}

export default ChannelStatusGrid;
