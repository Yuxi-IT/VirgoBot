import { Card } from '@heroui/react';
import { useI18n } from '../../i18n';
import type { StatusData } from './types';

interface StatusCardsProps {
  status: StatusData;
}

function StatusCards({ status }: StatusCardsProps) {
  const { t } = useI18n();

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 mb-6">
      <Card>
        <Card.Header>
          <Card.Title>{t('dashboard.botName')}</Card.Title>
        </Card.Header>
        <Card.Content>
          <p className="text-2xl font-bold">{status.botName}</p>
        </Card.Content>
      </Card>

      <Card>
        <Card.Header>
          <Card.Title>{t('dashboard.model')}</Card.Title>
        </Card.Header>
        <Card.Content>
          <p className="text-2xl font-bold">{status.model}</p>
        </Card.Content>
      </Card>

      <Card>
        <Card.Header>
          <Card.Title>{t('dashboard.uptime')}</Card.Title>
        </Card.Header>
        <Card.Content>
          <p className="text-2xl font-bold">{status.uptime}</p>
        </Card.Content>
      </Card>

      <Card>
        <Card.Header>
          <Card.Title>{t('dashboard.startTime')}</Card.Title>
        </Card.Header>
        <Card.Content>
          <p className="text-lg">{status.startTime ? new Date(status.startTime).toLocaleString() : '-'}</p>
        </Card.Content>
      </Card>

      <Card>
        <Card.Header>
          <Card.Title>{t('dashboard.connectedClients')}</Card.Title>
        </Card.Header>
        <Card.Content>
          <p className="text-2xl font-bold">{status.connectedClients ?? 0}</p>
        </Card.Content>
      </Card>

      {status.tokenStats && (
        <>
          <Card>
            <Card.Header>
              <Card.Title>{t('dashboard.promptTokens')}</Card.Title>
            </Card.Header>
            <Card.Content>
              <p className="text-2xl font-bold">{status.tokenStats.promptTokens.toLocaleString()}</p>
            </Card.Content>
          </Card>

          <Card>
            <Card.Header>
              <Card.Title>{t('dashboard.completionTokens')}</Card.Title>
            </Card.Header>
            <Card.Content>
              <p className="text-2xl font-bold">{status.tokenStats.completionTokens.toLocaleString()}</p>
            </Card.Content>
          </Card>

          <Card>
            <Card.Header>
              <Card.Title>{t('dashboard.totalTokens')}</Card.Title>
            </Card.Header>
            <Card.Content>
              <p className="text-2xl font-bold">{status.tokenStats.totalTokens.toLocaleString()}</p>
            </Card.Content>
          </Card>

        </>
      )}
    </div>
  );
}

export default StatusCards;
