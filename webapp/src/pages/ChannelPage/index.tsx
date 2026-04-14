import { useEffect, useState } from 'react';
import { Card, Button, Spinner, Switch, TextField, Label, Input, Chip, Separator, toast } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';

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
    botToken: string;
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
  const [botToken, setBotToken] = useState('');

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
        setBotToken(d.telegram?.botToken ?? '');
        setWsClients(d.webSocket?.connectedClients ?? 0);
        setWsStatus(d.webSocket?.status ?? '');
      }
    } catch (e) {
      console.error('Failed to load channels config:', e);
    } finally {
      setLoading(false);
    }
  };

  const saveChannels = async () => {
    setSaving(true);
    try {
      await api.put('/api/config/channels', {
        iLinkEnabled: iLinkEnabled,
        iLinkToken: iLinkToken,
        iLinkWebSocketUrl: iLinkWsUrl,
        iLinkSendUrl: iLinkSendUrl,
        iLinkWebhookPath: iLinkWebhookPath,
        iLinkDefaultUserId: iLinkDefaultUserId,
        botToken: botToken.includes('****') ? undefined : botToken,
      });
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
      await api.put('/api/config/channels', {
        iLinkEnabled: iLinkEnabled,
        iLinkToken: iLinkToken,
        iLinkWebSocketUrl: iLinkWsUrl,
        iLinkSendUrl: iLinkSendUrl,
        iLinkWebhookPath: iLinkWebhookPath,
        iLinkDefaultUserId: iLinkDefaultUserId,
        botToken: botToken.includes('****') ? undefined : botToken,
      });
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
          {/* ILink Card */}
          <Card>
            <Card.Header>
              <div className="flex items-center justify-between w-full">
                <Card.Title>{t('channel.ilink')}</Card.Title>
                <Switch
                  isSelected={iLinkEnabled}
                  onChange={() => setILinkEnabled(!iLinkEnabled)}
                >
                  <Switch.Control>
                    <Switch.Thumb />
                  </Switch.Control>
                  <Switch.Content>
                    <Label>{t('channel.enabled')}</Label>
                  </Switch.Content>
                </Switch>
              </div>
            </Card.Header>
            <Card.Content>
              <div className="space-y-4">
                <TextField value={iLinkToken} onChange={setILinkToken}>
                  <Label>{t('channel.token')}</Label>
                  <Input />
                </TextField>
                <TextField value={iLinkWsUrl} onChange={setILinkWsUrl}>
                  <Label>{t('channel.wsUrl')}</Label>
                  <Input />
                </TextField>
                <TextField value={iLinkSendUrl} onChange={setILinkSendUrl}>
                  <Label>{t('channel.sendUrl')}</Label>
                  <Input />
                </TextField>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <TextField value={iLinkWebhookPath} onChange={setILinkWebhookPath}>
                    <Label>{t('channel.webhookPath')}</Label>
                    <Input />
                  </TextField>
                  <TextField value={iLinkDefaultUserId} onChange={setILinkDefaultUserId}>
                    <Label>{t('channel.defaultUserId')}</Label>
                    <Input />
                  </TextField>
                </div>
              </div>
            </Card.Content>
          </Card>

          {/* Telegram Card */}
          <Card>
            <Card.Header>
              <Card.Title>{t('channel.telegram')}</Card.Title>
            </Card.Header>
            <Card.Content>
              <TextField value={botToken} onChange={setBotToken}>
                <Label>{t('channel.botToken')}</Label>
                <Input />
              </TextField>
            </Card.Content>
          </Card>

          {/* WebSocket Card */}
          <Card>
            <Card.Header>
              <Card.Title>{t('channel.websocket')}</Card.Title>
            </Card.Header>
            <Card.Content>
              <div className="flex items-center gap-4">
                <div className="flex items-center gap-2">
                  <span className="text-sm text-gray-500">{t('channel.status')}:</span>
                  <Chip size="sm" color="success" variant="soft">{wsStatus}</Chip>
                </div>
                <div className="flex items-center gap-2">
                  <span className="text-sm text-gray-500">{t('channel.connectedClients')}:</span>
                  <Chip size="sm" variant="soft">{wsClients}</Chip>
                </div>
              </div>
            </Card.Content>
          </Card>

          <Separator />

          {/* Action buttons */}
          <div className="flex gap-3">
            <Button
              onPress={saveChannels}
              isDisabled={saving || restarting}
            >
              {saving ? <Spinner size="sm" className="mr-2" /> : null}
              {t('channel.saveChannels')}
            </Button>
            <Button
              variant="danger"
              onPress={saveAndRestart}
              isDisabled={saving || restarting}
            >
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
