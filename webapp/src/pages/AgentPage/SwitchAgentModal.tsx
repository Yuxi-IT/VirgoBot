import { Button, Modal, Spinner } from '@heroui/react';
import { useI18n } from '../../i18n';

interface SwitchAgentModalProps {
  isOpen: boolean;
  agentName: string;
  processing: boolean;
  onConfirm: (createNewSession: boolean) => void;
  onCancel: () => void;
}

function SwitchAgentModal({ isOpen, agentName, processing, onConfirm, onCancel }: SwitchAgentModalProps) {
  const { t } = useI18n();

  return (
    <Modal.Backdrop isOpen={isOpen} onOpenChange={() => { if (!processing) onCancel(); }}>
      <Modal.Container>
        <Modal.Dialog>
          <Modal.Header>
            <Modal.Heading>{t('agent.switchModalTitle')}</Modal.Heading>
          </Modal.Header>
          <Modal.Body>
            <div className="space-y-3">
              <p className="text-sm">
                {t('agent.switchModalDesc').replace('{name}', agentName)}
              </p>
              <div className="p-3 bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-700 rounded-lg">
                <p className="text-sm text-amber-800 dark:text-amber-300">
                  {t('agent.switchModalWarning')}
                </p>
              </div>
              <p className="text-sm text-muted-foreground">
                {t('agent.switchModalHint')}
              </p>
            </div>
          </Modal.Body>
          <Modal.Footer>
            <Button variant="secondary" onPress={onCancel} isDisabled={processing}>
              {t('common.cancel')}
            </Button>
            <Button variant="secondary" onPress={() => onConfirm(false)} isDisabled={processing}>
              {processing ? <Spinner size="sm" className="mr-2" /> : null}
              {t('agent.switchNoNewSession')}
            </Button>
            <Button onPress={() => onConfirm(true)} isDisabled={processing}>
              {processing ? <Spinner size="sm" className="mr-2" /> : null}
              {t('agent.switchWithNewSession')}
            </Button>
          </Modal.Footer>
        </Modal.Dialog>
      </Modal.Container>
    </Modal.Backdrop>
  );
}

export default SwitchAgentModal;
