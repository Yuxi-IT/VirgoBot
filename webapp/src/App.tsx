import { useState, useEffect } from 'react';
import { BottomNav } from './layout/BottomNav';
import Navbar from './layout/Navbar';
import AppRoutes from './routes';
import { siteConfig } from './config/site';
import { Bars } from '@gravity-ui/icons';
import { Button } from '@heroui/react';

export function setTitle(title: string){
  document.title = siteConfig.name + (title ? ` - ${title}` : '');
}

const Navigation = () => {
  const [isSmallScreen, setIsSmallScreen] = useState(false);
  const [sidebarOpen, setSidebarOpen] = useState(false);

  useEffect(() => {
    const checkScreenSize = () => {
      setIsSmallScreen(window.innerWidth < 640);
    };

    checkScreenSize();

    window.addEventListener('resize', checkScreenSize);

    return () => window.removeEventListener('resize', checkScreenSize);
  }, []);

  return (
    <>
      {/* Always render Navbar, pass mobile props */}
      <Navbar isSmallScreen={isSmallScreen} isOpen={sidebarOpen} onClose={() => setSidebarOpen(false)} />

      {/* Mobile hamburger button */}
      {isSmallScreen && (
        <Button
          className={`fixed top-4 left-4 z-50 bg-white/80 dark:bg-gray-800/80 backdrop-blur-sm shadow-md ${sidebarOpen ? 'hidden' : ''}`}
          onClick={() => setSidebarOpen(true)}
          isIconOnly
        >
          <Bars className="w-5 h-5 text-gray-900 dark:text-gray-100" />
        </Button>
      )}

      {/* Mobile overlay */}
      {isSmallScreen && sidebarOpen && (
        <div
          className="fixed inset-0 bg-black/30 z-30"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {isSmallScreen && <BottomNav />}
      <div className={`${isSmallScreen ? 'pt-[44px]' : 'ml-54'}`}>
        <AppRoutes/>
      </div>
    </>
  );
};

export default Navigation;
