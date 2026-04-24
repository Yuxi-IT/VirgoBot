import { Table, Button, Spinner, Card, SearchField } from '@heroui/react';
import { Pencil, TrashBin } from '@gravity-ui/icons';
import { useI18n } from '../../i18n';
import type { ScheduledTask } from './types';
import { Switch } from '@heroui/react';

interface TasksTableProps {
  tasks: ScheduledTask[];
  loading: boolean;
  searchQuery: string;
  onSearchChange: (query: string) => void;
  onEdit: (task: ScheduledTask) => void;
  onDelete: (task: ScheduledTask) => void;
  onToggle: (task: ScheduledTask) => void;
}

function TasksTable({ tasks, loading, searchQuery, onSearchChange, onEdit, onDelete, onToggle }: TasksTableProps) {
  const { t } = useI18n();

  const filteredTasks = tasks.filter(task =>
    task.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
    task.description.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const formatTime = (time?: string) => {
    if (!time) return '-';
    return new Date(time).toLocaleString();
  };

  const getScheduleText = (task: ScheduledTask) => {
    if (task.scheduleType === 'interval') {
      return `${t('tasks.every')} ${task.intervalMinutes} ${t('tasks.minutes')}`;
    } else if (task.scheduleType === 'daily') {
      return `${t('tasks.daily')} ${task.dailyTime}`;
    } else if (task.scheduleType === 'once') {
      if (task.onceAt) return new Date(task.onceAt).toLocaleString();
      if (task.onceDelayMinutes) return `${task.onceDelayMinutes} ${t('tasks.minutes')}`;
      return '-';
    } else if (task.scheduleType === 'message_count') {
      const role = task.messageCountRole === 'user' ? t('tasks.roleUser') : t('tasks.roleAssistant');
      return `${t('tasks.every')} ${task.messageCountTarget} ${t('tasks.messagesOf')} ${role} (${task.messageCountCurrent}/${task.messageCountTarget})`;
    }
    return '-';
  };

  return (
    <>
      <div className="mb-4">
        <SearchField value={searchQuery} onChange={onSearchChange}>
          <SearchField.Group>
            <SearchField.SearchIcon />
            <SearchField.Input placeholder={t('common.search')} />
            <SearchField.ClearButton />
          </SearchField.Group>
        </SearchField>
      </div>

      <Card>
        <Card.Content>
          {loading ? (
            <div className="flex justify-center py-8">
              <Spinner size="lg" />
            </div>
          ) : filteredTasks.length === 0 ? (
            <p className="text-center py-8 text-gray-500">{t('common.noData')}</p>
          ) : (
            <Table>
              <Table.ScrollContainer>
                <Table.Content aria-label="Tasks">
                  <Table.Header>
                    <Table.Column>{t('tasks.name')}</Table.Column>
                    <Table.Column>{t('tasks.type')}</Table.Column>
                    <Table.Column>{t('tasks.schedule')}</Table.Column>
                    <Table.Column>{t('tasks.lastRun')}</Table.Column>
                    <Table.Column>{t('tasks.nextRun')}</Table.Column>
                    <Table.Column>{t('tasks.status')}</Table.Column>
                    <Table.Column>{t('common.actions')}</Table.Column>
                  </Table.Header>
                  <Table.Body>
                    {filteredTasks.map((task) => (
                      <Table.Row key={task.id}>
                        <Table.Cell>
                          <div>
                            <div className="font-medium">{task.name}</div>
                            <div className="text-sm text-gray-500">{task.description}</div>
                          </div>
                        </Table.Cell>
                        <Table.Cell>
                          {task.taskType.toUpperCase()}
                        </Table.Cell>
                        <Table.Cell>{getScheduleText(task)}</Table.Cell>
                        <Table.Cell className="text-sm">{formatTime(task.lastRunTime)}</Table.Cell>
                        <Table.Cell className="text-sm">{formatTime(task.nextRunTime)}</Table.Cell>
                        <Table.Cell>
                          <Switch 
                            isSelected={task.enabled}
                            onChange={() => onToggle(task)}>
                            <Switch.Control>
                              <Switch.Thumb />
                            </Switch.Control>
                          </Switch>
                        </Table.Cell>
                        <Table.Cell>
                          <div className="flex gap-2">
                            <Button
                              size="sm"
                              variant="secondary"
                              isIconOnly
                              onPress={() => onEdit(task)}
                            >
                              <Pencil />
                            </Button>
                            <Button
                              size="sm"
                              variant="danger"
                              isIconOnly
                              onPress={() => onDelete(task)}
                            >
                              <TrashBin />
                            </Button>
                          </div>
                        </Table.Cell>
                      </Table.Row>
                    ))}
                  </Table.Body>
                </Table.Content>
              </Table.ScrollContainer>
            </Table>
          )}
        </Card.Content>
      </Card>
    </>
  );
}

export default TasksTable;
