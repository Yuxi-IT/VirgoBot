import { useEffect, useState, useRef } from 'react';
import { Button, Modal, toast, TextField, Label, Input, Spinner, Chip } from '@heroui/react';
import { useOverlayState } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api, BASE_URL, getToken } from '../../services/api';
import SkillsTable from './SkillsTable';
import SkillFormModal from './SkillFormModal';
import SkillMdEditModal from './SkillMdEditModal';
import type { SkillInfo, SkillsResponse, SkillTestResult } from './types';

function SkillsPage() {
  const { t } = useI18n();
  const [skills, setSkills] = useState<SkillInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [editingSkill, setEditingSkill] = useState<SkillInfo | null>(null);
  const [deletingSkill, setDeletingSkill] = useState<SkillInfo | null>(null);
  const [importUrl, setImportUrl] = useState('');
  const [importing, setImporting] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [testingSkill, setTestingSkill] = useState<SkillInfo | null>(null);
  const [testResult, setTestResult] = useState<SkillTestResult | null>(null);
  const [testLoading, setTestLoading] = useState(false);

  const formModal = useOverlayState();
  const deleteModal = useOverlayState();
  const importModal = useOverlayState();
  const skillMdModal = useOverlayState();
  const addTypeModal = useOverlayState();
  const testModal = useOverlayState();

  const [editingSkillMd, setEditingSkillMd] = useState<SkillInfo | null>(null);

  useEffect(() => {
    loadSkills();
  }, []);

  const loadSkills = async () => {
    try {
      setLoading(true);
      const res = await api.get<SkillsResponse>('/api/skills');
      if (res.success) {
        setSkills(res.data);
      }
    } catch {
      // silently fail
    } finally {
      setLoading(false);
    }
  };

  const loadSkillsAndReload = async () => {
    await loadSkills();
    await reloadSkills();
  };

  const openAddModal = () => {
    addTypeModal.open();
  };

  const handleAddJson = () => {
    addTypeModal.close();
    setEditingSkill(null);
    formModal.open();
  };

  const handleAddSkillMd = () => {
    addTypeModal.close();
    setEditingSkillMd(null);
    skillMdModal.open();
  };

  const openEditModal = (skill: SkillInfo) => {
    if (skill.skillType === 'skill.md') {
      setEditingSkillMd(skill);
      skillMdModal.open();
    } else {
      setEditingSkill(skill);
      formModal.open();
    }
  };

  const handleTestSkill = async (skill: SkillInfo) => {
    setTestingSkill(skill);
    testModal.open();
    setTestLoading(true);
    setTestResult(null);
    try {
      const res = await api.post<SkillTestResult>(`/api/skills/${encodeURIComponent(skill.name)}/test?dryRun=true`, {});
      setTestResult(res);
    } catch {
      setTestResult({ success: false, errors: ['Failed to test skill'], warnings: [], skillType: null });
    } finally {
      setTestLoading(false);
    }
  };

  const openDeleteModal = (skill: SkillInfo) => {
    setDeletingSkill(skill);
    deleteModal.open();
  };

  const reloadSkills = async () => {
    try {
      await api.post('/api/gateway/restart', {});
    } catch { /* silent */ }
  };

  const handleDelete = async () => {
    if (!deletingSkill) return;
    try {
      const skillName = deletingSkill.fileName.replace('.json', '').replace('/SKILL.md', '');
      await api.del(`/api/skills/${skillName}`);
      toast.success(t('skills.deleteSuccess'));
      deleteModal.close();
      await loadSkills();
      await reloadSkills();
    } catch {
      toast.danger(t('skills.deleteFailed') || t('common.error'));
    }
  };

  const handleImportFromFile = () => {
    fileInputRef.current?.click();
  };

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    try {
      if (file.name.endsWith('.zip')) {
        const formData = new FormData();
        formData.append('file', file);
        const headers: Record<string, string> = {};
        const token = getToken();
        if (token) headers['Authorization'] = `Bearer ${token}`;
        const response = await fetch(`${BASE_URL}/api/skills/import`, {
          method: 'POST',
          headers,
          body: formData,
        });
        const result = await response.json();
        if (!result.success) throw new Error(result.error || 'Import failed');
      } else {
        const text = await file.text();
        const skillData = JSON.parse(text);
        await api.post('/api/skills', skillData);
      }
      toast.success(t('skills.importSuccess'));
      await loadSkills();
      await reloadSkills();
    } catch {
      toast.danger(t('skills.importFailed'));
    }

    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const handleImportFromUrl = async () => {
    if (!importUrl.trim()) {
      toast.danger(t('skills.urlRequired'));
      return;
    }

    try {
      setImporting(true);
      const isZip = importUrl.trim().toLowerCase().endsWith('.zip');

      if (isZip) {
        const headers: Record<string, string> = { 'Content-Type': 'application/json' };
        const token = getToken();
        if (token) headers['Authorization'] = `Bearer ${token}`;
        const response = await fetch(`${BASE_URL}/api/skills/import-url`, {
          method: 'POST',
          headers,
          body: JSON.stringify({ url: importUrl.trim() }),
        });
        const result = await response.json();
        if (!result.success) throw new Error(result.error || 'Import failed');
      } else {
        const response = await fetch(importUrl);
        if (!response.ok) throw new Error('Failed to fetch');
        const skillData = await response.json();
        await api.post('/api/skills', skillData);
      }

      toast.success(t('skills.importSuccess'));
      importModal.close();
      setImportUrl('');
      await loadSkills();
      await reloadSkills();
    } catch {
      toast.danger(t('skills.importFailed'));
    } finally {
      setImporting(false);
    }
  };

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4">
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-2xl font-bold">{t('skills.title')}</h1>
            <p className="text-sm text-gray-500 mt-1">{t('skills.restartHint')}</p>
          </div>
          <div className="flex gap-2">
            <Button variant="secondary" onPress={handleImportFromFile}>
              {t('skills.importFromFile')}
            </Button>
            <Button variant="secondary" onPress={importModal.open}>
              {t('skills.importFromUrl')}
            </Button>
            <Button onPress={openAddModal}>
              {t('skills.addSkill')}
            </Button>
          </div>
        </div>

        <input
          ref={fileInputRef}
          type="file"
          accept=".json,.zip"
          onChange={handleFileChange}
          style={{ display: 'none' }}
        />

        <SkillsTable
          skills={skills}
          loading={loading}
          searchQuery={searchQuery}
          onSearchChange={setSearchQuery}
          onEdit={openEditModal}
          onDelete={openDeleteModal}
          onTest={handleTestSkill}
        />

        <SkillFormModal
          isOpen={formModal.isOpen}
          onOpenChange={formModal.toggle}
          onClose={formModal.close}
          editingSkill={editingSkill}
          onSaved={loadSkillsAndReload}
        />

        <SkillMdEditModal
          isOpen={skillMdModal.isOpen}
          onOpenChange={skillMdModal.toggle}
          onClose={skillMdModal.close}
          skill={editingSkillMd}
          onSaved={loadSkillsAndReload}
        />

        {/* Add type selection modal */}
        <Modal>
          <Modal.Backdrop isOpen={addTypeModal.isOpen} onOpenChange={addTypeModal.toggle}>
            <Modal.Container size="lg">
              <Modal.Dialog>
                <Modal.Header>
                  <Modal.Heading>{t('skills.chooseType')}</Modal.Heading>
                </Modal.Header>
                <Modal.Body>
                  <div className="grid grid-cols-2 gap-4 p-2">
                    <button
                      className="p-4 border border-gray-200 dark:border-gray-700 rounded-lg hover:border-blue-500 dark:hover:border-blue-400 transition-colors text-left"
                      onClick={handleAddJson}
                    >
                      <div className="font-medium mb-1">JSON Skill</div>
                      <div className="text-sm text-gray-500">{t('skills.jsonSkillDesc')}</div>
                    </button>
                    <button
                      className="p-4 border border-gray-200 dark:border-gray-700 rounded-lg hover:border-green-500 dark:hover:border-green-400 transition-colors text-left"
                      onClick={handleAddSkillMd}
                    >
                      <div className="font-medium mb-1">SKILL.md</div>
                      <div className="text-sm text-gray-500">{t('skills.skillMdDesc')}</div>
                    </button>
                  </div>
                </Modal.Body>
                <Modal.Footer>
                  <Button variant="secondary" onPress={addTypeModal.close}>
                    {t('common.cancel')}
                  </Button>
                </Modal.Footer>
              </Modal.Dialog>
            </Modal.Container>
          </Modal.Backdrop>
        </Modal>

        {/* Import from URL modal */}
        <Modal>
          <Modal.Backdrop isOpen={importModal.isOpen} onOpenChange={importModal.toggle}>
            <Modal.Container size="lg">
              <Modal.Dialog>
                <Modal.Header>
                  <Modal.Heading>{t('skills.importFromUrl')}</Modal.Heading>
                </Modal.Header>
                <Modal.Body>
                  <TextField variant="secondary" value={importUrl} onChange={setImportUrl}>
                    <Label>{t('skills.skillUrl')}</Label>
                    <Input placeholder="https://example.com/skill.json" />
                  </TextField>
                </Modal.Body>
                <Modal.Footer>
                  <Button variant="secondary" onPress={importModal.close}>
                    {t('common.cancel')}
                  </Button>
                  <Button onPress={handleImportFromUrl} isDisabled={importing}>
                    {importing ? t('skills.importing') : t('common.confirm')}
                  </Button>
                </Modal.Footer>
              </Modal.Dialog>
            </Modal.Container>
          </Modal.Backdrop>
        </Modal>

        {/* Delete confirmation modal */}
        <Modal>
          <Modal.Backdrop isOpen={deleteModal.isOpen} onOpenChange={deleteModal.toggle}>
            <Modal.Container size="lg">
              <Modal.Dialog role="alertdialog">
                <Modal.Header>
                  <Modal.Heading>{t('skills.deleteSkill')}</Modal.Heading>
                </Modal.Header>
                <Modal.Body>
                  <p>{t('skills.deleteConfirm')}</p>
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

        {/* Test Skill Result Modal */}
        <Modal>
          <Modal.Backdrop isOpen={testModal.isOpen} onOpenChange={testModal.toggle}>
            <Modal.Container size="lg">
              <Modal.Dialog>
                <Modal.Header>
                  <Modal.Heading>
                    {t('skills.testResult')} - {testingSkill?.name}
                  </Modal.Heading>
                </Modal.Header>
                <Modal.Body>
                  {testLoading ? (
                    <div className="flex justify-center py-8"><Spinner size="lg" /></div>
                  ) : testResult ? (
                    <div className="space-y-4">
                      {/* Status */}
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-medium">{t('skills.status')}:</span>
                        <Chip size="sm" color={testResult.success ? 'success' : 'danger'} variant="soft">
                          {testResult.success ? t('skills.testPassed') : t('skills.testFailed')}
                        </Chip>
                      </div>

                      {/* Errors */}
                      {testResult.errors && testResult.errors.length > 0 && (
                        <div>
                          <div className="text-sm font-medium text-danger mb-1">{t('skills.testErrors')}:</div>
                          {testResult.errors.map((e, i) => (
                            <div key={i} className="text-sm text-danger bg-danger/10 rounded px-3 py-1 mb-1">{e}</div>
                          ))}
                        </div>
                      )}

                      {/* Warnings */}
                      {testResult.warnings && testResult.warnings.length > 0 && (
                        <div>
                          <div className="text-sm font-medium text-warning mb-1">{t('skills.testWarnings')}:</div>
                          {testResult.warnings.map((w, i) => (
                            <div key={i} className="text-sm text-warning bg-warning/10 rounded px-3 py-1 mb-1">{w}</div>
                          ))}
                        </div>
                      )}

                      {/* Dependency Check */}
                      {testResult.dependencyCheck && (
                        <div>
                          <div className="text-sm font-medium mb-1">{t('skills.dependencies')}:</div>
                          <div className="flex flex-wrap gap-1">
                            {testResult.dependencyCheck.available.map(d => (
                              <Chip key={d} size="sm" color="success" variant="soft">{d} ({t('skills.dependencyAvailable')})</Chip>
                            ))}
                            {testResult.dependencyCheck.missing.map(d => (
                              <Chip key={d} size="sm" color="danger" variant="soft">{d} ({t('skills.dependencyMissing')})</Chip>
                            ))}
                          </div>
                        </div>
                      )}

                      {/* Dry Run Output */}
                      {testResult.dryRunOutput && (
                        <div>
                          <div className="text-sm font-medium mb-1">{t('skills.dryRun')}:</div>
                          <pre className="text-xs bg-content2 rounded-lg p-3 overflow-x-auto whitespace-pre-wrap">{testResult.dryRunOutput}</pre>
                        </div>
                      )}
                    </div>
                  ) : (
                    <p className="text-center py-8 text-gray-500">{t('common.noData')}</p>
                  )}
                </Modal.Body>
                <Modal.Footer>
                  <Button variant="secondary" onPress={testModal.close}>
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

export default SkillsPage;
