import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Button, Dropdown, Label } from '@heroui/react';
import { navItems, siteConfig } from '../config/site';
import { useI18n } from '../i18n';
import { Globe } from '@gravity-ui/icons';

interface SidebarProps {
  isSmallScreen?: boolean;
  isOpen?: boolean;
  onClose?: () => void;
}

function Sidebar({ isSmallScreen = false, isOpen = false, onClose }: SidebarProps) {
  const { setLocale, t } = useI18n();
  const [currentUrl, setCurrentUrl] = useState(window.location.pathname);

  // On desktop: always visible (sm:translate-x-0)
  // On mobile: controlled by isOpen prop
  const translateClass = isSmallScreen
    ? (isOpen ? 'translate-x-0' : '-translate-x-full')
    : 'translate-x-0';

  return (
    <>
      <aside className={`
        fixed top-0 left-0 h-full w-50 bg-white/90 dark:bg-black/90 backdrop-blur-sm shadow-xl
        transform transition-transform duration-300 ease-in-out z-40
        ${translateClass}
      `}>
        <div className="flex flex-col h-full p-6">
          <Link to="/" className="text-3xl font-bold libre mb-8" onClick={() => onClose?.()}>
            {siteConfig.name}
          </Link>

          <nav className="flex-1 space-y-4 overflow-y-auto">
            {navItems.map((item) => (
              <Link
                key={item.url}
                to={item.url}
                className={`block py-2 px-4 rounded-[15px] hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors`
                  + (currentUrl === item.url ? ' bg-gray-200 dark:bg-gray-700' : '')
                }
                onClick={() => {
                  setCurrentUrl(item.url);
                  onClose?.();
                }}
              >
                <div className="flex items-center space-x-3">
                  <item.icon className="inline-block mr-2 w-4 h-4" />
                  {item.label ? t(`nav.${item.label.toLowerCase()}`) : ''}
                </div>
              </Link>
            ))}
          </nav>

          <div className="space-y-4 pt-4 border-t border-gray-200 dark:border-gray-800">

            <Dropdown>
              <Button
                aria-label="Language"
                variant="secondary"
                size="lg"
                className="w-full justify-start"
              >
                <Globe className="mr-2" />
                {t('common.language')}
              </Button>
              <Dropdown.Popover>
                <Dropdown.Menu onAction={(key) => setLocale(key as 'en' | 'zh')}>
                  <Dropdown.Item id="en" textValue="English">
                    <Label>English</Label>
                  </Dropdown.Item>
                  <Dropdown.Item id="zh" textValue="中文">
                    <Label>中文</Label>
                  </Dropdown.Item>
                </Dropdown.Menu>
              </Dropdown.Popover>
            </Dropdown>

          </div>
        </div>
      </aside>

    </>
  );
}

export default Sidebar;
