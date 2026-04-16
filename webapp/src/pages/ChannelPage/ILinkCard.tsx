import { Card, Switch, TextField, Label, Input, Button } from '@heroui/react';
import { useState } from 'react';
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
  const [showToken, setShowToken] = useState(false);

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
            <div className="flex gap-2">
              <Input type={showToken ? 'text' : 'password'} className="flex-1" />
              <Button size="sm" onPress={() => setShowToken(!showToken)}>
                {showToken ? t('channel.hide') : t('channel.show')}
              </Button>
            </div>
          </TextField>
        </div>
      </Card.Content>
    </Card>
  );
}

export default ILinkCard;
