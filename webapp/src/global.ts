export {};

declare global {
  interface Window {
    rootList: string[];
    __nativeCallbacks?: Record<string, (result: any) => void>;
  }
}
