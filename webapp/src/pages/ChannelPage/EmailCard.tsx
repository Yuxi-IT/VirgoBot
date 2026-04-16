import { Card, TextField, Label, Input, Switch } from '@heroui/react';
import { useI18n } from '../../i18n';

interface EmailCardProps {
  enabled: boolean;
  imapHost: string;
  imapPort: number;
  smtpHost: string;
  smtpPort: number;
  address: string;
  password: string;
  onEnabledChange: (v: boolean) => void;
  onImapHostChange: (v: string) => void;
  onImapPortChange: (v: number) => void;
  onSmtpHostChange: (v: string) => void;
  onSmtpPortChange: (v: number) => void;
  onAddressChange: (v: string) => void;
  onPasswordChange: (v: string) => void;
}

function EmailCard({
  enabled,
  imapHost,
  imapPort,
  smtpHost,
  smtpPort,
  address,
  password,
  onEnabledChange,
  onImapHostChange,
  onImapPortChange,
  onSmtpHostChange,
  onSmtpPortChange,
  onAddressChange,
  onPasswordChange,
}: EmailCardProps) {
  const { t } = useI18n();

  return (
    <Card>
      <Card.Header>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', width: '100%' }}>
          <Card.Title>{t('channel.email')}</Card.Title>
          <Switch isSelected={enabled} onChange={() => onEnabledChange(!enabled)}>
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
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <TextField value={imapHost} onChange={onImapHostChange}>
            <Label>{t('channel.imapHost')}</Label>
            <Input />
          </TextField>

          <TextField value={imapPort.toString()} onChange={(v) => onImapPortChange(parseInt(v) || 993)}>
            <Label>{t('channel.imapPort')}</Label>
            <Input type="number" />
          </TextField>

          <TextField value={smtpHost} onChange={onSmtpHostChange}>
            <Label>{t('channel.smtpHost')}</Label>
            <Input />
          </TextField>

          <TextField value={smtpPort.toString()} onChange={(v) => onSmtpPortChange(parseInt(v) || 587)}>
            <Label>{t('channel.smtpPort')}</Label>
            <Input type="number" />
          </TextField>

          <TextField value={address} onChange={onAddressChange}>
            <Label>{t('channel.emailAddress')}</Label>
            <Input />
          </TextField>

          <TextField value={password} onChange={onPasswordChange}>
            <Label>{t('channel.emailPassword')}</Label>
            <Input type="password" />
          </TextField>
        </div>
      </Card.Content>
    </Card>
  );
}

export default EmailCard;
