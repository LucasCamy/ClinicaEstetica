import { BarChart3, CalendarDays, ClipboardList, DollarSign, FileSignature, FileText, LayoutDashboard, LogOut, Palette, PanelLeftClose, PanelLeftOpen, Settings, Sparkles, UserCog, Users, X } from 'lucide-react';
import React, { useState } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import { Button } from '../ui/Components';
import { AppearanceDialog } from './AppearanceDialog';

export const AdminSidebar: React.FC<{
  activeTab: string;
  setActiveTab: (tab: string) => void;
  onBackToLanding: () => void;
  isMobileOpen: boolean;
  setIsMobileOpen: (open: boolean) => void;
}> = ({ activeTab, setActiveTab, onBackToLanding, isMobileOpen, setIsMobileOpen }) => {
  const { user, logout, can } = useAuth();
  const [isCollapsed, setIsCollapsed] = useState(false);
  const [isAppearanceOpen, setIsAppearanceOpen] = useState(false);
  const menuItems = [
    { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard, permission: 'reports.read' },
    { id: 'appointments', label: 'Agenda & Atendimentos', icon: CalendarDays, permission: 'appointments.read' },
    { id: 'clients', label: 'Clientes', icon: Users, permission: 'clients.read' },
    { id: 'forms', label: 'Formulários', icon: ClipboardList, permission: 'forms.read' },
    { id: 'terms', label: 'Termos digitais', icon: FileSignature, permission: 'forms.read' },
    { id: 'procedures', label: 'Procedimentos', icon: FileText, permission: 'procedures.read' },
    { id: 'finance', label: 'Financeiro & Caixa', icon: DollarSign, permission: 'finance.read' },
    { id: 'reports', label: 'Relatórios', icon: BarChart3, permission: 'reports.read' },
    { id: 'cms', label: 'Gestão Landing Page', icon: Settings, permission: 'cms.manage' },
    { id: 'settings', label: 'Configurações', icon: Settings, permission: 'settings.read' },
    { id: 'users', label: 'Usuários & Acessos', icon: UserCog, permission: 'users.manage' },
  ].filter(item => can(item.permission));
  const selectTab = (tab: string) => { setActiveTab(tab); setIsMobileOpen(false); };

  const navigation = <div className="flex h-full flex-col justify-between p-4">
    <div className="space-y-6">
      <div className="flex min-h-[50px] items-center justify-between border-b border-slate-800 pb-4">
        <div className={`flex items-center gap-3 ${isCollapsed ? 'w-full justify-center' : ''}`}>
          <div className="theme-on-accent flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-gradient-to-tr from-rose-700 to-rose-400 shadow-lg"><Sparkles className="h-5 w-5 text-white" /></div>
          {!isCollapsed && <div className="overflow-hidden"><h2 className="whitespace-nowrap font-display text-sm font-bold leading-tight text-white">Painel Admin</h2><span className="block truncate text-[10px] font-semibold uppercase text-rose-400">Olá, {user || 'Admin'}</span></div>}
        </div>
        {!isCollapsed && <button type="button" onClick={() => setIsCollapsed(true)} className="hidden shrink-0 rounded p-1.5 text-slate-400 transition-colors hover:bg-slate-800 hover:text-white xl:flex" title="Recolher menu"><PanelLeftClose className="h-4 w-4" /></button>}
        <button type="button" onClick={() => setIsMobileOpen(false)} className="p-1 text-slate-400 hover:text-white xl:hidden" aria-label="Fechar menu"><X className="h-6 w-6" /></button>
      </div>
      {isCollapsed && <div className="hidden justify-center border-b border-slate-800/60 pb-3 xl:flex"><button type="button" onClick={() => setIsCollapsed(false)} className="rounded p-1.5 text-rose-400 transition-colors hover:bg-slate-800 hover:text-rose-300" title="Expandir menu"><PanelLeftOpen className="h-5 w-5" /></button></div>}
      <nav className="space-y-1.5">
        {menuItems.map(item => { const Icon = item.icon; const isActive = activeTab === item.id; return <button key={item.id} type="button" onClick={() => selectTab(item.id)} aria-current={isActive ? 'page' : undefined} title={isCollapsed ? item.label : undefined} className={`flex w-full items-center gap-3 rounded-lg border py-2.5 text-left text-xs font-medium transition-[background-color,color] duration-150 focus:outline-none focus-visible:ring-2 focus-visible:ring-rose-500/60 sm:text-sm ${isCollapsed ? 'justify-center px-0' : 'justify-start px-3.5'} ${isActive ? 'border-rose-500/30 bg-rose-600/20 font-semibold text-rose-400 shadow-sm' : 'border-transparent text-slate-400 hover:border-slate-800 hover:bg-slate-900/60 hover:text-white'}`}><Icon className={`h-4 w-4 shrink-0 transition-colors ${isActive ? 'text-rose-400' : 'text-slate-400'}`} />{!isCollapsed && <span className="truncate whitespace-nowrap">{item.label}</span>}</button>; })}
      </nav>
    </div>
    <div className="space-y-2 border-t border-slate-800 pt-4">
      <Button variant="ghost" size="sm" className={`w-full ${isCollapsed ? 'justify-center px-0' : 'justify-start px-3'}`} onClick={() => setIsAppearanceOpen(true)} title={isCollapsed ? 'Personalizar aparência' : undefined}><Palette className="h-4 w-4 shrink-0" />{!isCollapsed && <span className="truncate">Aparência</span>}</Button>
      <Button variant="ghost" size="sm" className={`w-full text-red-400 hover:text-red-300 ${isCollapsed ? 'justify-center px-0' : 'justify-start px-3'}`} onClick={() => void logout()} title={isCollapsed ? 'Sair do sistema' : undefined}><LogOut className="h-4 w-4 shrink-0" />{!isCollapsed && <span className="truncate">Sair</span>}</Button>
      <Button variant="outline" size="sm" className={`w-full text-xs ${isCollapsed ? 'justify-center px-0' : 'justify-start px-3'}`} onClick={onBackToLanding} title={isCollapsed ? 'Ver landing page' : undefined}>{isCollapsed ? '←' : '← Ver Site'}</Button>
    </div>
  </div>;

  return <><aside className={`hidden h-full min-h-0 shrink-0 flex-col overflow-y-auto border-r border-slate-800 glass-panel transition-all duration-300 xl:flex ${isCollapsed ? 'w-20' : 'w-64'}`}>{navigation}</aside>{isMobileOpen && <div className="fixed inset-0 z-50 flex xl:hidden"><div className="fixed inset-0 bg-slate-950/80 backdrop-blur-sm" onClick={() => setIsMobileOpen(false)} /><div className="relative z-10 h-full w-72 border-r border-slate-800 glass-panel [padding-bottom:env(safe-area-inset-bottom,0px)] [padding-left:env(safe-area-inset-left,0px)] [padding-top:env(safe-area-inset-top,0px)] animate-slide-in">{navigation}</div></div>}<AppearanceDialog isOpen={isAppearanceOpen} onClose={() => setIsAppearanceOpen(false)} /></>;
};
