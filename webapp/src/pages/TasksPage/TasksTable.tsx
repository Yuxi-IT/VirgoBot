import { Table, TextField, Input, Badge, Button, Spinner } from '@heroui/react';
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
    } else if (task.scheduleType === 'cron') {
      return task.cronExpression;
    }
    return '-';
  };

  return (
    <div className="space-y-4">
      <TextField>
        <Input
          placeholder={t('common.search')}
          value={searchQuery}
          onChange={(e) => onSearchChange(e.target.value)}
        />
      </TextField>

      {loading ? (
        <div className="flex justify-center py-8">
          <Spinner size="lg" />
        </div>
      ) : (
        <Table>
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
            {filteredTasks.length === 0 ? (
              <Table.Row>
                <Table.Cell colSpan={7} className="text-center py-8 text-gray-500">
                  {t('common.noData')}
                </Table.Cell>
              </Table.Row>
            ) : (
              filteredTasks.map((task) => (
                <Table.Row key={task.id}>
                  <Table.Cell>
                    <div>
                      <div className="font-medium">{task.name}</div>
                      <div className="text-sm text-gray-500">{task.description}</div>
                    </div>
                  </Table.Cell>
                  <Table.Cell>
                    <Badge variant={task.taskType === 'http' ? 'primary' : 'secondary'}>
                      {task.taskType.toUpperCase()}
                    </Badge>
                  </Table.Cell>
                  <Table.Cell>{getScheduleText(task)}</Table.Cell>
                  <Table.Cell className="text-sm">{formatTime(task.lastRunTime)}</Table.Cell>
                  <Table.Cell className="text-sm">{formatTime(task.nextRunTime)}</Table.Cell>
                  <Table.Cell>
                    <Switch
                      isSelected={task.enabled}
                      onChange={() => onToggle(task)}
                    />
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
              ))
            )}
          </Table.Body>
        </Table>
      )}
    </div>
  );
}

export default TasksTable;
