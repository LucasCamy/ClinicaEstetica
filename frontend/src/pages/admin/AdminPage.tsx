import React, { useState, useEffect, Suspense, lazy } from 'react';
import { Menu, Sparkles } from 'lucide-react';
import { matchPath, useLocation, useNavigate } from 'react-router-dom';
import { Client, ProcedureType, CmsContent } from '../../types';
import { api } from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';
import { useToast } from '../../contexts/ToastContext';
import { SkeletonCard, SkeletonTable } from '../../components/ui/Components';
import { SecurityOnboardingPage } from './SecurityOnboardingPage';
import { AdminSidebar } from '../../components/admin/AdminSidebar';
import {
  DashboardView, AppointmentsView, ClientsView, ClientDetailView, ProceduresView
} from '../../components/admin/AdminComponents';

const UserAccessView = lazy(() => import('../../components/admin/UserAccessView').then(m => ({ default: m.UserAccessView })));
const FormManagementView = lazy(() => import('../../components/admin/FormManagementView').then(m => ({ default: m.FormManagementView })));
const TermManagementView = lazy(() => import('../../components/admin/TermManagementView').then(m => ({ default: m.TermManagementView })));
const FinanceView = lazy(() => import('../../components/admin/FinanceView').then(m => ({ default: m.FinanceView })));
const LandingManagementView = lazy(() => import('../../components/admin/LandingManagementView').then(m => ({ default: m.LandingManagementView })));
const ReportsView = lazy(() => import('../../components/admin/ReportsView').then(m => ({ default: m.ReportsView })));
const SettingsView = lazy(() => import('../../components/admin/SettingsView').then(m => ({ default: m.SettingsView })));

const adminPaths: Record<string, string> = {
  dashboard: '/admin/dashboard',
  appointments: '/admin/agenda',
  clients: '/admin/clientes',
  forms: '/admin/formularios',
  terms: '/admin/termos',
  procedures: '/admin/procedimentos',
  cms: '/admin/landing-page',
  finance: '/admin/financeiro',
  reports: '/admin/relatorios',
  users: '/admin/usuarios',
  settings: '/admin/configuracoes',
};

const tabFromPath: Record<string, string> = {
  dashboard: 'dashboard',
  agenda: 'appointments',
  clientes: 'clients',
  formularios: 'forms',
  termos: 'terms',
  procedimentos: 'procedures',
  'landing-page': 'cms',
  financeiro: 'finance',
  relatorios: 'reports',
  usuarios: 'users',
  configuracoes: 'settings',
  integracoes: 'settings',
};

const tabPermissions: Record<string, string> = {
  dashboard: 'reports.read',
  appointments: 'appointments.read',
  clients: 'clients.read',
  forms: 'forms.read',
  terms: 'forms.read',
  procedures: 'procedures.read',
  cms: 'cms.manage',
  finance: 'finance.read',
  reports: 'reports.read',
  users: 'users.manage',
  settings: 'settings.read',
};

export const AdminPage: React.FC = () => {
  const { isAuthenticated, currentUser, can } = useAuth();
  const { showToast } = useToast();
  const navigate = useNavigate();
  const location = useLocation();
  const selectedClientId = matchPath('/admin/clientes/:clientId', location.pathname)?.params.clientId ?? null;
  const routeSegment = location.pathname.split('/')[2] ?? 'dashboard';
  const activeTab = selectedClientId ? 'clients' : tabFromPath[routeSegment] ?? 'dashboard';
  const [isMobileOpen, setIsMobileOpen] = useState(false);
  const [clients, setClients] = useState<Client[]>([]);
  const [procedures, setProcedures] = useState<ProcedureType[]>([]);
  const [cms, setCms] = useState<CmsContent | null>(null);

  const loadAll = async () => {
    if (isAuthenticated && !currentUser?.mustChangePassword && !currentUser?.requiresMfaSetup) {
      try {
        const [loadedClients, loadedProcedures, loadedCms] = await Promise.all([
          can('clients.read') ? api.getClients() : Promise.resolve([]),
          can('procedures.read') ? api.getProcedures() : Promise.resolve([]),
          can('cms.manage') ? api.getCms() : Promise.resolve(null),
        ]);
        setClients(loadedClients);
        setProcedures(loadedProcedures);
        setCms(loadedCms);
      } catch (error) {
        showToast(error instanceof Error ? error.message : 'Falha ao carregar o painel.', 'error');
      }
    }
  };

  useEffect(() => { void loadAll(); }, [isAuthenticated, currentUser?.mustChangePassword, currentUser?.requiresMfaSetup]);

  useEffect(() => {
    if (!currentUser) return;
    if (!tabFromPath[routeSegment] && !selectedClientId) {
      navigate('/admin/dashboard', { replace: true });
      return;
    }
    if (selectedClientId && !can('clinical.read')) {
      navigate('/admin/clientes', { replace: true });
      return;
    }
    if (!can(tabPermissions[activeTab] ?? '')) {
      const fallback = Object.keys(adminPaths).find(tab => can(tabPermissions[tab]));
      if (fallback) navigate(adminPaths[fallback], { replace: true });
    }
  }, [currentUser, routeSegment, selectedClientId, activeTab, navigate]);

  if (currentUser?.mustChangePassword || currentUser?.requiresMfaSetup) {
    return <SecurityOnboardingPage />;
  }

  return (
    <div className="flex h-[100dvh] flex-col overflow-hidden bg-slate-950 pb-[env(safe-area-inset-bottom,0px)] pt-[env(safe-area-inset-top,0px)] font-sans text-slate-100 xl:flex-row">
      {/* TABLET & MOBILE TOPBAR HEADER (< XL) */}
      <header className="flex shrink-0 items-center justify-between border-b border-slate-800 p-4 glass-panel xl:hidden">
        <div className="flex items-center gap-3">
          <div className="theme-on-accent w-8 h-8 rounded-lg bg-gradient-to-tr from-rose-700 to-rose-400 flex items-center justify-center shadow-lg">
            <Sparkles className="w-4 h-4 text-white" />
          </div>
          <span className="font-display font-bold text-white text-base">Painel<span className="text-rose-500">Estética</span></span>
        </div>
        <button onClick={() => setIsMobileOpen(true)} className="p-2 text-slate-300 hover:text-white" title="Menu">
          <Menu className="w-6 h-6" />
        </button>
      </header>

      {/* SIDEBAR & TABLET/MOBILE DRAWER */}
      <AdminSidebar
        activeTab={activeTab}
        setActiveTab={tab => navigate(adminPaths[tab] ?? '/admin/dashboard')}
        onBackToLanding={() => navigate('/')}
        isMobileOpen={isMobileOpen}
        setIsMobileOpen={setIsMobileOpen}
      />

      <main className="min-h-0 min-w-0 flex-1 overflow-y-auto overscroll-contain p-4 sm:p-5 md:p-6 xl:p-8">
        <div key={selectedClientId || location.pathname} className="mx-auto w-full max-w-7xl animate-page-fade">
          <Suspense fallback={
            <div className="space-y-6">
              <div className="flex justify-between items-center">
                <SkeletonCard lines={1} className="w-64" />
                <SkeletonCard lines={1} className="w-32" />
              </div>
              <SkeletonTable rows={6} columns={4} />
            </div>
          }>
            {selectedClientId ? (
              <ClientDetailView clientId={selectedClientId} onBack={() => navigate('/admin/clientes')} />
            ) : (
              <>
                {activeTab === 'dashboard' && can('reports.read') && <DashboardView />}
                {activeTab === 'appointments' && can('appointments.read') && <AppointmentsView procedures={procedures} clients={clients} />}
                {activeTab === 'clients' && can('clients.read') && (
                  <ClientsView
                    clients={clients}
                    onSelectClient={can('clinical.read')
                      ? id => navigate(`/admin/clientes/${id}`)
                      : () => showToast('Seu perfil pode gerenciar o cadastro básico, mas não acessa prontuários.', 'error')}
                    onRefresh={loadAll}
                  />
                )}

                {activeTab === 'forms' && can('forms.read') && <FormManagementView />}

                {activeTab === 'terms' && can('forms.read') && <TermManagementView />}

                {activeTab === 'procedures' && can('procedures.read') && (
                  <ProceduresView procedures={procedures} onRefresh={loadAll} />
                )}

                {activeTab === 'cms' && can('cms.manage') && cms && <LandingManagementView cms={cms} onCmsChange={setCms} />}

                {activeTab === 'finance' && can('finance.read') && <FinanceView />}

                {activeTab === 'reports' && can('reports.read') && <ReportsView />}

                {activeTab === 'users' && can('users.manage') && <UserAccessView />}

                {activeTab === 'settings' && can('settings.read') && <SettingsView />}
              </>
            )}
          </Suspense>
        </div>
      </main>
    </div>
  );
};
