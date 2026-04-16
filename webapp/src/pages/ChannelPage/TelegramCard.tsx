import { Card, TextField, Label, Input, Switch } from '@heroui/react';
import { useI18n } from '../../i18n';

interface TelegramCardProps {
  enabled: boolean;
  botToken: string;
  allowedUsers: number[];
  onEnabledChange: (v: boolean) => void;
  onBotTokenChange: (v: string) => void;
  onAllowedUsersChange: (v: number[]) => void;
}

function TelegramCard({ enabled, botToken, allowedUsers, onEnabledChange, onBotTokenChange, onAllowedUsersChange }: TelegramCardProps) {
  const { t } = useI18n();

  return (
    <Card>
      <Card.Header>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', width: '100%' }}>
          <Card.Title>{t('channel.telegram')}</Card.Title>
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
          <TextField value={botToken} onChange={onBotTokenChange}>
            <Label>{t('channel.botToken')}</Label>
            <Input />
          </TextField>

          <TextField
            value={allowedUsers.join(', ')}
            onChange={(v) => {
              const users = v.split(',').map(s => parseInt(s.trim())).filter(n => !isNaN(n));
              onAllowedUsersChange(users);
            }}
          >
            <Label>{t('channel.allowedUsers')}</Label>
            <Input placeholder={t('channel.allowedUsersHint')} />
          </TextField>
        </div>
      </Card.Content>
    </Card>
  );
}

export default TelegramCard;
