import { Card, Button, Chip, Spinner, Table, SearchField } from '@heroui/react';
import { useI18n } from '../../i18n';
import type { Contact } from './types';

interface ContactsTableProps {
  contacts: Contact[];
  loading: boolean;
  searchQuery: string;
  onSearchChange: (query: string) => void;
  onEdit: (contact: Contact) => void;
  onDelete: (contact: Contact) => void;
}

function ContactsTable({ contacts, loading, searchQuery, onSearchChange, onEdit, onDelete }: ContactsTableProps) {
  const { t } = useI18n();

  const filteredContacts = searchQuery
    ? contacts.filter(c =>
        c.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
        (c.email?.toLowerCase().includes(searchQuery.toLowerCase())) ||
        (c.phone?.includes(searchQuery))
      )
    : contacts;

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
                            <Button size="sm" variant="secondary" onPress={() => onEdit(contact)}>
                              {t('common.edit')}
                            </Button>
                            <Button size="sm" variant="danger" onPress={() => onDelete(contact)}>
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
    </>
  );
}

export default ContactsTable;
