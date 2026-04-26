import { useState, useRef } from 'react';
import { Button, Label, Spinner, Switch, TextArea, toast } from '@heroui/react';
import { Microphone, ArrowShapeTurnUpRight, Paperclip, Picture } from '@gravity-ui/icons';
import { useI18n } from '../../i18n';
import { api } from '../../services/api';

export interface ImageAttachment {
  /** 'url' = online URL, 'base64' = local file */
  type: 'url' | 'base64';
  data: string;
  mediaType?: string;
  /** preview src for display */
  preview: string;
}

interface Props {
  sending: boolean;
  voiceFeedback: boolean;
  splitEnabled: boolean;
  markdownEnabled: boolean;
  onSend: (text: string, images?: ImageAttachment[]) => void;
  onToggleVoiceFeedback: () => void;
  onToggleSplit: () => void;
  onToggleMarkdown: () => void;
}

export default function ChatInput({
  sending, voiceFeedback, splitEnabled, markdownEnabled,
  onSend, onToggleVoiceFeedback, onToggleSplit, onToggleMarkdown
}: Props) {
  const { t } = useI18n();
  const [text, setText] = useState('');
  const [recording, setRecording] = useState(false);
  const [processing, setProcessing] = useState(false);
  const [images, setImages] = useState<ImageAttachment[]>([]);
  const [urlInput, setUrlInput] = useState('');
  const [showUrlInput, setShowUrlInput] = useState(false);
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const chunksRef = useRef<Blob[]>([]);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleSend = () => {
    if ((!text.trim() && images.length === 0) || sending) return;
    onSend(text.trim(), images.length > 0 ? images : undefined);
    setText('');
    setImages([]);
    setUrlInput('');
    setShowUrlInput(false);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const addUrlImage = () => {
    const url = urlInput.trim();
    if (!url) return;
    setImages(prev => [...prev, { type: 'url', data: url, preview: url }]);
    setUrlInput('');
    setShowUrlInput(false);
  };

  const handleFileSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files ?? []);
    if (files.length === 0) return;
    for (const file of files) {
      if (!file.type.startsWith('image/')) continue;
      const base64 = await fileToBase64(file);
      const preview = `data:${file.type};base64,${base64}`;
      setImages(prev => [...prev, { type: 'base64', data: base64, mediaType: file.type, preview }]);
    }
    // Reset input so same file can be re-selected
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  const removeImage = (idx: number) => {
    setImages(prev => prev.filter((_, i) => i !== idx));
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
          <Switch.Control><Switch.Thumb /></Switch.Control>
          <Switch.Content><Label className="text-sm">{t('chatPage.voiceFeedback')}</Label></Switch.Content>
        </Switch>
        <Switch isSelected={splitEnabled} onChange={onToggleSplit}>
          <Switch.Control><Switch.Thumb /></Switch.Control>
          <Switch.Content><Label className="text-sm">{t('chatPage.messageSplit')}</Label></Switch.Content>
        </Switch>
        <Switch isSelected={markdownEnabled} onChange={onToggleMarkdown}>
          <Switch.Control><Switch.Thumb /></Switch.Control>
          <Switch.Content><Label className="text-sm">{t('chatPage.markdown')}</Label></Switch.Content>
        </Switch>
      </div>

      {/* Image previews */}
      {images.length > 0 && (
        <div className="flex gap-2 flex-wrap mt-2">
          {images.map((img, idx) => (
            <div key={idx} className="relative group">
              <img
                src={img.preview}
                alt=""
                className="h-16 w-16 object-cover rounded-lg border border-default-300"
              />
              <button
                onClick={() => removeImage(idx)}
                className="absolute -top-1 -right-1 bg-danger text-white rounded-full w-4 h-4 flex items-center justify-center text-xs opacity-0 group-hover:opacity-100 transition-opacity"
              >
                ×
              </button>
            </div>
          ))}
        </div>
      )}

      {/* URL input row */}
      {showUrlInput && (
        <div className="flex gap-2 mt-2">
          <input
            className="flex-1 text-sm border border-default-300 rounded-lg px-2 py-1 bg-transparent outline-none focus:border-primary"
            placeholder={t('chatPage.imageUrlPlaceholder')}
            value={urlInput}
            onChange={e => setUrlInput(e.target.value)}
            onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); addUrlImage(); } }}
            autoFocus
          />
          <Button size="sm" variant="primary" onPress={addUrlImage} isDisabled={!urlInput.trim()}>
            {t('chatPage.addImage')}
          </Button>
          <Button size="sm" variant="secondary" onPress={() => { setShowUrlInput(false); setUrlInput(''); }}>
            {t('common.cancel')}
          </Button>
        </div>
      )}

      <div className="flex gap-2 items-end m-1">
        <TextArea
          className="flex-1 text-[16px]"
          rows={1}
          value={text}
          onChange={(e) => setText(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={t('chatPage.inputPlaceholder')}
        />

        {/* Image attach button */}
        <Button
          size="sm"
          isIconOnly
          variant="secondary"
          onPress={() => setShowUrlInput(v => !v)}
        >
          <Paperclip />
        </Button>
        <input
          ref={fileInputRef}
          type="file"
          accept="image/*"
          multiple
          className="hidden"
          onChange={handleFileSelect}
        />
        <Button
          size="sm"
          isIconOnly
          variant="secondary"
          onPress={() => fileInputRef.current?.click()}
        >
          <Picture />
        </Button>

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
          isDisabled={(!text.trim() && images.length === 0) || sending}
        >
          {sending ? <Spinner size="sm" /> : <ArrowShapeTurnUpRight />}
        </Button>
      </div>
    </div>
  );
}

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = reader.result as string;
      // Strip data URL prefix
      resolve(result.split(',')[1]);
    };
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}
