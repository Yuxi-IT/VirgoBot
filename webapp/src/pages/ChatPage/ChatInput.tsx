import { useState, useRef } from 'react';
import { Button, Spinner, Switch, TextArea, toast } from '@heroui/react';
import { Microphone, ArrowShapeTurnUpRight, Volume } from '@gravity-ui/icons';
import { api } from '../../services/api';

interface Props {
  sending: boolean;
  voiceFeedback: boolean;
  onSend: (text: string) => void;
  onToggleVoiceFeedback: () => void;
}

export default function ChatInput({ sending, voiceFeedback, onSend, onToggleVoiceFeedback }: Props) {
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
      toast.danger('无法访问麦克风');
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
        toast.danger('语音识别失败');
      }
    } catch {
      toast.danger('语音处理失败');
    } finally {
      setProcessing(false);
    }
  };

  const micAvailable = !!navigator.mediaDevices?.getUserMedia;

  return (
    <div className="border-t p-3">
      <div className="flex gap-2 items-end">
        <TextArea
          className="flex-1"
          rows={1}
          value={text}
          onChange={(e) => setText(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="输入消息..."
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
      <div className="flex items-center gap-1 mt-2 text-xs text-default-400">
        <Volume className="w-3 h-3" />
        <span>语音反馈</span>
        <Switch size="sm" isSelected={voiceFeedback} onChange={onToggleVoiceFeedback} />
      </div>
    </div>
  );
}
