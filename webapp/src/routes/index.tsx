import { Routes, Route, useLocation } from 'react-router-dom';
import { AnimatePresence, motion } from 'framer-motion';
import { setTitle } from '../App';
import { lazy, Suspense } from 'react';
import { Spinner } from '@heroui/react';

const Dashboard = lazy(() => import('../pages/DashboardPage'));
const Chat = lazy(() => import('../pages/ChatPage'));
const Contacts = lazy(() => import('../pages/ContactsPage'));
const Settings = lazy(() => import('../pages/SettingsPage'));
const Skills = lazy(() => import('../pages/SkillsPage'));
const Tasks = lazy(() => import('../pages/TasksPage'));
const Channel = lazy(() => import('../pages/ChannelPage'));
const Providers = lazy(() => import('../pages/ProvidersPage'));
const Mcp = lazy(() => import('../pages/McpPage'));
const NotFound = lazy(() => import('../pages/NotFoundPage'));

function Loading() {
  return (
    <div className="flex items-center justify-center h-[60vh]">
      <Spinner size="lg" />
    </div>
  );
}

function AppRoutes() {
  const location = useLocation();

  setTitle("");

  return (
    <AnimatePresence mode="wait">
      <Routes location={location} key={location.pathname}>
        <Route path="/" element={<PageTransition><Suspense fallback={<Loading />}><Dashboard /></Suspense></PageTransition>} />
        <Route path="/chat" element={<PageTransition><Suspense fallback={<Loading />}><Chat /></Suspense></PageTransition>} />
        <Route path="/contacts" element={<PageTransition><Suspense fallback={<Loading />}><Contacts /></Suspense></PageTransition>} />
        <Route path="/settings" element={<PageTransition><Suspense fallback={<Loading />}><Settings /></Suspense></PageTransition>} />
        <Route path="/skills" element={<PageTransition><Suspense fallback={<Loading />}><Skills /></Suspense></PageTransition>} />
        <Route path="/tasks" element={<PageTransition><Suspense fallback={<Loading />}><Tasks /></Suspense></PageTransition>} />
        <Route path="/channel" element={<PageTransition><Suspense fallback={<Loading />}><Channel /></Suspense></PageTransition>} />
        <Route path="/providers" element={<PageTransition><Suspense fallback={<Loading />}><Providers /></Suspense></PageTransition>} />
        <Route path="/mcp" element={<PageTransition><Suspense fallback={<Loading />}><Mcp /></Suspense></PageTransition>} />
        <Route path="*" element={<Suspense fallback={<Loading />}><NotFound /></Suspense>} />
      </Routes>
    </AnimatePresence>
  );
}

function PageTransition({ children }: { children: React.ReactNode }) {
  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.2 }}
      className="overflow-y-auto"
      style={{ touchAction: 'pan-y' }}
    >
      {children}
    </motion.div>
  );
}

export default AppRoutes;
