import { Card, TextField, Label, Input } from '@heroui/react';
import { useI18n } from '../../i18n';

interface TelegramCardProps {
  botToken: string;
  onBotTokenChange: (v: string) => void;
}

function TelegramCard({ botToken, onBotTokenChange }: TelegramCardProps) {
  const { t } = useI18n();

  return (
    <Card>
      <Card.Header>
        <Card.Title>{t('channel.telegram')}</Card.Title>
      </Card.Header>
      <Card.Content>
        <TextField value={botToken} onChange={onBotTokenChange}>
          <Label>{t('channel.botToken')}</Label>
          <Input />
        </TextField>
      </Card.Content>
    </Card>
  );
}

export default TelegramCard;
