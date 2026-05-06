import { useI18n } from '../../i18n';
import ChangePasswordTab from '../SecurityPage/ChangePasswordTab';
import AccessKeyTab from '../SecurityPage/AccessKeyTab';

function SecurityTab({ active }: { active: boolean }) {
  const { t } = useI18n();

  return (
    <div className="mt-4 space-y-6">
      <div>
        <h2 className="text-lg font-semibold mb-3">{t('security.changePassword')}</h2>
        <ChangePasswordTab />
      </div>

      <div>
        <h2 className="text-lg font-semibold mb-3">{t('security.accessKeys')}</h2>
        <AccessKeyTab active={active} />
      </div>
    </div>
  );
}

export default SecurityTab;
