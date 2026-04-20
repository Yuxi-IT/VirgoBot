import { useEffect, useState, useCallback, useRef } from 'react';
import { Spinner, Alert, Button, Modal, Card, Link, CloseButton } from '@heroui/react';
import { useOverlayState } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';
import StatusCards from './StatusCards';
import ChannelStatusGrid from './ChannelStatusGrid';
import ServerConfigCard from './ServerConfigCard';
import type { StatusData, ApiResponse } from './types';
import { ArrowsRotateRight, LogoGithub } from '@gravity-ui/icons';

function DashboardPage() {
  const { t } = useI18n();
  const [status, setStatus] = useState<StatusData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [restarting, setRestarting] = useState(false);
  const restartModal = useOverlayState();
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const loadStatus = useCallback(async (silent = false) => {
    try {
      if (!silent) {
        setLoading(true);
        setError(null);
      }
      const res = await api.get<ApiResponse>('/api/status');
      if (res.success) {
        setStatus(res.data);
        if (error) setError(null);
      }
    } catch (err) {
      if (!silent) {
        setError(err instanceof Error ? err.message : 'Unknown error');
      }
    } finally {
      if (!silent) setLoading(false);
    }
  }, [error]);

  useEffect(() => {
    loadStatus();
    intervalRef.current = setInterval(() => loadStatus(true), 1000);
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, []);

  const handleRestart = async () => {
    restartModal.close();
    setRestarting(true);
    try {
      await api.post<{ success: boolean }>('/api/gateway/restart', {});
      await loadStatus(true);
    } catch {
      // will be reflected in status
    } finally {
      setRestarting(false);
    }
  };

  if (loading) {
    return (
      <DefaultLayout>
        <div className="flex items-center justify-center h-[60vh]">
          <Spinner size="lg" />
        </div>
      </DefaultLayout>
    );
  }

  if (error) {
    return (
      <DefaultLayout>
        <div className="container mx-auto p-4">
          <Alert status="danger">
            <Alert.Content>
              <Alert.Title>{t('common.error')}</Alert.Title>
              <Alert.Description>{error}</Alert.Description>
            </Alert.Content>
          </Alert>
        </div>
      </DefaultLayout>
    );
  }

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4">
        <div className="flex items-center justify-between mb-6">
          <h1 className="text-2xl font-bold">{t('dashboard.title')}</h1>
          <Button
            variant="danger"
            isIconOnly
            onPress={restartModal.open}
            isDisabled={restarting}
          >
            {restarting ? (
              <><Spinner size="sm" className="mr-2" /><ArrowsRotateRight/></>
            ) : (
              <ArrowsRotateRight/>
            )}
          </Button>
        </div>
        <div className='mb-4 flex flex-col gap-4 md:flex-row'>
          <Card className="w-full md:w-[50%]">
            <LogoGithub aria-label="Dollar sign icon" className="text-primary size-6" role="img" />
            <Card.Header>
              <Card.Title>你好！感谢使用VirgoBot！</Card.Title>
              <Card.Description>
                VirgoBot是基于 .NET 10 的多通道 AI 助手框架
              </Card.Description>
            </Card.Header>
            <Card.Footer>
              <Link
                aria-label="goto github"
                href="https://github.com/Yuxi-IT/VirgoBot"
                rel="noopener noreferrer"
                target="_blank"
              >
                Github
                <Link.Icon aria-hidden="true" />
              </Link>
            </Card.Footer>
          </Card>

          <Card className="w-full md:w-[50%] md:flex-row">
            <div className="relative h-[140px] w-full shrink-0 overflow-hidden rounded-2xl sm:h-[120px] sm:w-[120px]">
              <img
                alt="Cherries"
                className="rounded-[45px] pointer-events-none absolute inset-0 h-full w-full object-cover select-none"
                loading="lazy"
                src="./icon.png"
              />
            </div>
            <div className="flex flex-1 flex-col gap-3">
              <Card.Header className="gap-1">
                <Card.Title className="pr-8">它有什么亮点？</Card.Title>
                <Card.Description>
                  你可以导入自己的设定，可以是动画中的虚拟人物，也可以是小说中的角色，甚至是你自己！<br/>
                  原生支持多种多样的Skill，也可以设置定时任务，帮你随时收集数据
                </Card.Description>
              </Card.Header>
            </div>
          </Card>
        </div>

        {/* Restart Confirmation Modal */}
        <Modal>
          <Modal.Backdrop isOpen={restartModal.isOpen} onOpenChange={restartModal.toggle}>
            <Modal.Container size="lg">
              <Modal.Dialog role="alertdialog">
                <Modal.Header>
                  <Modal.Heading>{t('gateway.restart')}</Modal.Heading>
                </Modal.Header>
                <Modal.Body>
                  <p>{t('gateway.restartConfirm')}</p>
                </Modal.Body>
                <Modal.Footer>
                  <Button variant="secondary" onPress={restartModal.close}>
                    {t('common.cancel')}
                  </Button>
                  <Button variant="danger" onPress={handleRestart}>
                    {t('common.confirm')}
                  </Button>
                </Modal.Footer>
              </Modal.Dialog>
            </Modal.Container>
          </Modal.Backdrop>
        </Modal>

        {status && (
          <>
            <StatusCards status={status} />
            <ChannelStatusGrid channels={status.channels} />
            <ServerConfigCard server={status.server} />
          </>
        )}
      </div>
    </DefaultLayout>
  );
}

export default DashboardPage;
