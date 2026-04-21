import { Card, Button, Spinner, TextField, Label, Input, Separator, Chip, Switch, Select, ListBox } from '@heroui/react';
import { useI18n } from '../../i18n';
import type { ConfigData } from './types';

interface GeneralTabProps {
  config: ConfigData;
  editModel: string;
  editBaseUrl: string;
  editApiStandard: string;
  editMaxTokens: string;
  editMessageLimit: string;
  editMessageSplitDelimiters: string;
  editAutoResponseEnabled: boolean;
  editAutoResponseMinIdle: string;
  editAutoResponseMaxIdle: string;
  onEditModel: (v: string) => void;
  onEditBaseUrl: (v: string) => void;
  onEditApiStandard: (v: string) => void;
  onEditMaxTokens: (v: string) => void;
  onEditMessageLimit: (v: string) => void;
  onEditMessageSplitDelimiters: (v: string) => void;
  onEditAutoResponseEnabled: (v: boolean) => void;
  onEditAutoResponseMinIdle: (v: string) => void;
  onEditAutoResponseMaxIdle: (v: string) => void;
  saving: boolean;
  restarting: boolean;
  onSave: () => void;
  onSaveAndRestart: () => void;
}

function GeneralTab({
  config, editModel, editBaseUrl, editApiStandard, editMaxTokens, editMessageLimit, editMessageSplitDelimiters,
  editAutoResponseEnabled, editAutoResponseMinIdle, editAutoResponseMaxIdle,
  onEditModel, onEditBaseUrl, onEditApiStandard, onEditMaxTokens, onEditMessageLimit, onEditMessageSplitDelimiters,
  onEditAutoResponseEnabled, onEditAutoResponseMinIdle, onEditAutoResponseMaxIdle,
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
          <div>
            <Label>{t('settings.apiStandard')}</Label>
            <div className="mt-2">
              <Select selectedKey={editApiStandard} onSelectionChange={(key) => onEditApiStandard(String(key))}>
                <Select.Trigger>
                  <Select.Value />
                </Select.Trigger>
                <Select.Popover>
                  <ListBox>
                    <ListBox.Item id="OpenAI" textValue="OpenAI">OpenAI</ListBox.Item>
                    <ListBox.Item id="Anthropic" textValue="Anthropic">Anthropic</ListBox.Item>
                    <ListBox.Item id="Gemini" textValue="Gemini">Gemini</ListBox.Item>
                  </ListBox>
                </Select.Popover>
              </Select>
            </div>
            <p className="text-xs text-gray-500 mt-1">{t('settings.apiStandardHint')}</p>
          </div>

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

          <h3 className="font-semibold">{t('settings.autoResponse')}</h3>
          <div>
            <Label>{t('settings.autoResponseEnabled')}</Label>
            <div className="mt-2">
              <Switch isSelected={editAutoResponseEnabled} onChange={onEditAutoResponseEnabled} />
            </div>
            <p className="text-xs text-gray-500 mt-1">
              {t('settings.autoResponseHint')}
            </p>
          </div>
          {editAutoResponseEnabled && (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <TextField value={editAutoResponseMinIdle} onChange={onEditAutoResponseMinIdle}>
                <Label>{t('settings.minIdleMinutes')}</Label>
                <Input type="number" min="1" />
              </TextField>
              <TextField value={editAutoResponseMaxIdle} onChange={onEditAutoResponseMaxIdle}>
                <Label>{t('settings.maxIdleMinutes')}</Label>
                <Input type="number" min="1" />
              </TextField>
            </div>
          )}

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
