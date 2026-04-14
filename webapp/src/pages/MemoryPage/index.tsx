import { useState } from 'react';
import { Tabs } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import SessionSelector from './SessionSelector';
import MessagesPanel from './MessagesPanel';
import SoulPanel from './SoulPanel';

function MemoryPage() {
  const { t } = useI18n();
  const [activeTab, setActiveTab] = useState<string>('sessions');
  const [refreshKey, setRefreshKey] = useState(0);

  const handleSessionSwitched = () => {
    setRefreshKey(k => k + 1);
  };

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4">
        <h1 className="text-2xl font-bold mb-6">{t('memory.title')}</h1>

        <SessionSelector onSessionSwitched={handleSessionSwitched} />

        <Tabs onSelectionChange={(key) => setActiveTab(String(key))}>
          <Tabs.ListContainer>
            <Tabs.List aria-label="Memory tabs">
              <Tabs.Tab id="sessions">
                {t('memory.sessions')}
                <Tabs.Indicator />
              </Tabs.Tab>
              <Tabs.Tab id="soul">
                {t('memory.soul')}
                <Tabs.Indicator />
              </Tabs.Tab>
            </Tabs.List>
          </Tabs.ListContainer>

          <Tabs.Panel id="sessions">
            <MessagesPanel refreshKey={refreshKey} />
          </Tabs.Panel>

          <Tabs.Panel id="soul">
            <SoulPanel active={activeTab === 'soul'} />
          </Tabs.Panel>
        </Tabs>
      </div>
    </DefaultLayout>
  );
}

export default MemoryPage;
