import { Card, Button, Spinner, TextField, Label, Input, Separator, Chip } from '@heroui/react';
import { useI18n } from '../../i18n';
import type { ConfigData } from './types';

interface GeneralTabProps {
  config: ConfigData;
  editModel: string;
  editBaseUrl: string;
  editMaxTokens: string;
  editMessageLimit: string;
  editMessageSplitDelimiters: string;
  onEditModel: (v: string) => void;
  onEditBaseUrl: (v: string) => void;
  onEditMaxTokens: (v: string) => void;
  onEditMessageLimit: (v: string) => void;
  onEditMessageSplitDelimiters: (v: string) => void;
  saving: boolean;
  restarting: boolean;
  onSave: () => void;
  onSaveAndRestart: () => void;
}

function GeneralTab({
  config, editModel, editBaseUrl, editMaxTokens, editMessageLimit, editMessageSplitDelimiters,
  onEditModel, onEditBaseUrl, onEditMaxTokens, onEditMessageLimit, onEditMessageSplitDelimiters,
  saving, restarting, onSave, onSaveAndRestart,
}: GeneralTabProps) {
  const { t } = useI18n();

  return (
    <Card className="mt-4">
      <Card.Header>
        <Card.Title>{t('settings.general')}</Card.Title>
      </Card.Header>
      <Card.Content>
        <div className="space-y-4">
          <TextField value={editModel} onChange={onEditModel}>
            <Label>{t('settings.model')}</Label>
            <Input />
          </TextField>
          <TextField value={editBaseUrl} onChange={onEditBaseUrl}>
            <Label>{t('settings.baseUrl')}</Label>
            <Input />
          </TextField>

          <Separator />

          <h3 className="font-semibold">{t('dashboard.serverConfig')}</h3>
          <TextField isDisabled value={config.server.listenUrl}>
            <Label>{t('settings.listenUrl')}</Label>
            <Input />
          </TextField>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <TextField value={editMaxTokens} onChange={onEditMaxTokens}>
              <Label>{t('settings.maxTokens')}</Label>
              <Input />
            </TextField>
            <TextField value={editMessageLimit} onChange={onEditMessageLimit}>
              <Label>{t('settings.messageLimit')}</Label>
              <Input />
            </TextField>
          </div>
          <TextField value={editMessageSplitDelimiters} onChange={onEditMessageSplitDelimiters}>
            <Label>Message Split Delimiters</Label>
            <Input placeholder="。|！|？|?|\n\n|\n" />
            <p className="text-xs text-gray-500 mt-1">
              Use | to separate multiple delimiters. Messages will be split by these delimiters before sending.
            </p>
          </TextField>

          <Separator />

          <h3 className="font-semibold">{t('settings.allowedUsers')}</h3>
          <div className="flex flex-wrap gap-2">
            {config.channel?.telegram?.allowedUsers?.map(userId => (
              <Chip key={userId} size="sm" variant="soft">{userId}</Chip>
            )) || <span className="text-sm text-gray-500">No allowed users configured</span>}
          </div>

          <Separator />

          <div className="flex gap-3">
            <Button onPress={onSave} isDisabled={saving || restarting}>
              {saving ? <Spinner size="sm" className="mr-2" /> : null}
              {t('gateway.saveConfig')}
            </Button>
            <Button variant="danger" onPress={onSaveAndRestart} isDisabled={saving || restarting}>
              {restarting ? <Spinner size="sm" className="mr-2" /> : null}
              {restarting ? t('gateway.restarting') : t('gateway.saveAndRestart')}
            </Button>
          </div>
        </div>
      </Card.Content>
    </Card>
  );
}

export default GeneralTab;
