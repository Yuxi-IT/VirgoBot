import { useState, useRef } from 'react';
import { Button, Label, Spinner, Switch, TextArea, toast } from '@heroui/react';
import { Microphone, ArrowShapeTurnUpRight } from '@gravity-ui/icons';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';

interface Props {
  sending: boolean;
  voiceFeedback: boolean;
  splitEnabled: boolean;
  showTime: boolean;
  markdownEnabled: boolean;
  onSend: (text: string) => void;
  onToggleVoiceFeedback: () => void;
  onToggleSplit: () => void;
  onToggleShowTime: () => void;
  onToggleMarkdown: () => void;
}

export default function ChatInput({
  sending, voiceFeedback, splitEnabled, showTime, markdownEnabled,
  onSend, onToggleVoiceFeedback, onToggleSplit, onToggleShowTime, onToggleMarkdown
}: Props) {
  const { t } = useI18n();
  const [text, setText] = useState('');
  const [recording, setRecording] = useState(false);
  const [processing, setProcessing] = useState(false);
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const chunksRef = useRef<Blob[]>([]);

  const handleSend = () => {
    if (!text.trim() || sending) return;
    onSend(text.trim());
    setText('');
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const startRecording = async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      let mimeType = 'audio/webm;codecs=opus';
      if (!MediaRecorder.isTypeSupported(mimeType)) {
        mimeType = 'audio/webm';
        if (!MediaRecorder.isTypeSupported(mimeType)) {
          mimeType = 'audio/mp4';
          if (!MediaRecorder.isTypeSupported(mimeType)) mimeType = '';
        }
      }
      const recorder = new MediaRecorder(stream, mimeType ? { mimeType } : {});
      chunksRef.current = [];
      recorder.ondataavailable = (e) => { if (e.data.size > 0) chunksRef.current.push(e.data); };
      recorder.onstop = async () => {
        const blob = new Blob(chunksRef.current, { type: mimeType || 'audio/webm' });
        stream.getTracks().forEach(t => t.stop());
        await processAudio(blob);
      };
      recorder.start();
      mediaRecorderRef.current = recorder;
      setRecording(true);
    } catch {
      toast.danger(t('chatPage.micError'));
    }
  };

  const stopRecording = () => {
    if (mediaRecorderRef.current && recording) {
      mediaRecorderRef.current.stop();
      setRecording(false);
      setProcessing(true);
    }
  };

  const processAudio = async (blob: Blob) => {
    try {
      const buf = await blob.arrayBuffer();
      const bytes = new Uint8Array(buf);
      let binary = '';
      for (let i = 0; i < bytes.byteLength; i++) binary += String.fromCharCode(bytes[i]);
      const base64 = btoa(binary);

      const res = await api.post<{ success: boolean; data: { text: string } }>('/api/voice/asr', { audioBase64: base64 });
      if (res.success && res.data.text) {
        onSend(res.data.text);
      } else {
        toast.danger(t('chatPage.asrFailed'));
      }
    } catch {
      toast.danger(t('chatPage.audioProcessFailed'));
    } finally {
      setProcessing(false);
    }
  };

  const micAvailable = !!navigator.mediaDevices?.getUserMedia;

  return (
    <div className="border-t p-3">
      <div className="flex items-center gap-3 mt-2 text-xs text-default-400 flex-wrap">
        <Switch isSelected={voiceFeedback} onChange={onToggleVoiceFeedback}>
          <Switch.Control>
            <Switch.Thumb />
          </Switch.Control>
          <Switch.Content>
            <Label className="text-sm">{t('chatPage.voiceFeedback')}</Label>
          </Switch.Content>
        </Switch>
        <Switch isSelected={splitEnabled} onChange={onToggleSplit}>
          <Switch.Control>
            <Switch.Thumb />
          </Switch.Control>
          <Switch.Content>
            <Label className="text-sm">{t('chatPage.messageSplit')}</Label>
          </Switch.Content>
        </Switch>
        <Switch isSelected={showTime} onChange={onToggleShowTime}>
          <Switch.Control>
            <Switch.Thumb />
          </Switch.Control>
          <Switch.Content>
            <Label className="text-sm">{t('chatPage.showTime')}</Label>
          </Switch.Content>
        </Switch>
        <Switch isSelected={markdownEnabled} onChange={onToggleMarkdown}>
          <Switch.Control>
            <Switch.Thumb />
          </Switch.Control>
          <Switch.Content>
            <Label className="text-sm">{t('chatPage.markdown')}</Label>
          </Switch.Content>
        </Switch>
      </div>
      <div className="flex gap-2 items-end m-1">
        <TextArea
          className="flex-1 text-[16px]"
          rows={1}
          value={text}
          onChange={(e) => setText(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={t('chatPage.inputPlaceholder')}
        />
        {micAvailable && (
          <Button
            size="sm"
            isIconOnly
            variant={recording ? 'danger' : 'secondary'}
            onPress={recording ? stopRecording : startRecording}
            isDisabled={processing}
          >
            {processing ? <Spinner size="sm" /> : <Microphone />}
          </Button>
        )}
        <Button
          size="sm"
          isIconOnly
          onPress={handleSend}
          isDisabled={!text.trim() || sending}
        >
          {sending ? <Spinner size="sm" /> : <ArrowShapeTurnUpRight />}
        </Button>
      </div>
    </div>
  );
}
