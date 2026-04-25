import { useState } from 'react';
import { Tabs } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import AccessKeyTab from './AccessKeyTab';
import ChangePasswordTab from './ChangePasswordTab';

function SecurityPage() {
  const { t } = useI18n();
  const [activeTab, setActiveTab] = useState<string>('accessKeys');

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4">
        <h1 className="text-2xl font-bold mb-6">{t('security.title')}</h1>

        <Tabs onSelectionChange={(key) => setActiveTab(String(key))}>
          <Tabs.ListContainer>
            <Tabs.List aria-label="Security tabs">
              <Tabs.Tab id="accessKeys">
                {t('security.accessKeys')}
                <Tabs.Indicator />
              </Tabs.Tab>
              <Tabs.Tab id="password">
                {t('security.changePassword')}
                <Tabs.Indicator />
              </Tabs.Tab>
            </Tabs.List>
          </Tabs.ListContainer>

          <Tabs.Panel id="accessKeys">
            <AccessKeyTab active={activeTab === 'accessKeys'} />
          </Tabs.Panel>

          <Tabs.Panel id="password">
            <ChangePasswordTab />
          </Tabs.Panel>
        </Tabs>
      </div>
    </DefaultLayout>
  );
}

export default SecurityPage;
