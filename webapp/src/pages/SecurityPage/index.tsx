import { useI18n } from '../../i18n';

function SecurityPage() {
  const { t } = useI18n();
  return (
    <div className="p-6">
      <h1 className="text-2xl font-bold mb-4">{t('security.title')}</h1>
      <p>Loading...</p>
    </div>
  );
}

export default SecurityPage;
