import { Button, Toolbar, SearchField, Label } from '@heroui/react';
import { Select, ListBox } from '@heroui/react';
import { useI18n } from '../../i18n';

interface LogToolbarProps {
  levelFilter: string;
  searchQuery: string;
  onLevelFilterChange: (level: string) => void;
  onSearchChange: (query: string) => void;
  onRefresh: () => void;
  onClear: () => void;
}

function LogToolbar({ levelFilter: _levelFilter, searchQuery, onLevelFilterChange, onSearchChange, onRefresh, onClear }: LogToolbarProps) {
  const { t } = useI18n();

  return (
    <Toolbar aria-label="Log actions" className="mb-4">
      <div className="flex flex-col sm:flex-row gap-4 w-full items-start sm:items-center">
        {/* Level Filter */}
        <div className="w-full sm:w-48">
          <Select
            placeholder={t('logs.allLevels')}
            onChange={(value) => {
              onLevelFilterChange(String(value ?? ''));
            }}
          >
            <Select.Trigger>
              <Select.Value />
            </Select.Trigger>
            <Select.Popover>
              <ListBox>
                <ListBox.Item id="" textValue="All">
                  <Label>{t('logs.allLevels')}</Label>
                </ListBox.Item>
                <ListBox.Item id="Info" textValue="Info">
                  <Label>{t('logs.info')}</Label>
                </ListBox.Item>
                <ListBox.Item id="Warn" textValue="Warn">
                  <Label>{t('logs.warn')}</Label>
                </ListBox.Item>
                <ListBox.Item id="Error" textValue="Error">
                  <Label>{t('logs.error')}</Label>
                </ListBox.Item>
                <ListBox.Item id="Success" textValue="Success">
                  <Label>{t('logs.success')}</Label>
                </ListBox.Item>
              </ListBox>
            </Select.Popover>
          </Select>
        </div>

        {/* Search */}
        <div className="flex-1">
          <SearchField value={searchQuery} onChange={onSearchChange}>
            <SearchField.Group>
              <SearchField.SearchIcon />
              <SearchField.Input placeholder={t('common.search')} />
              <SearchField.ClearButton />
            </SearchField.Group>
          </SearchField>
        </div>

        {/* Actions */}
        <div className="flex gap-2">
          <Button variant="secondary" onPress={onRefresh}>
            {t('common.refresh')}
          </Button>
          <Button variant="danger" onPress={onClear}>
            {t('logs.clearLogs')}
          </Button>
        </div>
      </div>
    </Toolbar>
  );
}

export default LogToolbar;
