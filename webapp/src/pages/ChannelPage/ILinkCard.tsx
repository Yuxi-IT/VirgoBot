import { Card, Switch, TextField, Label, Input, Button } from '@heroui/react';
import { useI18n } from '../../i18n';

interface ILinkCardProps {
  enabled: boolean;
  token: string;
  wsUrl: string;
  sendUrl: string;
  webhookPath: string;
  defaultUserId: string;
  onEnabledChange: (v: boolean) => void;
  onTokenChange: (v: string) => void;
  onWsUrlChange: (v: string) => void;
  onSendUrlChange: (v: string) => void;
  onWebhookPathChange: (v: string) => void;
  onDefaultUserIdChange: (v: string) => void;
  onQrLogin: () => void;
}

function ILinkCard({
  enabled, token, wsUrl, sendUrl, webhookPath, defaultUserId,
  onEnabledChange, onTokenChange, onWsUrlChange, onSendUrlChange,
  onWebhookPathChange, onDefaultUserIdChange, onQrLogin,
}: ILinkCardProps) {
  const { t } = useI18n();

  return (
    <Card>
      <Card.Header>
        <div className="flex items-center justify-between w-full">
          <Card.Title>{t('channel.ilink')}</Card.Title>
          <div className="flex items-center gap-3">
            <Button size="sm" onPress={onQrLogin}>
              {t('channel.ilinkQrLogin')}
            </Button>
            <Switch
              isSelected={enabled}
              onChange={() => onEnabledChange(!enabled)}
            >
              <Switch.Control>
                <Switch.Thumb />
              </Switch.Control>
              <Switch.Content>
                <Label>{t('channel.enabled')}</Label>
              </Switch.Content>
            </Switch>
          </div>
        </div>
      </Card.Header>
      <Card.Content>
        <div className="space-y-4">
          <TextField value={token} onChange={onTokenChange}>
            <Label>{t('channel.token')}</Label>
            <Input />
          </TextField>
          <TextField value={wsUrl} onChange={onWsUrlChange}>
            <Label>{t('channel.wsUrl')}</Label>
            <Input />
          </TextField>
          <TextField value={sendUrl} onChange={onSendUrlChange}>
            <Label>{t('channel.sendUrl')}</Label>
            <Input />
          </TextField>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <TextField value={webhookPath} onChange={onWebhookPathChange}>
              <Label>{t('channel.webhookPath')}</Label>
              <Input />
            </TextField>
            <TextField value={defaultUserId} onChange={onDefaultUserIdChange}>
              <Label>{t('channel.defaultUserId')}</Label>
              <Input />
            </TextField>
          </div>
        </div>
      </Card.Content>
    </Card>
  );
}

export default ILinkCard;
