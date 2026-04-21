import { Card, TextField, Label, Input, Button, Separator, Spinner } from '@heroui/react';
import { useI18n } from '../../i18n';

interface VoiceTabProps {
  editApiKey: string;
  editAsrResourceId: string;
  editTtsResourceId: string;
  editVoiceType: string;
  onEditApiKey: (v: string) => void;
  onEditAsrResourceId: (v: string) => void;
  onEditTtsResourceId: (v: string) => void;
  onEditVoiceType: (v: string) => void;
  saving: boolean;
  onSave: () => void;
}

function VoiceTab({
  editApiKey,
  editAsrResourceId,
  editTtsResourceId,
  editVoiceType,
  onEditApiKey,
  onEditAsrResourceId,
  onEditTtsResourceId,
  onEditVoiceType,
  saving,
  onSave,
}: VoiceTabProps) {
  const { t } = useI18n();

  return (
    <div className="mt-4 space-y-4">
      <Card className="p-6">
        <h2 className="text-xl font-semibold mb-4">{t('settings.voice.title')}</h2>
        <p className="text-sm text-gray-600 mb-4">
          {t('settings.voice.description')}
        </p>

        <div className="space-y-4">
          <TextField>
            <Label>{t('settings.voice.apiKey')}</Label>
            <Input
              type="password"
              value={editApiKey}
              onChange={(e) => onEditApiKey(e.target.value)}
              placeholder={t('settings.voice.apiKeyPlaceholder')}
            />
          </TextField>

          <Separator />

          <h3 className="text-base font-medium">{t('settings.voice.asrSection')}</h3>

          <TextField>
            <Label>{t('settings.voice.asrResourceId')}</Label>
            <Input
              value={editAsrResourceId}
              onChange={(e) => onEditAsrResourceId(e.target.value)}
              placeholder="volc.bigasr.auc_turbo"
            />
          </TextField>

          <Separator />

          <h3 className="text-base font-medium">{t('settings.voice.ttsSection')}</h3>

          <TextField>
            <Label>{t('settings.voice.ttsResourceId')}</Label>
            <Input
              value={editTtsResourceId}
              onChange={(e) => onEditTtsResourceId(e.target.value)}
              placeholder="seed-tts-2.0"
            />
          </TextField>

          <TextField>
            <Label>{t('settings.voice.voiceType')}</Label>
            <Input
              value={editVoiceType}
              onChange={(e) => onEditVoiceType(e.target.value)}
              placeholder="zh_female_vv_uranus_bigtts"
            />
          </TextField>
        </div>

        <div className="flex gap-2 mt-6">
          <Button
            onPress={onSave}
            isDisabled={saving}
            variant="primary"
          >
            {saving ? <Spinner size="sm" className="mr-2" /> : null}
            {t('settings.save')}
          </Button>
        </div>
      </Card>
    </div>
  );
}

export default VoiceTab;
