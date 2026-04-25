import { useState } from 'react';
import { Button, Card, TextField, Label, Input, toast } from '@heroui/react';
import { api } from '../../services/api';
import { useI18n } from '../../i18n';

function ChangePasswordTab() {
  const { t } = useI18n();
  const [oldPassword, setOldPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async () => {
    if (!oldPassword || !newPassword || !confirmPassword) {
      toast.danger(t('security.allFieldsRequired'));
      return;
    }
    if (newPassword !== confirmPassword) {
      toast.danger(t('security.passwordMismatch'));
      return;
    }
    setLoading(true);
    try {
      const res = await api.post<{ success: boolean; message?: string }>('/api/auth/change-password', {
        oldPassword,
        newPassword,
      });
      if (res.success) {
        toast.success(t('security.passwordChanged'));
        setOldPassword('');
        setNewPassword('');
        setConfirmPassword('');
      }
    } catch {
      toast.danger(t('security.oldPasswordWrong'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="mt-4 max-w-md">
      <Card>
        <div className="p-6 flex flex-col gap-4">
          <TextField value={oldPassword} onChange={setOldPassword}>
            <Label>{t('security.oldPassword')}</Label>
            <Input type="password" autoComplete="current-password" />
          </TextField>
          <TextField value={newPassword} onChange={setNewPassword}>
            <Label>{t('security.newPassword')}</Label>
            <Input type="password" autoComplete="new-password" />
          </TextField>
          <TextField value={confirmPassword} onChange={setConfirmPassword}>
            <Label>{t('security.confirmPassword')}</Label>
            <Input type="password" autoComplete="new-password" />
          </TextField>
          <Button
            variant="primary"
            onPress={handleSubmit}
            isDisabled={loading || !oldPassword || !newPassword || !confirmPassword}
          >
            {loading ? t('common.loading') : t('security.changePassword')}
          </Button>
        </div>
      </Card>
    </div>
  );
}

export default ChangePasswordTab;
