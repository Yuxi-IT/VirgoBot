import { Label } from '@heroui/react';
import { Select, ListBox } from '@heroui/react';
import { useI18n } from '../../i18n';
import type { UserInfo } from './types';

interface UserSelectorProps {
  users: UserInfo[];
  loading: boolean;
  onSelect: (userId: string) => void;
}

function UserSelector({ users, loading, onSelect }: UserSelectorProps) {
  const { t } = useI18n();

  return (
    <div className="w-full sm:w-64">
      <Select
        placeholder={t('chat.selectUser')}
        onChange={(value) => {
          onSelect(String(value));
        }}
      >
        <Select.Trigger>
          <Select.Value />
        </Select.Trigger>
        <Select.Popover>
          <ListBox>
            {loading ? (
              <ListBox.Item id="loading" textValue="Loading">
                <Label>{t('common.loading')}</Label>
              </ListBox.Item>
            ) : users.map(user => (
              <ListBox.Item key={user.userId} id={user.userId} textValue={user.userId}>
                <Label>{user.userId}</Label>
              </ListBox.Item>
            ))}
          </ListBox>
        </Select.Popover>
      </Select>
    </div>
  );
}

export default UserSelector;
