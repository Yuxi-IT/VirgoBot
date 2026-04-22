import { useEffect, useState } from 'react';
import { Tabs, Spinner, toast } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import GeneralTab from './GeneralTab';
import RuleTab from './RuleTab';
import VoiceTab from './VoiceTab';
import LogsTab from './LogsTab';
import type { ConfigData, ConfigResponse, VoiceConfigResponse } from './types';

function SettingsPage() {
  const { t } = useI18n();
  const [config, setConfig] = useState<ConfigData | null>(null);
  const [loading, setLoading] = useState(true);
  const [restarting, setRestarting] = useState(false);
  const [activeTab, setActiveTab] = useState<string>('general');

  // Editable field states
  const [editModel, setEditModel] = useState('');
  const [editBaseUrl, setEditBaseUrl] = useState('');
  const [editApiStandard, setEditApiStandard] = useState('OpenAI');
  const [editMaxTokens, setEditMaxTokens] = useState('');
  const [editMessageLimit, setEditMessageLimit] = useState('');
  const [editMessageSplitDelimiters, setEditMessageSplitDelimiters] = useState('');
  const [editAutoResponseEnabled, setEditAutoResponseEnabled] = useState(false);
  const [editAutoResponseMinIdle, setEditAutoResponseMinIdle] = useState('30');
  const [editAutoResponseMaxIdle, setEditAutoResponseMaxIdle] = useState('120');
  const [editImapHost, setEditImapHost] = useState('');
  const [editEmailAddress, setEditEmailAddress] = useState('');

  // Voice config states
  const [editVoiceApiKey, setEditVoiceApiKey] = useState('');
  const [editAsrResourceId, setEditAsrResourceId] = useState('volc.bigasr.auc_turbo');
  const [editTtsResourceId, setEditTtsResourceId] = useState('seed-tts-2.0');
  const [editVoiceType, setEditVoiceType] = useState('zh_female_vv_uranus_bigtts');
  const [savingVoice, setSavingVoice] = useState(false);

  useEffect(() => {
    loadConfig();
    loadVoiceConfig();
  }, []);

  const loadConfig = async () => {
    try {
      setLoading(true);
      const res = await api.get<ConfigResponse>('/api/config');
      if (res.success) {
        setConfig(res.data);
        setEditModel(res.data.model);
        setEditBaseUrl(res.data.baseUrl);
        setEditApiStandard(res.data.apiStandard ?? 'OpenAI');
        setEditMaxTokens(String(res.data.server.maxTokens));
        setEditMessageLimit(String(res.data.server.messageLimit));
        setEditMessageSplitDelimiters(res.data.server.messageSplitDelimiters);
        setEditAutoResponseEnabled(res.data.server.autoResponse.enabled);
        setEditAutoResponseMinIdle(String(res.data.server.autoResponse.minIdleMinutes));
        setEditAutoResponseMaxIdle(String(res.data.server.autoResponse.maxIdleMinutes));
        setEditImapHost(res.data.channel.email.imapHost);
        setEditEmailAddress(res.data.channel.email.address);
      }
    } catch {
      // silently fail
    } finally {
      setLoading(false);
    }
  };

  const loadVoiceConfig = async () => {
    try {
      const res = await api.get<VoiceConfigResponse>('/api/voice/config');
      if (res.success && res.data.voice) {
        setEditVoiceApiKey(res.data.voice.apiKey);
        setEditAsrResourceId(res.data.voice.asrResourceId);
        setEditTtsResourceId(res.data.voice.ttsResourceId);
        setEditVoiceType(res.data.voice.voiceType);
      }
    } catch {
      // silently fail
    }
  };

  const saveAndRestart = async () => {
    setRestarting(true);
    try {
      await api.put('/api/config', {
        model: editModel,
        baseUrl: editBaseUrl,
        apiStandard: editApiStandard,
        maxTokens: parseInt(editMaxTokens) || undefined,
        messageLimit: parseInt(editMessageLimit) || undefined,
        messageSplitDelimiters: editMessageSplitDelimiters,
        autoResponseEnabled: editAutoResponseEnabled,
        autoResponseMinIdle: parseInt(editAutoResponseMinIdle) || undefined,
        autoResponseMaxIdle: parseInt(editAutoResponseMaxIdle) || undefined,
        imapHost: editImapHost,
        emailAddress: editEmailAddress,
      });
      await api.post('/api/gateway/restart', {});
      toast.success(t('gateway.restartSuccess'));
      await loadConfig();
    } catch {
      toast.danger(t('settings.saveFailed'));
    } finally {
      setRestarting(false);
    }
  };

  const saveVoiceConfig = async () => {
    setSavingVoice(true);
    try {
      await api.put('/api/voice/config', {
        voice: {
          apiKey: editVoiceApiKey,
          asrResourceId: editAsrResourceId,
          ttsResourceId: editTtsResourceId,
          voiceType: editVoiceType,
        },
      });
      toast.success(t('gateway.configSaved'));
      await loadVoiceConfig();
    } catch {
      toast.danger(t('settings.saveFailed'));
    } finally {
      setSavingVoice(false);
    }
  };

  if (loading) {
    return (
      <DefaultLayout>
        <div className="flex items-center justify-center h-[60vh]">
          <Spinner size="lg" />
        </div>
      </DefaultLayout>
    );
  }

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4">
        <h1 className="text-2xl font-bold mb-6">{t('settings.title')}</h1>

        <Tabs onSelectionChange={(key) => setActiveTab(String(key))}>
          <Tabs.ListContainer>
            <Tabs.List aria-label="Settings tabs">
              <Tabs.Tab id="general">
                {t('settings.general')}
                <Tabs.Indicator />
              </Tabs.Tab>
              <Tabs.Tab id="rule">
                {t('settings.rule')}
                <Tabs.Indicator />
              </Tabs.Tab>
              <Tabs.Tab id="voice">
                {t('settings.voice.tab')}
                <Tabs.Indicator />
              </Tabs.Tab>
              <Tabs.Tab id="logs">
                {t('logs.title')}
                <Tabs.Indicator />
              </Tabs.Tab>
            </Tabs.List>
          </Tabs.ListContainer>

          <Tabs.Panel id="general">
            {config && (
              <GeneralTab
                config={config}
                editModel={editModel}
                editBaseUrl={editBaseUrl}
                editApiStandard={editApiStandard}
                editMaxTokens={editMaxTokens}
                editMessageLimit={editMessageLimit}
                editMessageSplitDelimiters={editMessageSplitDelimiters}
                editAutoResponseEnabled={editAutoResponseEnabled}
                editAutoResponseMinIdle={editAutoResponseMinIdle}
                editAutoResponseMaxIdle={editAutoResponseMaxIdle}
                onEditModel={setEditModel}
                onEditBaseUrl={setEditBaseUrl}
                onEditApiStandard={setEditApiStandard}
                onEditMaxTokens={setEditMaxTokens}
                onEditMessageLimit={setEditMessageLimit}
                onEditMessageSplitDelimiters={setEditMessageSplitDelimiters}
                onEditAutoResponseEnabled={setEditAutoResponseEnabled}
                onEditAutoResponseMinIdle={setEditAutoResponseMinIdle}
                onEditAutoResponseMaxIdle={setEditAutoResponseMaxIdle}
                restarting={restarting}
                onSaveAndRestart={saveAndRestart}
              />
            )}
          </Tabs.Panel>

          <Tabs.Panel id="rule">
            <RuleTab active={activeTab === 'rule'} />
          </Tabs.Panel>

          <Tabs.Panel id="voice">
            <VoiceTab
              editApiKey={editVoiceApiKey}
              editAsrResourceId={editAsrResourceId}
              editTtsResourceId={editTtsResourceId}
              editVoiceType={editVoiceType}
              onEditApiKey={setEditVoiceApiKey}
              onEditAsrResourceId={setEditAsrResourceId}
              onEditTtsResourceId={setEditTtsResourceId}
              onEditVoiceType={setEditVoiceType}
              saving={savingVoice}
              onSave={saveVoiceConfig}
            />
          </Tabs.Panel>

          <Tabs.Panel id="logs">
            <LogsTab active={activeTab === 'logs'} />
          </Tabs.Panel>
        </Tabs>
      </div>
    </DefaultLayout>
  );
}

export default SettingsPage;
