import { useEffect, useState } from 'react';
import { Button, Modal, toast } from '@heroui/react';
import { useOverlayState } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import TasksTable from './TasksTable';
import TaskFormModal from './TaskFormModal';
import type { ScheduledTask, TasksResponse } from './types';

function TasksPage() {
  const { t } = useI18n();
  const [tasks, setTasks] = useState<ScheduledTask[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [editingTask, setEditingTask] = useState<ScheduledTask | null>(null);
  const [deletingTask, setDeletingTask] = useState<ScheduledTask | null>(null);

  const formModal = useOverlayState();
  const deleteModal = useOverlayState();

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
      </div>
    </DefaultLayout>
  );
}

export default TasksPage;
