import { useState, useEffect } from 'react';
import { Button, Modal, TextField, Label, Input, TextArea } from '@heroui/react';
import { useI18n } from '../../i18n';
import type { Contact } from './types';

interface ContactFormModalProps {
  isOpen: boolean;
  onOpenChange: () => void;
  onClose: () => void;
  editingContact: Contact | null;
  onSave: (data: { name: string; email: string | null; phone: string | null; notes: string | null }) => void;
}

function ContactFormModal({ isOpen, onOpenChange, onClose, editingContact, onSave }: ContactFormModalProps) {
  const { t } = useI18n();
  const [formName, setFormName] = useState('');
  const [formEmail, setFormEmail] = useState('');
  const [formPhone, setFormPhone] = useState('');
  const [formNotes, setFormNotes] = useState('');

  useEffect(() => {
    if (isOpen) {
      if (editingContact) {
        setFormName(editingContact.name);
        setFormEmail(editingContact.email ?? '');
        setFormPhone(editingContact.phone ?? '');
        setFormNotes(editingContact.notes ?? '');
      } else {
        setFormName('');
        setFormEmail('');
        setFormPhone('');
        setFormNotes('');
      }
    }
  }, [isOpen, editingContact]);

  const handleSave = () => {
    onSave({
      name: formName,
      email: formEmail || null,
      phone: formPhone || null,
      notes: formNotes || null,
    });
  };

  return (
    <Modal>
      <Modal.Backdrop isOpen={isOpen} onOpenChange={onOpenChange}>
        <Modal.Container size="lg">
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
              <Button variant="secondary" onPress={onClose}>
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
  );
}

export default ContactFormModal;
