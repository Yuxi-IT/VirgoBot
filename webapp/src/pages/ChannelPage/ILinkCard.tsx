import { Card, Switch, TextField, Label, Input, Button } from '@heroui/react';
import { useI18n } from '../../i18n';

interface ILinkCardProps {
  enabled: boolean;
  token: string;
  onEnabledChange: (v: boolean) => void;
  onTokenChange: (v: string) => void;
  onQrLogin: () => void;
}

function ILinkCard({
  enabled, token,
  onEnabledChange, onTokenChange, onQrLogin,
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
        </div>
      </Card.Content>
    </Card>
  );
}

export default ILinkCard;
