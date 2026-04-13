import { createContext, useContext, useState, ReactNode } from 'react';
import en from './locales/en';
import zh from './locales/zh';

type Locale = 'en' | 'zh';
type Messages = Record<string, string | Record<string, string>>;

const messages: Record<Locale, Messages> = { en, zh };

const getInitialLocale = (): Locale => {
  const saved = localStorage.getItem('locale');
  if (saved === 'en' || saved === 'zh') return saved;
  return navigator.language.startsWith('zh') ? 'zh' : 'en';
};

const I18nContext = createContext<{
  locale: Locale;
  setLocale: (locale: Locale) => void;
  t: (key: string) => string;
} | null>(null);

export function I18nProvider({ children }: { children: ReactNode }) {
  const [locale, setLocaleState] = useState<Locale>(getInitialLocale);

  const setLocale = (newLocale: Locale) => {
    setLocaleState(newLocale);
    localStorage.setItem('locale', newLocale);
  };

  const t = (key: string): string => {
    const keys = key.split('.');
    let value: any = messages[locale];
    for (const k of keys) {
      value = value?.[k];
    }
    return value || key;
  };

  return (
    <I18nContext.Provider value={{ locale, setLocale, t }}>
      {children}
    </I18nContext.Provider>
  );
}

export function useI18n() {
  const context = useContext(I18nContext);
  if (!context) throw new Error('useI18n must be used within I18nProvider');
  return context;
}
