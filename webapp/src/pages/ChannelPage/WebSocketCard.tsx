import { Card, Chip } from '@heroui/react';
import { useI18n } from '../../i18n';

interface WebSocketCardProps {
  status: string;
  clients: number;
}

function WebSocketCard({ status, clients }: WebSocketCardProps) {
  const { t } = useI18n();

  return (
    <Card>
      <Card.Header>
        <Card.Title>{t('channel.websocket')}</Card.Title>
      </Card.Header>
      <Card.Content>
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-2">
            <span className="text-sm text-gray-500">{t('channel.status')}:</span>
            <Chip size="sm" color="success" variant="soft">{status}</Chip>
          </div>
          <div className="flex items-center gap-2">
            <span className="text-sm text-gray-500">{t('channel.connectedClients')}:</span>
            <Chip size="sm" variant="soft">{clients}</Chip>
          </div>
        </div>
      </Card.Content>
    </Card>
  );
}

export default WebSocketCard;
