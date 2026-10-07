import React, { Suspense, lazy } from 'react';
import { BrowserRouter, Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import { ThemeProvider } from './contexts/ThemeContext';
import { ToastProvider } from './contexts/ToastContext';
import { SkeletonLoader } from './components/ui/Components';

const LandingPage = lazy(() => import('./pages/landing/LandingPage').then(m => ({ default: m.LandingPage })));
const PrivacyPolicyPage = lazy(() => import('./pages/landing/PrivacyPolicyPage').then(m => ({ default: m.PrivacyPolicyPage })));
const TermsOfServicePage = lazy(() => import('./pages/landing/TermsOfServicePage').then(m => ({ default: m.TermsOfServicePage })));
const AdminPage = lazy(() => import('./pages/admin/AdminPage').then(m => ({ default: m.AdminPage })));
const LoginPage = lazy(() => import('./pages/admin/LoginPage').then(m => ({ default: m.LoginPage })));

const ScrollToTop: React.FC = () => {
  const location = useLocation();

  React.useEffect(() => {
    if (!location.hash) {
      window.scrollTo(0, 0);
    }
  }, [location.pathname, location.hash]);

  return null;
};

const AdminSessionLoading: React.FC = () => (
  <div className="flex min-h-screen flex-col items-center justify-center bg-slate-950 px-4 text-center">
    <div className="w-full max-w-sm space-y-4">
      <div className="mx-auto h-12 w-12 rounded-xl bg-gradient-to-tr from-rose-700 to-rose-400 p-0.5 shadow-lg shadow-rose-600/30 flex items-center justify-center animate-pulse">
        <div className="h-4 w-4 rounded-full bg-white animate-ping" />
      </div>
      <SkeletonLoader className="h-5 w-48 mx-auto" />
      <SkeletonLoader className="h-3 w-32 mx-auto" />
    </div>
  </div>
);

const isSafeAdminReturnPath = (value: string | null): value is string =>
  Boolean(value && value.startsWith('/admin/') && !value.startsWith('/admin/login'));

const RequireAdminAuthentication: React.FC<React.PropsWithChildren> = ({ children }) => {
  const { isAuthenticated, isLoading } = useAuth();
  const location = useLocation();

  if (isLoading) return <AdminSessionLoading />;
  if (isAuthenticated) return <>{children}</>;

  const returnTo = `${location.pathname}${location.search}`;
  return <Navigate to={`/admin/login?returnTo=${encodeURIComponent(returnTo)}`} replace />;
};

const AdminLoginRoute: React.FC = () => {
  const { isAuthenticated, isLoading } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const requestedReturnPath = new URLSearchParams(location.search).get('returnTo');
  const returnTo = isSafeAdminReturnPath(requestedReturnPath)
    ? requestedReturnPath
    : '/admin/dashboard';

  if (isLoading) return <AdminSessionLoading />;
  if (isAuthenticated) return <Navigate to={returnTo} replace />;

  return (
    <LoginPage
      onBackToLanding={() => navigate('/')}
      onAuthenticated={() => navigate(returnTo, { replace: true })}
    />
  );
};

export const AppContent: React.FC = () => {
  const navigate = useNavigate();

  return (
    <>
      <ScrollToTop />
      <Suspense fallback={<AdminSessionLoading />}>
        <Routes>
          <Route path="/" element={<LandingPage onOpenAdmin={() => navigate('/admin/dashboard')} />} />
          <Route path="/privacidade" element={<PrivacyPolicyPage onOpenAdmin={() => navigate('/admin/dashboard')} />} />
          <Route path="/privacy" element={<Navigate to="/privacidade" replace />} />
          <Route path="/termos" element={<TermsOfServicePage onOpenAdmin={() => navigate('/admin/dashboard')} />} />
          <Route path="/terms" element={<Navigate to="/termos" replace />} />
          <Route path="/admin/login" element={<AdminLoginRoute />} />
          <Route path="/admin" element={<Navigate to="/admin/dashboard" replace />} />
          <Route path="/admin/*" element={<RequireAdminAuthentication><AdminPage /></RequireAdminAuthentication>} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Suspense>
    </>
  );
};

export const App: React.FC = () => {
  return (
    <ThemeProvider>
      <AuthProvider>
        <ToastProvider>
          <BrowserRouter><AppContent /></BrowserRouter>
        </ToastProvider>
      </AuthProvider>
    </ThemeProvider>
  );
};

export default App;
