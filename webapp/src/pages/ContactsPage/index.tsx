import { useEffect, useState } from 'react';
import { Button, Modal, toast } from '@heroui/react';
import { useOverlayState } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import ContactsTable from './ContactsTable';
import ContactFormModal from './ContactFormModal';
import type { Contact, ContactsResponse } from './types';

function ContactsPage() {
  const { t } = useI18n();
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [editingContact, setEditingContact] = useState<Contact | null>(null);
  const [deletingContact, setDeletingContact] = useState<Contact | null>(null);

  const formModal = useOverlayState();
  const deleteModal = useOverlayState();

  useEffect(() => {
    loadContacts();
  }, []);

  const loadContacts = async () => {
    try {
      setLoading(true);
      const res = await api.get<ContactsResponse>('/api/contacts');
      if (res.success) {
        setContacts(res.data);
      }
    } catch {
      // silently fail
    } finally {
      setLoading(false);
    }
  };

  const openAddModal = () => {
    setEditingContact(null);
    formModal.open();
  };

  const openEditModal = (contact: Contact) => {
    setEditingContact(contact);
    formModal.open();
  };

  const openDeleteModal = (contact: Contact) => {
    setDeletingContact(contact);
    deleteModal.open();
  };

  const handleSave = async (data: { name: string; email: string | null; phone: string | null; notes: string | null }) => {
    try {
      if (editingContact) {
        await api.put(`/api/contacts/${editingContact.id}`, data);
        toast.success(t('contacts.updateSuccess'));
      } else {
        await api.post('/api/contacts', data);
        toast.success(t('contacts.addSuccess'));
      }
      formModal.close();
      await loadContacts();
    } catch {
      toast.danger(t('common.error'));
    }
  };

  const handleDelete = async () => {
    if (!deletingContact) return;
    try {
      await api.del(`/api/contacts/${deletingContact.id}`);
      toast.success(t('contacts.deleteSuccess'));
      deleteModal.close();
      await loadContacts();
    } catch {
      toast.danger(t('common.error'));
    }
  };

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4">
        <div className="flex items-center justify-between mb-6">
          <h1 className="text-2xl font-bold">{t('contacts.title')}</h1>
          <Button onPress={openAddModal}>
            {t('contacts.addContact')}
          </Button>
        </div>

        <ContactsTable
          contacts={contacts}
          loading={loading}
          searchQuery={searchQuery}
          onSearchChange={setSearchQuery}
          onEdit={openEditModal}
          onDelete={openDeleteModal}
        />

        <ContactFormModal
          isOpen={formModal.isOpen}
          onOpenChange={formModal.toggle}
          onClose={formModal.close}
          editingContact={editingContact}
          onSave={handleSave}
        />

        {/* Delete Confirmation Modal */}
        <Modal>
          <Modal.Backdrop isOpen={deleteModal.isOpen} onOpenChange={deleteModal.toggle}>
            <Modal.Container>
              <Modal.Dialog role="alertdialog">
                <Modal.Header>
                  <Modal.Heading>{t('contacts.deleteContact')}</Modal.Heading>
                </Modal.Header>
                <Modal.Body>
                  <p>{t('contacts.deleteConfirm')}</p>
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

export default ContactsPage;
