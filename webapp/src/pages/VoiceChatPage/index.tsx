import { useState, useRef, useEffect } from 'react';
import { Button, Card, Spinner, toast, TextField, Input } from '@heroui/react';
import DefaultLayout from '../../layout/DefaultLayout';
import { useI18n } from '../../i18n';
import { api, BASE_URL } from '../../services/api';
import { Microphone, Stop, Volume } from '@gravity-ui/icons';

interface AsrResponse {
  success: boolean;
  data: {
    text: string;
    duration: number;
    logId: string;
  };
}

interface TtsResponse {
  success: boolean;
  data: {
    audioBase64: string;
    audioSize: number;
    logId: string;
  };
}

interface WsMessage {
  type: string;
  content?: string;
  message?: string;
  userId?: string;
}

function VoiceChatPage() {
  const { t } = useI18n();
  const [isRecording, setIsRecording] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);
  const [transcript, setTranscript] = useState('');
  const [botResponse, setBotResponse] = useState('');
  const [isPlaying, setIsPlaying] = useState(false);
  const [micUnavailable, setMicUnavailable] = useState(false);
  const [textInput, setTextInput] = useState('');

  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const audioChunksRef = useRef<Blob[]>([]);
  const wsRef = useRef<WebSocket | null>(null);
  const audioRef = useRef<HTMLAudioElement | null>(null);

  // Check mic API availability (requires HTTPS on mobile)
  useEffect(() => {
    if (!navigator.mediaDevices?.getUserMedia) {
      setMicUnavailable(true);
    }
  }, []);

  // Initialize WebSocket connection
  useEffect(() => {
    const wsUrl = BASE_URL.replace(/^http/, 'ws') + '/';
    const ws = new WebSocket(wsUrl);

    ws.onopen = () => {
      console.log('WebSocket connected');
    };

    ws.onmessage = (event) => {
      try {
        const data: WsMessage = JSON.parse(event.data);
        if (data.type === 'sendMessage' && data.content) {
          setBotResponse(data.content);
          setIsProcessing(false);
          // Convert bot response to speech
          convertTextToSpeech(data.content);
        }
      } catch (err) {
        console.error('WebSocket message error:', err);
      }
    };

    ws.onerror = (error) => {
      console.error('WebSocket error:', error);
      //toast.danger(t('voiceChat.wsError'));
    };

    ws.onclose = () => {
      console.log('WebSocket disconnected');
    };

    wsRef.current = ws;

    return () => {
      ws.close();
    };
  }, [t]);

  const startRecording = async () => {
    try {
      // iOS Safari 不支持 sampleRate 约束，只请求基本音频权限
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });

      // Try to use audio/webm;codecs=opus, fallback to mp4 (iOS), then default
      let mimeType = 'audio/webm;codecs=opus';
      if (!MediaRecorder.isTypeSupported(mimeType)) {
        mimeType = 'audio/webm';
        if (!MediaRecorder.isTypeSupported(mimeType)) {
          mimeType = 'audio/mp4';
          if (!MediaRecorder.isTypeSupported(mimeType)) {
            mimeType = ''; // Use default
          }
        }
      }

      const options = mimeType ? { mimeType } : {};
      const mediaRecorder = new MediaRecorder(stream, options);

      audioChunksRef.current = [];

      mediaRecorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
          audioChunksRef.current.push(event.data);
        }
      };

      mediaRecorder.onstop = async () => {
        const audioBlob = new Blob(audioChunksRef.current, {
          type: mimeType || 'audio/webm'
        });
        await processAudio(audioBlob);
        stream.getTracks().forEach(track => track.stop());
      };

      mediaRecorder.start();
      mediaRecorderRef.current = mediaRecorder;
      setIsRecording(true);
      setTranscript('');
      setBotResponse('');
    } catch (err) {
      console.error('Failed to start recording:', err);
      const error = err as Error;
      if (error.name === 'NotAllowedError' || error.name === 'PermissionDeniedError') {
        toast.danger(t('voiceChat.micPermissionDenied'));
      } else if (error.name === 'NotFoundError' || error.name === 'DevicesNotFoundError') {
        toast.danger(t('voiceChat.micNotFound'));
      } else {
        toast.danger(t('voiceChat.micError'));
      }
    }
  };

  const stopRecording = () => {
    if (mediaRecorderRef.current && isRecording) {
      mediaRecorderRef.current.stop();
      setIsRecording(false);
      setIsProcessing(true);
    }
  };

  const sendTextMessage = () => {
    const text = textInput.trim();
    if (!text) return;

    if (wsRef.current && wsRef.current.readyState === WebSocket.OPEN) {
      setTranscript(text);
      setBotResponse('');
      setIsProcessing(true);
      setTextInput('');
      wsRef.current.send(JSON.stringify({
        type: 'message',
        message: text,
        userId: 'voice-user',
      }));
    } else {
      toast.danger(t('voiceChat.wsNotConnected'));
    }
  };

  const processAudio = async (audioBlob: Blob) => {
    try {
      // Convert WebM to a format compatible with the backend
      // The backend expects audio data, we'll send the raw blob as base64
      const reader = new FileReader();
      reader.onloadend = async () => {
        const arrayBuffer = reader.result as ArrayBuffer;
        const base64Audio = arrayBufferToBase64(arrayBuffer);

        // Call ASR API
        try {
          const asrResponse = await api.post<AsrResponse>('/api/voice/asr', {
            audioBase64: base64Audio,
          });

          if (asrResponse.success && asrResponse.data.text) {
            const recognizedText = asrResponse.data.text;
            setTranscript(recognizedText);

            // Send to VirgoBot via WebSocket
            if (wsRef.current && wsRef.current.readyState === WebSocket.OPEN) {
              wsRef.current.send(JSON.stringify({
                type: 'message',
                message: recognizedText,
                userId: 'voice-user',
              }));
            } else {
              toast.danger(t('voiceChat.wsNotConnected'));
              setIsProcessing(false);
            }
          } else {
            toast.danger(t('voiceChat.asrFailed'));
            setIsProcessing(false);
          }
        } catch (err) {
          console.error('ASR error:', err);
          toast.danger(t('voiceChat.asrError'));
          setIsProcessing(false);
        }
      };
      reader.readAsArrayBuffer(audioBlob);
    } catch (err) {
      console.error('Audio processing error:', err);
      toast.danger(t('voiceChat.asrError'));
      setIsProcessing(false);
    }
  };

  const arrayBufferToBase64 = (buffer: ArrayBuffer): string => {
    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (let i = 0; i < bytes.byteLength; i++) {
      binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary);
  };

  const convertTextToSpeech = async (text: string) => {
    try {
      const ttsResponse = await api.post<TtsResponse>('/api/voice/tts', {
        text,
      });

      if (ttsResponse.success && ttsResponse.data.audioBase64) {
        playAudio(ttsResponse.data.audioBase64);
      } else {
        toast.danger(t('voiceChat.ttsFailed'));
      }
    } catch (err) {
      console.error('TTS error:', err);
      toast.danger(t('voiceChat.ttsError'));
    }
  };

  const playAudio = (base64Audio: string) => {
    try {
      const audioData = atob(base64Audio);
      const arrayBuffer = new ArrayBuffer(audioData.length);
      const view = new Uint8Array(arrayBuffer);
      for (let i = 0; i < audioData.length; i++) {
        view[i] = audioData.charCodeAt(i);
      }
      const blob = new Blob([arrayBuffer], { type: 'audio/mpeg' });
      const audioUrl = URL.createObjectURL(blob);

      const audio = new Audio(audioUrl);
      audioRef.current = audio;

      audio.onplay = () => setIsPlaying(true);
      audio.onended = () => {
        setIsPlaying(false);
        URL.revokeObjectURL(audioUrl);
      };
      audio.onerror = () => {
        setIsPlaying(false);
        toast.danger(t('voiceChat.playError'));
        URL.revokeObjectURL(audioUrl);
      };

      audio.play();
    } catch (err) {
      console.error('Audio playback error:', err);
      toast.danger(t('voiceChat.playError'));
    }
  };

  const stopAudio = () => {
    if (audioRef.current) {
      audioRef.current.pause();
      audioRef.current.currentTime = 0;
      setIsPlaying(false);
    }
  };

  return (
    <DefaultLayout>
      <div className="container mx-auto p-4 max-w-4xl">
        <h1 className="text-2xl font-bold mb-6">{t('voiceChat.title')}</h1>

        <Card className="p-6 mb-6">
          <div className="flex flex-col items-center space-y-6">
            {/* Recording Button */}
            <div className="flex flex-col items-center space-y-4">
              {micUnavailable ? (
                <div className="w-32 h-32 rounded-full flex items-center justify-center bg-gray-100 text-center p-2">
                  <p className="text-xs text-gray-500">{t('voiceChat.httpsRequired')}</p>
                </div>
              ) : !isRecording && !isProcessing ? (
                <Button
                  onPress={startRecording}
                  className="w-32 h-32 rounded-full"
                >
                  <Microphone className="w-12 h-12" />
                </Button>
              ) : null}

              {isRecording && (
                <Button
                  onPress={stopRecording}
                  variant="danger"
                  className="w-32 h-32 rounded-full animate-pulse"
                >
                  <Stop className="w-12 h-12" />
                </Button>
              )}

              {isProcessing && (
                <div className="w-32 h-32 flex flex-col items-center justify-center space-y-2">
                  <Spinner size="lg" />
                </div>
              )}

              {/* Stop audio button shown alongside mic when playing */}
              {isPlaying && !isRecording && !isProcessing && (
                <Button
                  onPress={stopAudio}
                  variant="secondary"
                >
                  <Volume className="w-4 h-4 mr-1" />
                  {t('voiceChat.stopPlaying')}
                </Button>
              )}
            </div>

            {/* Status Text */}
            <div className="text-center">
              {isRecording && (
                <p className="text-lg font-medium text-red-600">
                  {t('voiceChat.recording')}
                </p>
              )}
              {isProcessing && (
                <p className="text-lg font-medium text-blue-600">
                  {t('voiceChat.processing')}
                </p>
              )}
              {isPlaying && (
                <p className="text-lg font-medium text-green-600">
                  {t('voiceChat.playing')}
                </p>
              )}
              {!isRecording && !isProcessing && !isPlaying && (
                <p className="text-lg text-gray-600">
                  {t('voiceChat.tapToStart')}
                </p>
              )}
            </div>
          </div>
        </Card>

        <Card className="p-4 mb-6">
          <div className="flex gap-2">
            <TextField
              value={textInput}
              onChange={setTextInput}
              className="flex-1"
              aria-label={t('voiceChat.textInputPlaceholder')}
            >
              <Input
                placeholder={t('voiceChat.textInputPlaceholder')}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    sendTextMessage();
                  }
                }}
              />
            </TextField>
            <Button
              onPress={sendTextMessage}
              isDisabled={!textInput.trim() || isProcessing}
            >
              {t('voiceChat.send')}
            </Button>
          </div>
        </Card>

        {/* Transcript and Response */}
        {(transcript || botResponse) && (
          <div className="space-y-4">
            {transcript && (
              <Card className="p-4">
                <h3 className="text-sm font-semibold text-gray-600 mb-2">
                  {t('voiceChat.yourMessage')}
                </h3>
                <p className="text-base">{transcript}</p>
              </Card>
            )}

            {botResponse && (
              <Card className="p-4 bg-blue-50">
                <h3 className="text-sm font-semibold text-gray-600 mb-2">
                  {t('voiceChat.botResponse')}
                </h3>
                <p className="text-base">{botResponse}</p>
              </Card>
            )}
          </div>
        )}

        {/* Instructions */}
        <Card className="p-4 mt-6 bg-gray-50">
          <h3 className="text-sm font-semibold mb-2">{t('voiceChat.instructions')}</h3>
          <ol className="text-sm text-gray-700 space-y-1 list-decimal list-inside">
            <li>{t('voiceChat.step1')}</li>
            <li>{t('voiceChat.step2')}</li>
            <li>{t('voiceChat.step3')}</li>
          </ol>
        </Card>
      </div>
    </DefaultLayout>
  );
}

export default VoiceChatPage;
