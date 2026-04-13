import { useState, useEffect } from 'react';
import { BottomNav } from './layout/BottomNav';
import Navbar from './layout/Navbar';
import AppRoutes from './routes';
import { siteConfig } from './config/site';

export function setTitle(title: string){
  document.title = siteConfig.name + (title ? ` - ${title}` : '');
}

const Navigation = () => {
  const [isSmallScreen, setIsSmallScreen] = useState(false);

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
      {!isSmallScreen && <Navbar />}
      {isSmallScreen && <BottomNav />}
      <div className={`${isSmallScreen ? '' : 'ml-54'}`}>
        <AppRoutes/>
      </div>
    </>
  );
};

export default Navigation;
