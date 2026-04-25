import { Routes, Route, useLocation, Navigate } from 'react-router-dom';
import { AnimatePresence, motion } from 'framer-motion';
import { setTitle } from '../App';
import { lazy, Suspense } from 'react';
import { Spinner } from '@heroui/react';
import { isAuthenticated } from '../services/api';

const Dashboard = lazy(() => import('../pages/DashboardPage'));
const Chat = lazy(() => import('../pages/ChatPage'));
const Contacts = lazy(() => import('../pages/ContactsPage'));
const Settings = lazy(() => import('../pages/SettingsPage'));
const Skills = lazy(() => import('../pages/SkillsPage'));
const Tasks = lazy(() => import('../pages/TasksPage'));
const Channel = lazy(() => import('../pages/ChannelPage'));
const Providers = lazy(() => import('../pages/ProvidersPage'));
const Mcp = lazy(() => import('../pages/McpPage'));
const Security = lazy(() => import('../pages/SecurityPage'));
const Login = lazy(() => import('../pages/LoginPage'));
const NotFound = lazy(() => import('../pages/NotFoundPage'));

function Loading() {
  return (
    <div className="flex items-center justify-center h-[60vh]">
      <Spinner size="lg" />
    </div>
  );
}

function AuthGuard({ children }: { children: React.ReactNode }) {
  if (!isAuthenticated()) {
    return <Navigate to="/login" replace />;
  }
  return <>{children}</>;
}

function AppRoutes() {
  const location = useLocation();

  setTitle("");

  return (
    <AnimatePresence mode="wait">
      <Routes location={location} key={location.pathname}>
        <Route path="/login" element={<Suspense fallback={<Loading />}><Login /></Suspense>} />
        <Route path="/" element={<AuthGuard><PageTransition><Suspense fallback={<Loading />}><Dashboard /></Suspense></PageTransition></AuthGuard>} />
        <Route path="/chat" element={<AuthGuard><PageTransition><Suspense fallback={<Loading />}><Chat /></Suspense></PageTransition></AuthGuard>} />
        <Route path="/contacts" element={<AuthGuard><PageTransition><Suspense fallback={<Loading />}><Contacts /></Suspense></PageTransition></AuthGuard>} />
        <Route path="/settings" element={<AuthGuard><PageTransition><Suspense fallback={<Loading />}><Settings /></Suspense></PageTransition></AuthGuard>} />
        <Route path="/skills" element={<AuthGuard><PageTransition><Suspense fallback={<Loading />}><Skills /></Suspense></PageTransition></AuthGuard>} />
        <Route path="/tasks" element={<AuthGuard><PageTransition><Suspense fallback={<Loading />}><Tasks /></Suspense></PageTransition></AuthGuard>} />
        <Route path="/channel" element={<AuthGuard><PageTransition><Suspense fallback={<Loading />}><Channel /></Suspense></PageTransition></AuthGuard>} />
        <Route path="/providers" element={<AuthGuard><PageTransition><Suspense fallback={<Loading />}><Providers /></Suspense></PageTransition></AuthGuard>} />
        <Route path="/mcp" element={<AuthGuard><PageTransition><Suspense fallback={<Loading />}><Mcp /></Suspense></PageTransition></AuthGuard>} />
        <Route path="/security" element={<AuthGuard><PageTransition><Suspense fallback={<Loading />}><Security /></Suspense></PageTransition></AuthGuard>} />
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
