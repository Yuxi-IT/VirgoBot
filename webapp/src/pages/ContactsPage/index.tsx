import { useEffect, useState } from 'react';
import { Card, Button, Spinner, Table, Modal, TextField, Label, Input, TextArea, SearchField, Chip, toast } from '@heroui/react';
import { useOverlayState } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';

interface Contact {
  id: number;
  name: string;
  email: string | null;
  phone: string | null;
  notes: string | null;
}

interface ContactsResponse {
  success: boolean;
  data: Contact[];
}

function ContactsPage() {
  const { t } = useI18n();
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [editingContact, setEditingContact] = useState<Contact | null>(null);
  const [deletingContact, setDeletingContact] = useState<Contact | null>(null);

  const formModal = useOverlayState();
  const deleteModal = useOverlayState();

  // Form state
  const [formName, setFormName] = useState('');
  const [formEmail, setFormEmail] = useState('');
  const [formPhone, setFormPhone] = useState('');
  const [formNotes, setFormNotes] = useState('');

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
    setFormName('');
    setFormEmail('');
    setFormPhone('');
    setFormNotes('');
    formModal.open();
  };

  const openEditModal = (contact: Contact) => {
    setEditingContact(contact);
    setFormName(contact.name);
    setFormEmail(contact.email ?? '');
    setFormPhone(contact.phone ?? '');
    setFormNotes(contact.notes ?? '');
    formModal.open();
  };

  const openDeleteModal = (contact: Contact) => {
    setDeletingContact(contact);
    deleteModal.open();
  };

  const handleSave = async () => {
    try {
      const body = {
        name: formName,
        email: formEmail || null,
        phone: formPhone || null,
        notes: formNotes || null,
      };

      if (editingContact) {
        await api.put(`/api/contacts/${editingContact.id}`, body);
        toast.success(t('contacts.updateSuccess'));
      } else {
        await api.post('/api/contacts', body);
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

  const filteredContacts = searchQuery
    ? contacts.filter(c =>
        c.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
        (c.email?.toLowerCase().includes(searchQuery.toLowerCase())) ||
        (c.phone?.includes(searchQuery))
      )
    : contacts;

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4">
        <div className="flex items-center justify-between mb-6">
          <h1 className="text-2xl font-bold">{t('contacts.title')}</h1>
          <Button onPress={openAddModal}>
            {t('contacts.addContact')}
          </Button>
        </div>

        {/* Search */}
        <div className="mb-4">
          <SearchField value={searchQuery} onChange={setSearchQuery}>
            <SearchField.Group>
              <SearchField.SearchIcon />
              <SearchField.Input placeholder={t('common.search')} />
              <SearchField.ClearButton />
            </SearchField.Group>
          </SearchField>
        </div>

        {/* Contacts Table */}
        <Card>
          <Card.Content>
            {loading ? (
              <div className="flex justify-center py-8">
                <Spinner size="lg" />
              </div>
            ) : filteredContacts.length === 0 ? (
              <p className="text-center py-8 text-gray-500">{t('common.noData')}</p>
            ) : (
              <Table>
                <Table.ScrollContainer>
                  <Table.Content aria-label="Contacts">
                    <Table.Header>
                      <Table.Column>{t('contacts.name')}</Table.Column>
                      <Table.Column>{t('contacts.email')}</Table.Column>
                      <Table.Column>{t('contacts.phone')}</Table.Column>
                      <Table.Column>{t('contacts.notes')}</Table.Column>
                      <Table.Column>{t('common.actions')}</Table.Column>
                    </Table.Header>
                    <Table.Body>
                      {filteredContacts.map(contact => (
                        <Table.Row key={contact.id}>
                          <Table.Cell>
                            <span className="font-medium">{contact.name}</span>
                          </Table.Cell>
                          <Table.Cell>
                            {contact.email ? (
                              <Chip size="sm" variant="soft">{contact.email}</Chip>
                            ) : '-'}
                          </Table.Cell>
                          <Table.Cell>{contact.phone ?? '-'}</Table.Cell>
                          <Table.Cell>
                            <div className="max-w-xs truncate">{contact.notes ?? '-'}</div>
                          </Table.Cell>
                          <Table.Cell>
                            <div className="flex gap-2">
                              <Button size="sm" variant="secondary" onPress={() => openEditModal(contact)}>
                                {t('common.edit')}
                              </Button>
                              <Button size="sm" variant="danger" onPress={() => openDeleteModal(contact)}>
                                {t('common.delete')}
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

        {/* Add/Edit Modal */}
        <Modal>
          <Modal.Backdrop isOpen={formModal.isOpen} onOpenChange={formModal.toggle}>
            <Modal.Container>
              <Modal.Dialog>
                <Modal.Header>
                  <Modal.Heading>
                    {editingContact ? t('contacts.editContact') : t('contacts.addContact')}
                  </Modal.Heading>
                </Modal.Header>
                <Modal.Body>
                  <div className="space-y-4">
                    <TextField isRequired value={formName} onChange={setFormName}>
                      <Label>{t('contacts.name')}</Label>
                      <Input placeholder={t('contacts.name')} />
                    </TextField>
                    <TextField value={formEmail} onChange={setFormEmail}>
                      <Label>{t('contacts.email')}</Label>
                      <Input type="email" placeholder={t('contacts.email')} />
                    </TextField>
                    <TextField value={formPhone} onChange={setFormPhone}>
                      <Label>{t('contacts.phone')}</Label>
                      <Input placeholder={t('contacts.phone')} />
                    </TextField>
                    <div>
                      <Label>{t('contacts.notes')}</Label>
                      <TextArea
                        value={formNotes}
                        onChange={(e) => setFormNotes(e.target.value)}
                        placeholder={t('contacts.notes')}
                        rows={3}
                      />
                    </div>
                  </div>
                </Modal.Body>
                <Modal.Footer>
                  <Button variant="secondary" onPress={formModal.close}>
                    {t('common.cancel')}
                  </Button>
                  <Button onPress={handleSave} isDisabled={!formName.trim()}>
                    {t('common.save')}
                  </Button>
                </Modal.Footer>
              </Modal.Dialog>
            </Modal.Container>
          </Modal.Backdrop>
        </Modal>

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
