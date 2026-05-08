import { useEffect, useState } from 'react';
import { Button, Modal, toast, Spinner } from '@heroui/react';
import { useOverlayState } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import TasksTable from './TasksTable';
import TaskFormModal from './TaskFormModal';
import type { ScheduledTask, TasksResponse, TaskHistoryEntry, TaskHistoryResponse } from './types';

function TasksPage() {
  const { t } = useI18n();
  const [tasks, setTasks] = useState<ScheduledTask[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [editingTask, setEditingTask] = useState<ScheduledTask | null>(null);
  const [deletingTask, setDeletingTask] = useState<ScheduledTask | null>(null);
  const [historyTask, setHistoryTask] = useState<ScheduledTask | null>(null);
  const [historyEntries, setHistoryEntries] = useState<TaskHistoryEntry[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);

  const formModal = useOverlayState();
  const deleteModal = useOverlayState();
  const historyModal = useOverlayState();

  useEffect(() => {
    loadTasks();
  }, []);

  const loadTasks = async () => {
    try {
      setLoading(true);
      const res = await api.get<TasksResponse>('/api/tasks');
      if (res.success) {
        setTasks(res.data);
      }
    } catch {
      // silently fail
    } finally {
      setLoading(false);
    }
  };

  const openAddModal = () => {
    setEditingTask(null);
    formModal.open();
  };

  const openEditModal = (task: ScheduledTask) => {
    setEditingTask(task);
    formModal.open();
  };

  const openDeleteModal = (task: ScheduledTask) => {
    setDeletingTask(task);
    deleteModal.open();
  };

  const openHistoryModal = async (task: ScheduledTask) => {
    setHistoryTask(task);
    historyModal.open();
    setHistoryLoading(true);
    try {
      const res = await api.get<TaskHistoryResponse>(`/api/tasks/${task.id}/history`);
      setHistoryEntries(res.data || []);
    } catch {
      setHistoryEntries([]);
    } finally {
      setHistoryLoading(false);
    }
  };

  const clearHistory = async () => {
    if (!historyTask) return;
    try {
      await api.del(`/api/tasks/${historyTask.id}/history`);
      toast.success(t('tasks.historyCleared'));
      setHistoryEntries([]);
    } catch {
      toast.danger(t('common.error'));
    }
  };

  const handleToggle = async (task: ScheduledTask) => {
    try {
      await api.post(`/api/tasks/${task.id}/toggle`, { enabled: !task.enabled });
      toast.success(t('tasks.toggleSuccess'));
      await loadTasks();
    } catch {
      toast.danger(t('common.error'));
    }
  };

  const handleDelete = async () => {
    if (!deletingTask) return;
    try {
      await api.del(`/api/tasks/${deletingTask.id}`);
      toast.success(t('tasks.deleteSuccess'));
      deleteModal.close();
      await loadTasks();
    } catch {
      toast.danger(t('common.error'));
    }
  };

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4">
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-2xl font-bold">{t('tasks.title')}</h1>
            <p className="text-sm text-gray-500 mt-1">{t('tasks.subtitle')}</p>
          </div>
          <Button onPress={openAddModal}>
            {t('tasks.addTask')}
          </Button>
        </div>

        <TasksTable
          tasks={tasks}
          loading={loading}
          searchQuery={searchQuery}
          onSearchChange={setSearchQuery}
          onEdit={openEditModal}
          onDelete={openDeleteModal}
          onToggle={handleToggle}
          onHistory={openHistoryModal}
        />

        <TaskFormModal
          isOpen={formModal.isOpen}
          onOpenChange={formModal.toggle}
          onClose={formModal.close}
          editingTask={editingTask}
          onSaved={loadTasks}
        />

        <Modal>
          <Modal.Backdrop isOpen={deleteModal.isOpen} onOpenChange={deleteModal.toggle}>
            <Modal.Container size="lg">
              <Modal.Dialog role="alertdialog">
                <Modal.Header>
                  <Modal.Heading>{t('tasks.deleteTask')}</Modal.Heading>
                </Modal.Header>
                <Modal.Body>
                  <p>{t('tasks.deleteConfirm')}</p>
                </Modal.Body>
                <Modal.Footer>
                  <Button variant="secondary" onPress={deleteModal.close}>
                    {t('common.cancel')}
                  </Button>
                  <Button variant="danger" onPress={handleDelete}>
                    {t('common.delete')}
                  </Button>
                </Modal.Footer>
              </Modal.Dialog>
            </Modal.Container>
          </Modal.Backdrop>
        </Modal>

        {/* Execution History Modal */}
        <Modal>
          <Modal.Backdrop isOpen={historyModal.isOpen} onOpenChange={historyModal.toggle}>
            <Modal.Container size="lg">
              <Modal.Dialog>
                <Modal.Header>
                  <Modal.Heading>
                    {t('tasks.executionHistory')} - {historyTask?.name}
                  </Modal.Heading>
                </Modal.Header>
                <Modal.Body>
                  {historyLoading ? (
                    <div className="flex justify-center py-8"><Spinner size="lg" /></div>
                  ) : historyEntries.length === 0 ? (
                    <p className="text-center py-8 text-gray-500">{t('tasks.noHistory')}</p>
                  ) : (
                    <div className="max-h-[50vh] overflow-y-auto">
                      <table className="w-full text-sm">
                        <thead>
                          <tr className="border-b border-default-200">
                            <th className="text-left py-2 px-2">{t('tasks.status')}</th>
                            <th className="text-left py-2 px-2">{t('tasks.duration')}</th>
                            <th className="text-left py-2 px-2">{t('tasks.executedAt')}</th>
                            <th className="text-left py-2 px-2">{t('common.actions')}</th>
                          </tr>
                        </thead>
                        <tbody>
                          {historyEntries.map(e => (
                            <tr key={e.id} className="border-b border-default-100">
                              <td className="py-2 px-2">
                                <span className={e.status === 'success' ? 'text-success' : 'text-danger'}>
                                  {e.status}
                                </span>
                              </td>
                              <td className="py-2 px-2">{e.durationMs}ms</td>
                              <td className="py-2 px-2">{new Date(e.executedAt).toLocaleString()}</td>
                              <td className="py-2 px-2">
                                <span className="text-xs text-default-500 max-w-[200px] truncate block" title={e.result}>
                                  {e.result.length > 40 ? e.result.slice(0, 40) + '...' : e.result}
                                </span>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </Modal.Body>
                <Modal.Footer>
                  <Button variant="danger" size="sm" onPress={clearHistory} isDisabled={historyEntries.length === 0}>
                    {t('tasks.clearHistory')}
                  </Button>
                  <Button variant="secondary" onPress={historyModal.close}>
                    {t('common.cancel')}
                  </Button>
                </Modal.Footer>
              </Modal.Dialog>
            </Modal.Container>
          </Modal.Backdrop>
        </Modal>
      </div>
    </DefaultLayout>
  );
}

export default TasksPage;
