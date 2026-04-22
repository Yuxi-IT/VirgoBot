import { Card } from '@heroui/react';
import { useI18n } from '../../i18n';

interface ServerConfigCardProps {
  server: {
    listenUrl: string;
    maxTokens: number;
    messageLimit: number;
  };
}

function ServerConfigCard({ server }: ServerConfigCardProps) {
  const { t } = useI18n();

  return (
    <>
      <h2 className="text-xl font-semibold mb-4">{t('dashboard.serverConfig')}</h2>
      <Card>
        <Card.Content>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div>
              <p className="text-sm text-default-500">{t('dashboard.listenUrl')}</p>
              <p className="font-mono">{server.listenUrl}</p>
            </div>
            <div>
              <p className="text-sm text-default-500">{t('dashboard.maxTokens')}</p>
              <p className="font-bold">{server.maxTokens}</p>
            </div>
            <div>
              <p className="text-sm text-default-500">{t('dashboard.messageLimit')}</p>
              <p className="font-bold">{server.messageLimit}</p>
            </div>
          </div>
        </Card.Content>
      </Card>
    </>
  );
}

export default ServerConfigCard;
