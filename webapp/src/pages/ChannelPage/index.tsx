import { useEffect, useState } from 'react';
import { Button, Spinner, Separator, toast } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import ILinkCard from './ILinkCard';
import TelegramCard from './TelegramCard';
import EmailCard from './EmailCard';
import WebSocketCard from './WebSocketCard';

interface ChannelsData {
  iLink: {
    enabled: boolean;
    token: string;
    webSocketUrl: string;
    sendUrl: string;
    webhookPath: string;
    defaultUserId: string;
  };
  telegram: {
    enabled: boolean;
    botToken: string;
    allowedUsers: number[];
  };
  email: {
    enabled: boolean;
    imapHost: string;
    imapPort: number;
    smtpHost: string;
    smtpPort: number;
    address: string;
    password: string;
  };
  webSocket: {
    connectedClients: number;
    status: string;
  };
}

interface ChannelsResponse {
  success: boolean;
  data: ChannelsData;
}

function ChannelPage() {
  const { t } = useI18n();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [restarting, setRestarting] = useState(false);

  // ILink fields
  const [iLinkEnabled, setILinkEnabled] = useState(false);
  const [iLinkToken, setILinkToken] = useState('');
  const [iLinkWsUrl, setILinkWsUrl] = useState('');
  const [iLinkSendUrl, setILinkSendUrl] = useState('');
  const [iLinkWebhookPath, setILinkWebhookPath] = useState('');
  const [iLinkDefaultUserId, setILinkDefaultUserId] = useState('');

  // Telegram
  const [telegramEnabled, setTelegramEnabled] = useState(false);
  const [botToken, setBotToken] = useState('');
  const [allowedUsers, setAllowedUsers] = useState<number[]>([]);

  // Email
  const [emailEnabled, setEmailEnabled] = useState(false);
  const [imapHost, setImapHost] = useState('');
  const [imapPort, setImapPort] = useState(993);
  const [smtpHost, setSmtpHost] = useState('');
  const [smtpPort, setSmtpPort] = useState(587);
  const [emailAddress, setEmailAddress] = useState('');
  const [emailPassword, setEmailPassword] = useState('');

  // WebSocket (read-only)
  const [wsClients, setWsClients] = useState(0);
  const [wsStatus, setWsStatus] = useState('');

  useEffect(() => {
    loadChannels();
  }, []);

  const loadChannels = async () => {
    try {
      setLoading(true);
      const res = await api.get<ChannelsResponse>('/api/config/channels');
      if (res.success && res.data) {
        const d = res.data;
        setILinkEnabled(d.iLink?.enabled ?? false);
        setILinkToken(d.iLink?.token ?? '');
        setILinkWsUrl(d.iLink?.webSocketUrl ?? '');
        setILinkSendUrl(d.iLink?.sendUrl ?? '');
        setILinkWebhookPath(d.iLink?.webhookPath ?? '');
        setILinkDefaultUserId(d.iLink?.defaultUserId ?? '');
        setTelegramEnabled(d.telegram?.enabled ?? false);
        setBotToken(d.telegram?.botToken ?? '');
        setAllowedUsers(d.telegram?.allowedUsers ?? []);
        setEmailEnabled(d.email?.enabled ?? false);
        setImapHost(d.email?.imapHost ?? '');
        setImapPort(d.email?.imapPort ?? 993);
        setSmtpHost(d.email?.smtpHost ?? '');
        setSmtpPort(d.email?.smtpPort ?? 587);
        setEmailAddress(d.email?.address ?? '');
        setEmailPassword(d.email?.password ?? '');
        setWsClients(d.webSocket?.connectedClients ?? 0);
        setWsStatus(d.webSocket?.status ?? '');
      }
    } catch (e) {
      console.error('Failed to load channels config:', e);
    } finally {
      setLoading(false);
    }
  };

  const getPayload = () => ({
    iLinkEnabled,
    iLinkToken,
    iLinkWebSocketUrl: iLinkWsUrl,
    iLinkSendUrl,
    iLinkWebhookPath,
    iLinkDefaultUserId,
    telegramEnabled,
    botToken: botToken.includes('****') ? undefined : botToken,
    allowedUsers,
    emailEnabled,
    imapHost,
    imapPort,
    smtpHost,
    smtpPort,
    emailAddress,
    emailPassword: emailPassword.includes('****') ? undefined : emailPassword,
  });

  const saveChannels = async () => {
    setSaving(true);
    try {
      await api.put('/api/config/channels', getPayload());
      toast.success(t('channel.saveSuccess'));
    } catch {
      toast.danger(t('channel.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  const saveAndRestart = async () => {
    setRestarting(true);
    try {
      await api.put('/api/config/channels', getPayload());
      await api.post('/api/gateway/restart', {});
      toast.success(t('gateway.restartSuccess'));
      await loadChannels();
    } catch {
      toast.danger(t('channel.saveFailed'));
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

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4">
        <h1 className="text-2xl font-bold mb-6">{t('channel.title')}</h1>

        <div className="space-y-4">
          <ILinkCard
            enabled={iLinkEnabled}
            token={iLinkToken}
            wsUrl={iLinkWsUrl}
            sendUrl={iLinkSendUrl}
            webhookPath={iLinkWebhookPath}
            defaultUserId={iLinkDefaultUserId}
            onEnabledChange={setILinkEnabled}
            onTokenChange={setILinkToken}
            onWsUrlChange={setILinkWsUrl}
            onSendUrlChange={setILinkSendUrl}
            onWebhookPathChange={setILinkWebhookPath}
            onDefaultUserIdChange={setILinkDefaultUserId}
          />

          <TelegramCard
            enabled={telegramEnabled}
            botToken={botToken}
            allowedUsers={allowedUsers}
            onEnabledChange={setTelegramEnabled}
            onBotTokenChange={setBotToken}
            onAllowedUsersChange={setAllowedUsers}
          />

          <EmailCard
            enabled={emailEnabled}
            imapHost={imapHost}
            imapPort={imapPort}
            smtpHost={smtpHost}
            smtpPort={smtpPort}
            address={emailAddress}
            password={emailPassword}
            onEnabledChange={setEmailEnabled}
            onImapHostChange={setImapHost}
            onImapPortChange={setImapPort}
            onSmtpHostChange={setSmtpHost}
            onSmtpPortChange={setSmtpPort}
            onAddressChange={setEmailAddress}
            onPasswordChange={setEmailPassword}
          />

          <WebSocketCard
            status={wsStatus}
            clients={wsClients}
          />

          <Separator />

          <div className="flex gap-3">
            <Button onPress={saveChannels} isDisabled={saving || restarting}>
              {saving ? <Spinner size="sm" className="mr-2" /> : null}
              {t('channel.saveChannels')}
            </Button>
            <Button variant="danger" onPress={saveAndRestart} isDisabled={saving || restarting}>
              {restarting ? <Spinner size="sm" className="mr-2" /> : null}
              {restarting ? t('gateway.restarting') : t('gateway.saveAndRestart')}
            </Button>
          </div>
        </div>
      </div>
    </DefaultLayout>
  );
}

export default ChannelPage;
