import { useEffect, useState } from 'react';
import { Card, Tabs, Button, Spinner, Switch, TextField, Label, Input, TextArea, Separator, Chip, toast } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';

interface ConfigData {
  model: string;
  baseUrl: string;
  server: {
    listenUrl: string;
    maxTokens: number;
    messageLimit: number;
  };
  email: {
    imapHost: string;
    address: string;
    enabled: boolean;
  };
  iLink: {
    enabled: boolean;
  };
  allowedUsers: number[];
}

interface ConfigResponse {
  success: boolean;
  data: ConfigData;
}

interface ContentResponse {
  success: boolean;
  data: { content: string };
}

function SettingsPage() {
  const { t } = useI18n();
  const [config, setConfig] = useState<ConfigData | null>(null);
  const [loading, setLoading] = useState(true);
  const [systemMemory, setSystemMemory] = useState('');
  const [soulContent, setSoulContent] = useState('');
  const [ruleContent, setRuleContent] = useState('');
  const [memoryLoading, setMemoryLoading] = useState(false);
  const [soulLoading, setSoulLoading] = useState(false);
  const [ruleLoading, setRuleLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [restarting, setRestarting] = useState(false);

  // Editable field states
  const [editModel, setEditModel] = useState('');
  const [editBaseUrl, setEditBaseUrl] = useState('');
  const [editMaxTokens, setEditMaxTokens] = useState('');
  const [editMessageLimit, setEditMessageLimit] = useState('');
  const [editImapHost, setEditImapHost] = useState('');
  const [editEmailAddress, setEditEmailAddress] = useState('');

  useEffect(() => {
    loadConfig();
  }, []);

  const loadConfig = async () => {
    try {
      setLoading(true);
      const res = await api.get<ConfigResponse>('/api/config');
      if (res.success) {
        setConfig(res.data);
        setEditModel(res.data.model);
        setEditBaseUrl(res.data.baseUrl);
        setEditMaxTokens(String(res.data.server.maxTokens));
        setEditMessageLimit(String(res.data.server.messageLimit));
        setEditImapHost(res.data.email.imapHost);
        setEditEmailAddress(res.data.email.address);
      }
    } catch {
      // silently fail
    } finally {
      setLoading(false);
    }
  };

  const loadSystemMemory = async () => {
    try {
      setMemoryLoading(true);
      const res = await api.get<ContentResponse>('/api/config/system-memory');
      if (res.success) {
        setSystemMemory(res.data.content);
      }
    } catch {
      // silently fail
    } finally {
      setMemoryLoading(false);
    }
  };

  const loadSoul = async () => {
    try {
      setSoulLoading(true);
      const res = await api.get<ContentResponse>('/api/config/soul');
      if (res.success) {
        setSoulContent(res.data.content);
      }
    } catch {
      // silently fail
    } finally {
      setSoulLoading(false);
    }
  };

  const loadRule = async () => {
    try {
      setRuleLoading(true);
      const res = await api.get<ContentResponse>('/api/config/rule');
      if (res.success) {
        setRuleContent(res.data.content);
      }
    } catch {
      // silently fail
    } finally {
      setRuleLoading(false);
    }
  };

  const saveSystemMemory = async () => {
    try {
      await api.put('/api/config/system-memory', { content: systemMemory });
      toast.success(t('settings.saveSuccess'));
    } catch {
      toast.danger(t('settings.saveFailed'));
    }
  };

  const saveSoul = async () => {
    try {
      await api.put('/api/config/soul', { content: soulContent });
      toast.success(t('settings.saveSuccess'));
    } catch {
      toast.danger(t('settings.saveFailed'));
    }
  };

  const saveRule = async () => {
    try {
      await api.put('/api/config/rule', { content: ruleContent });
      toast.success(t('settings.saveSuccess'));
    } catch {
      toast.danger(t('settings.saveFailed'));
    }
  };

  const saveConfig = async () => {
    setSaving(true);
    try {
      await api.put('/api/config', {
        model: editModel,
        baseUrl: editBaseUrl,
        maxTokens: parseInt(editMaxTokens) || undefined,
        messageLimit: parseInt(editMessageLimit) || undefined,
        imapHost: editImapHost,
        emailAddress: editEmailAddress,
      });
      toast.success(t('gateway.configSaved'));
    } catch {
      toast.danger(t('settings.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  const saveAndRestart = async () => {
    setRestarting(true);
    try {
      await api.put('/api/config', {
        model: editModel,
        baseUrl: editBaseUrl,
        maxTokens: parseInt(editMaxTokens) || undefined,
        messageLimit: parseInt(editMessageLimit) || undefined,
        imapHost: editImapHost,
        emailAddress: editEmailAddress,
      });
      await api.post('/api/gateway/restart', {});
      toast.success(t('gateway.restartSuccess'));
      // Reload config to reflect any changes
      await loadConfig();
    } catch {
      toast.danger(t('settings.saveFailed'));
    } finally {
      setRestarting(false);
    }
  };

  const handleTabChange = (key: string | number) => {
    const tabKey = String(key);
    if (tabKey === 'systemMemory') {
      loadSystemMemory();
    } else if (tabKey === 'soul') {
      loadSoul();
    } else if (tabKey === 'rule') {
      loadRule();
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

        <Tabs onSelectionChange={handleTabChange}>
          <Tabs.ListContainer>
            <Tabs.List aria-label="Settings tabs">
              <Tabs.Tab id="general">
                {t('settings.general')}
                <Tabs.Indicator />
              </Tabs.Tab>
              <Tabs.Tab id="email">
                {t('settings.email')}
                <Tabs.Indicator />
              </Tabs.Tab>
              <Tabs.Tab id="systemMemory">
                {t('settings.systemMemory')}
                <Tabs.Indicator />
              </Tabs.Tab>
              <Tabs.Tab id="soul">
                {t('settings.soul')}
                <Tabs.Indicator />
              </Tabs.Tab>
              <Tabs.Tab id="rule">
                {t('settings.rule')}
                <Tabs.Indicator />
              </Tabs.Tab>
            </Tabs.List>
          </Tabs.ListContainer>

          {/* General Tab */}
          <Tabs.Panel id="general">
            <Card className="mt-4">
              <Card.Header>
                <Card.Title>{t('settings.general')}</Card.Title>
              </Card.Header>
              <Card.Content>
                <div className="space-y-4">
                  <TextField value={editModel} onChange={setEditModel}>
                    <Label>{t('settings.model')}</Label>
                    <Input />
                  </TextField>
                  <TextField value={editBaseUrl} onChange={setEditBaseUrl}>
                    <Label>{t('settings.baseUrl')}</Label>
                    <Input />
                  </TextField>

                  <Separator />

                  <h3 className="font-semibold">{t('dashboard.serverConfig')}</h3>
                  <TextField isDisabled value={config?.server.listenUrl ?? ''}>
                    <Label>{t('settings.listenUrl')}</Label>
                    <Input />
                  </TextField>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <TextField value={editMaxTokens} onChange={setEditMaxTokens}>
                      <Label>{t('settings.maxTokens')}</Label>
                      <Input />
                    </TextField>
                    <TextField value={editMessageLimit} onChange={setEditMessageLimit}>
                      <Label>{t('settings.messageLimit')}</Label>
                      <Input />
                    </TextField>
                  </div>

                  <Separator />

                  <h3 className="font-semibold">{t('settings.allowedUsers')}</h3>
                  <div className="flex flex-wrap gap-2">
                    {config?.allowedUsers.map(userId => (
                      <Chip key={userId} size="sm" variant="soft">{userId}</Chip>
                    ))}
                  </div>

                  <Separator />

                  <div className="flex items-center gap-4">
                    <Switch isSelected={config?.iLink.enabled ?? false} isDisabled>
                      <Switch.Control>
                        <Switch.Thumb />
                      </Switch.Control>
                      <Switch.Content>
                        <Label>iLink {t('settings.enabled')}</Label>
                      </Switch.Content>
                    </Switch>
                  </div>

                  <Separator />

                  <div className="flex gap-3">
                    <Button
                      onPress={saveConfig}
                      isDisabled={saving || restarting}
                    >
                      {saving ? <Spinner size="sm" className="mr-2" /> : null}
                      {t('gateway.saveConfig')}
                    </Button>
                    <Button
                      variant="danger"
                      onPress={saveAndRestart}
                      isDisabled={saving || restarting}
                    >
                      {restarting ? <Spinner size="sm" className="mr-2" /> : null}
                      {restarting ? t('gateway.restarting') : t('gateway.saveAndRestart')}
                    </Button>
                  </div>
                </div>
              </Card.Content>
            </Card>
          </Tabs.Panel>

          {/* Email Tab */}
          <Tabs.Panel id="email">
            <Card className="mt-4">
              <Card.Header>
                <Card.Title>{t('settings.email')}</Card.Title>
              </Card.Header>
              <Card.Content>
                <div className="space-y-4">
                  <TextField value={editImapHost} onChange={setEditImapHost}>
                    <Label>{t('settings.imapHost')}</Label>
                    <Input />
                  </TextField>
                  <TextField value={editEmailAddress} onChange={setEditEmailAddress}>
                    <Label>{t('settings.emailAddress')}</Label>
                    <Input />
                  </TextField>
                  <div className="flex items-center gap-4">
                    <Switch isSelected={config?.email.enabled ?? false} isDisabled>
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
                    <Button
                      onPress={saveConfig}
                      isDisabled={saving || restarting}
                    >
                      {saving ? <Spinner size="sm" className="mr-2" /> : null}
                      {t('gateway.saveConfig')}
                    </Button>
                    <Button
                      variant="danger"
                      onPress={saveAndRestart}
                      isDisabled={saving || restarting}
                    >
                      {restarting ? <Spinner size="sm" className="mr-2" /> : null}
                      {restarting ? t('gateway.restarting') : t('gateway.saveAndRestart')}
                    </Button>
                  </div>
                </div>
              </Card.Content>
            </Card>
          </Tabs.Panel>

          {/* System Memory Tab */}
          <Tabs.Panel id="systemMemory">
            <Card className="mt-4">
              <Card.Header>
                <Card.Title>{t('settings.systemMemory')}</Card.Title>
              </Card.Header>
              <Card.Content>
                {memoryLoading ? (
                  <div className="flex justify-center py-8">
                    <Spinner size="lg" />
                  </div>
                ) : (
                  <div className="space-y-4">
                    <TextArea
                      value={systemMemory}
                      onChange={(e) => setSystemMemory(e.target.value)}
                      rows={15}
                      className="font-mono w-full"
                    />
                    <Button onPress={saveSystemMemory}>
                      {t('common.save')}
                    </Button>
                  </div>
                )}
              </Card.Content>
            </Card>
          </Tabs.Panel>

          {/* Soul Tab */}
          <Tabs.Panel id="soul">
            <Card className="mt-4">
              <Card.Header>
                <Card.Title>{t('settings.soul')}</Card.Title>
              </Card.Header>
              <Card.Content>
                {soulLoading ? (
                  <div className="flex justify-center py-8">
                    <Spinner size="lg" />
                  </div>
                ) : (
                  <div className="space-y-4">
                    <TextArea
                      value={soulContent}
                      onChange={(e) => setSoulContent(e.target.value)}
                      rows={15}
                      className="font-mono w-full"
                    />
                    <Button onPress={saveSoul}>
                      {t('common.save')}
                    </Button>
                  </div>
                )}
              </Card.Content>
            </Card>
          </Tabs.Panel>

          {/* Rule Tab */}
          <Tabs.Panel id="rule">
            <Card className="mt-4">
              <Card.Header>
                <Card.Title>{t('settings.rule')}</Card.Title>
              </Card.Header>
              <Card.Content>
                {ruleLoading ? (
                  <div className="flex justify-center py-8">
                    <Spinner size="lg" />
                  </div>
                ) : (
                  <div className="space-y-4">
                    <TextArea
                      value={ruleContent}
                      onChange={(e) => setRuleContent(e.target.value)}
                      rows={15}
                      className="font-mono w-full"
                    />
                    <Button onPress={saveRule}>
                      {t('common.save')}
                    </Button>
                  </div>
                )}
              </Card.Content>
            </Card>
          </Tabs.Panel>
        </Tabs>
      </div>
    </DefaultLayout>
  );
}

export default SettingsPage;
