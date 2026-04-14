import { useState, useEffect } from 'react';
import { Card, Button, Spinner, Chip, toast } from '@heroui/react';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import type { SessionInfo, SessionsResponse } from './types';

interface SessionSelectorProps {
  onSessionSwitched: () => void;
}

const formatFileSize = (bytes: number) => {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
};

const shortId = (fileName: string) => {
  const name = fileName.replace('.db', '');
  return name.length > 8 ? name.substring(0, 8) + '...' : name;
};

function SessionSelector({ onSessionSwitched }: SessionSelectorProps) {
  const { t } = useI18n();
  const [sessions, setSessions] = useState<SessionInfo[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    loadSessions();
  }, []);

  const loadSessions = async () => {
    try {
      setLoading(true);
      const res = await api.get<SessionsResponse>('/api/sessions');
      if (res.success) {
        setSessions(res.data);
      }
    } catch {
      // silently fail
    } finally {
      setLoading(false);
    }
  };

  const createSession = async () => {
    try {
      await api.post('/api/sessions', {});
      toast.success(t('memory.createSuccess'));
      loadSessions();
    } catch {
      toast.danger(t('settings.saveFailed'));
    }
  };

  const switchSession = async (fileName: string) => {
    if (!confirm(t('memory.switchConfirm'))) return;
    try {
      await api.put('/api/sessions/switch', { session: fileName });
      toast.success(t('memory.switchSuccess'));
      loadSessions();
      onSessionSwitched();
    } catch {
      toast.danger(t('settings.saveFailed'));
    }
  };

  const deleteSession = async (fileName: string) => {
    if (!confirm(t('memory.deleteSessionConfirm'))) return;
    try {
      await api.del(`/api/sessions/${encodeURIComponent(fileName)}`);
      toast.success(t('memory.deleteSessionSuccess'));
      loadSessions();
    } catch {
      toast.danger(t('settings.saveFailed'));
    }
  };

  return (
    <Card className="mb-6">
      <Card.Header>
        <div className="flex items-center justify-between w-full">
          <Card.Title>{t('memory.currentSession')}</Card.Title>
          <Button size="sm" onPress={createSession}>
            {t('memory.createSession')}
          </Button>
        </div>
      </Card.Header>
      <Card.Content>
        {loading ? (
          <div className="flex justify-center py-4">
            <Spinner size="sm" />
          </div>
        ) : sessions.length === 0 ? (
          <p className="text-gray-500 text-center py-4">{t('memory.noSessions')}</p>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
            {sessions.map((session) => (
              <Card
                key={session.fileName}
                className={`border-[.5px] hover:scale-101 cursor-pointer transition-all ${
                  session.isCurrent
                    ? 'bg-blue-50 dark:bg-blue-900/30 border-blue-300 dark:border-blue-500/10 scale-101'
                    : 'bg-gray-50 dark:bg-gray-800 border-gray-200 dark:border-gray-700 hover:border-gray-300 dark:hover:border-gray-600'
                }`}
              >
                <div className="flex items-center justify-between mb-2">
                  <div className="flex items-center gap-2">
                    <span className="font-mono text-sm font-medium" title={session.fileName}>
                      {shortId(session.fileName)}
                    </span>
                    {session.isCurrent && (
                      <Chip size="sm" color="accent" variant="soft">{t('memory.currentSession')}</Chip>
                    )}
                  </div>
                </div>
                <div className="text-xs text-gray-500 space-y-1 mb-2">
                  <div>{t('memory.messageCount')}: {session.messageCount}</div>
                  <div>{t('memory.soulCount')}: {session.soulCount}</div>
                  <div>{t('memory.lastModified')}: {new Date(session.lastModified).toLocaleString()}</div>
                  <div>{formatFileSize(session.size)}</div>
                </div>
                <div className="flex gap-2">
                  {!session.isCurrent && (
                    <>
                      <Button size="sm" variant="secondary" onPress={() => switchSession(session.fileName)}>
                        {t('memory.switchSession')}
                      </Button>
                      <Button size="sm" variant="danger" onPress={() => deleteSession(session.fileName)}>
                        {t('memory.deleteSession')}
                      </Button>
                    </>
                  )}
                </div>
              </Card>
            ))}
          </div>
        )}
      </Card.Content>
    </Card>
  );
}

export default SessionSelector;
