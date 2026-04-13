import { Modal, useOverlayState, Button } from '@heroui/react';
import { createContext, useContext, useState, ReactNode, useEffect } from 'react';
import { ArrowDownToSquare } from '@gravity-ui/icons';

interface ImagePreviewContextType {
  showImage: (src: string, type?: 'image' | 'video') => void;
}

const ImagePreviewContext = createContext<ImagePreviewContextType | null>(null);

export function ImagePreviewProvider({ children }: { children: ReactNode }) {
  const [mediaSrc, setMediaSrc] = useState<string | null>(null);
  const [mediaType, setMediaType] = useState<'image' | 'video'>('image');
  const state = useOverlayState();

  const showImage = (src: string, type: 'image' | 'video' = 'image') => {
    setMediaSrc(src);
    setMediaType(type);
    state.open();
    window.history.pushState({ modal: true }, '');
  };

  const handleSave = async () => {
    if (!mediaSrc) return;

    const a = document.createElement('a');
      a.href = mediaSrc;
      a.download = `media_${Date.now()}`;
      a.click();
  };

  useEffect(() => {
    const handlePopState = () => {
      if (state.isOpen) {
        state.close();
      }
    };

    window.addEventListener('popstate', handlePopState);
    return () => window.removeEventListener('popstate', handlePopState);
  }, [state.isOpen]);

  useEffect(() => {
    if (!state.isOpen && window.history.state?.modal) {
      window.history.back();
    }
  }, [state.isOpen]);

  return (
    <ImagePreviewContext.Provider value={{ showImage }}>
      {children}
      <Modal.Backdrop variant="blur" isOpen={state.isOpen} onOpenChange={state.setOpen}>
        <Modal.Container placement="center">
          <Modal.Dialog className="max-w-[90vw] max-h-[90vh]">
            <Modal.Body className="p-0 relative">
              {mediaSrc && mediaType === 'image' && <img src={mediaSrc} alt="" className="w-full h-full object-contain rounded-lg" />}
              {mediaSrc && mediaType === 'video' && <video src={mediaSrc} controls className="w-full h-full object-contain rounded-lg" />}
              <Button isIconOnly className="absolute bottom-4 right-4" onClick={handleSave}>
                <ArrowDownToSquare className="w-5 h-5" />
              </Button>
            </Modal.Body>
          </Modal.Dialog>
        </Modal.Container>
      </Modal.Backdrop>
    </ImagePreviewContext.Provider>
  );
}

export function useImagePreview() {
  const context = useContext(ImagePreviewContext);
  if (!context) throw new Error('useImagePreview must be used within ImagePreviewProvider');
  return context;
}
