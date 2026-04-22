import { Card, Button, Spinner, TextField, Label, Input, Switch, Separator } from '@heroui/react';
import { useI18n } from '../../i18n';
import type { ConfigData } from './types';

interface EmailTabProps {
  config: ConfigData;
  editImapHost: string;
  editEmailAddress: string;
  onEditImapHost: (v: string) => void;
  onEditEmailAddress: (v: string) => void;
  restarting: boolean;
  onSaveAndRestart: () => void;
}

function EmailTab({
  config, editImapHost, editEmailAddress,
  onEditImapHost, onEditEmailAddress,
  restarting, onSaveAndRestart,
}: EmailTabProps) {
  const { t } = useI18n();

  return (
    <Card className="mt-4">
      <Card.Header>
        <Card.Title>{t('settings.email')}</Card.Title>
      </Card.Header>
      <Card.Content>
        <div className="space-y-4">
          <TextField value={editImapHost} onChange={onEditImapHost}>
            <Label>{t('settings.imapHost')}</Label>
            <Input />
          </TextField>
          <TextField value={editEmailAddress} onChange={onEditEmailAddress}>
            <Label>{t('settings.emailAddress')}</Label>
            <Input />
          </TextField>
          <TextField isDisabled value={config.channel.email.password}>
            <Label>Email Password</Label>
            <Input />
          </TextField>
          <div className="flex items-center gap-4">
            <Switch isSelected={config.channel.email.enabled} isDisabled>
              <Switch.Control>
                <Switch.Thumb />
              </Switch.Control>
              <Switch.Content>
                <Label>{t('settings.enabled')}</Label>
              </Switch.Content>
            </Switch>
          </div>

          <Separator />

          <div className="flex gap-3">
            <Button onPress={onSaveAndRestart} isDisabled={restarting}>
              {restarting ? <Spinner size="sm" className="mr-2" /> : null}
              {restarting ? t('gateway.restarting') : t('gateway.saveConfig')}
            </Button>
          </div>
        </div>
      </Card.Content>
    </Card>
  );
}

export default EmailTab;
