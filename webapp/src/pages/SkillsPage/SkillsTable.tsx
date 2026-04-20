import { Card, Button, Chip, Spinner, Table, SearchField } from '@heroui/react';
import { useI18n } from '../../i18n';
import type { SkillInfo } from './types';

interface SkillsTableProps {
  skills: SkillInfo[];
  loading: boolean;
  searchQuery: string;
  onSearchChange: (query: string) => void;
  onEdit: (skill: SkillInfo) => void;
  onDelete: (skill: SkillInfo) => void;
}

function SkillsTable({ skills, loading, searchQuery, onSearchChange, onEdit, onDelete }: SkillsTableProps) {
  const { t } = useI18n();

  const filteredSkills = searchQuery
    ? skills.filter(s =>
        s.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
        s.description.toLowerCase().includes(searchQuery.toLowerCase()) ||
        s.command.toLowerCase().includes(searchQuery.toLowerCase())
      )
    : skills;

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
          ) : filteredSkills.length === 0 ? (
            <p className="text-center py-8 text-gray-500">{t('common.noData')}</p>
          ) : (
            <Table>
              <Table.ScrollContainer>
                <Table.Content aria-label="Skills">
                  <Table.Header>
                    <Table.Column>{t('skills.name')}</Table.Column>
                    <Table.Column>{t('skills.description')}</Table.Column>
                    <Table.Column>{t('skills.mode')}</Table.Column>
                    <Table.Column>{t('skills.commandOrUrl')}</Table.Column>
                    <Table.Column>{t('skills.parameterCount')}</Table.Column>
                    <Table.Column>{t('common.actions')}</Table.Column>
                  </Table.Header>
                  <Table.Body>
                    {filteredSkills.map(skill => (
                      <Table.Row key={skill.fileName}>
                        <Table.Cell>
                          <span className="font-medium font-mono">{skill.name}</span>
                        </Table.Cell>
                        <Table.Cell>
                          <div className="max-w-xs truncate" title={skill.description}>
                            {skill.description}
                          </div>
                        </Table.Cell>
                        <Table.Cell>
                          <Chip
                            size="sm"
                            color={skill.mode === 'http' ? 'accent' : skill.mode === 'skill.md' ? 'success' : 'default'}
                            variant="soft"
                          >
                            {skill.mode === 'http' ? 'HTTP' : skill.mode === 'skill.md' ? 'SKILL.md' : 'Command'}
                          </Chip>
                        </Table.Cell>
                        <Table.Cell>
                          {skill.mode === 'skill.md' ? (
                            <Chip size="sm" variant="soft" color="success">
                              <span className="font-mono text-xs">OpenClaw</span>
                            </Chip>
                          ) : (
                            <Chip size="sm" variant="soft">
                              <span className="font-mono text-xs">{skill.command.length > 30 ? skill.command.substring(0, 30) + '...' : skill.command}</span>
                            </Chip>
                          )}
                        </Table.Cell>
                        <Table.Cell>
                          <Chip size="sm" color="accent">{skill.parameterCount}</Chip>
                        </Table.Cell>
                        <Table.Cell>
                          <div className="flex gap-2">
                            <Button size="sm" variant="secondary" onPress={() => onEdit(skill)}>
                              {t('common.edit')}
                            </Button>
                            <Button size="sm" variant="danger" onPress={() => onDelete(skill)}>
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

export default SkillsTable;
