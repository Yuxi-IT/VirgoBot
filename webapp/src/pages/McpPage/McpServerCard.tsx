import { useState } from 'react';
import { Card, Button, Chip, Spinner } from '@heroui/react';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import { ArrowsRotateRight, Pencil, TrashBin, ChevronDown, ChevronUp } from '@gravity-ui/icons';
import type { McpServer, McpTool, McpToolsResponse } from './types';

interface Props {
  server: McpServer;
  onEdit: () => void;
  onDelete: () => void;
  onRestart: () => void;
}

export default function McpServerCard({ server: s, onEdit, onDelete, onRestart }: Props) {
  const { t } = useI18n();
  const [expanded, setExpanded] = useState(false);
  const [tools, setTools] = useState<McpTool[] | null>(null);

  const statusColor = (status: string): 'success' | 'warning' | 'danger' | 'default' | 'accent' => {
    switch (status) {
      case 'connected': return 'success';
      case 'connecting': return 'warning';
      case 'error': return 'danger';
      default: return 'default';
    }
  };

  const statusLabel = (status: string) => {
    switch (status) {
      case 'connected': return t('mcp.connected');
      case 'connecting': return t('mcp.connecting');
      case 'error': return t('mcp.error');
      case 'disabled': return t('mcp.disabled');
      case 'disconnected': return t('mcp.disconnected');
      default: return status;
    }
  };

  const toggleTools = async () => {
    if (expanded) { setExpanded(false); return; }
    setExpanded(true);
    if (!tools) {
      try {
        const res = await api.get<McpToolsResponse>(`/api/mcp/servers/${encodeURIComponent(s.name)}/tools`);
        if (res.success) setTools(res.data);
      } catch { /* ignore */ }
    }
  };

  return (
    <Card>
      <div className="p-4">
        <div className="flex justify-between items-center">
          <div className="flex items-center gap-3 flex-wrap">
            <span className="font-semibold text-lg">{s.name}</span>
            <Chip size="sm" color={statusColor(s.status)}>{statusLabel(s.status)}</Chip>
            <Chip size="sm" variant="soft">{s.transport.toUpperCase()}</Chip>
            {s.status === 'connected' && (
              <Chip size="sm" variant="soft" color="accent">{s.toolCount} {t('mcp.tools')}</Chip>
            )}
          </div>
          <div className="flex gap-1">
            <Button size="sm" variant="ghost" onPress={onRestart}>
              <ArrowsRotateRight className="w-4 h-4" />
            </Button>
            <Button size="sm" variant="ghost" onPress={onEdit}>
              <Pencil className="w-4 h-4" />
            </Button>
            <Button size="sm" variant="ghost" onPress={onDelete}>
              <TrashBin className="w-4 h-4" />
            </Button>
          </div>
        </div>
        {s.error && <p className="text-red-500 text-sm mt-2">{s.error}</p>}
        <p className="text-sm text-gray-500 mt-1">
          {s.transport === 'stdio' ? `${s.command} ${s.args.join(' ')}` : s.url}
        </p>
        {s.status === 'connected' && s.toolCount > 0 && (
          <div className="mt-3">
            <Button size="sm" variant="secondary" onPress={toggleTools}>
              {expanded ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
              {t('mcp.viewTools')} ({s.toolCount})
            </Button>
            {expanded && (
              <div className="mt-3 space-y-2">
                {tools ? (
                  tools.map(tool => (
                    <div key={tool.name} className="border border-gray-200 dark:border-gray-700 rounded-lg p-3">
                      <p className="font-mono text-sm font-semibold">{tool.name}</p>
                      {tool.description && <p className="text-sm text-gray-500 mt-1">{tool.description}</p>}
                      {tool.inputSchema && (
                        <pre className="text-xs bg-gray-100 dark:bg-gray-800 rounded p-2 mt-2 overflow-x-auto">
                          {JSON.stringify(tool.inputSchema, null, 2)}
                        </pre>
                      )}
                    </div>
                  ))
                ) : (
                  <Spinner size="sm" />
                )}
              </div>
            )}
          </div>
        )}
      </div>
    </Card>
  );
}
