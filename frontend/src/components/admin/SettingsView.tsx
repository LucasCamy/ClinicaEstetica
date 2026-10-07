import React, { useEffect, useMemo, useState } from 'react';
import { CalendarClock, HardDrive, Info, Loader2, Save, Settings2 } from 'lucide-react';
import { useLocation } from 'react-router-dom';
import { ClinicOperationalSettings, OperatingHoursDay, StorageUsage } from '../../types';
import { api } from '../../services/api';
import { useToast } from '../../contexts/ToastContext';
import { Badge, Button, Card } from '../ui/Components';
import { AdminPageHeader } from './AdminPageHeader';
import { GoogleCalendarSettingsView } from './GoogleCalendarSettingsView';

type SettingsSection = 'operation' | 'integrations' | 'storage';

const weekDays = [
  { dayOfWeek: 0, label: 'Domingo' },
  { dayOfWeek: 1, label: 'Segunda-feira' },
  { dayOfWeek: 2, label: 'Terça-feira' },
  { dayOfWeek: 3, label: 'Quarta-feira' },
  { dayOfWeek: 4, label: 'Quinta-feira' },
  { dayOfWeek: 5, label: 'Sexta-feira' },
  { dayOfWeek: 6, label: 'Sábado' },
];

const formatBytes = (bytes: number) => {
  if (bytes < 1024 * 1024) return `${Math.max(0, Math.round(bytes / 1024))} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`;
};

export const SettingsView: React.FC = () => {
  const { showToast } = useToast();
  const location = useLocation();
  const [section, setSection] = useState<SettingsSection>(() => new URLSearchParams(location.search).has('google') ? 'integrations' : 'operation');
  const [settings, setSettings] = useState<ClinicOperationalSettings | null>(null);
  const [storage, setStorage] = useState<StorageUsage | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);

  const load = async () => {
    try {
      setIsLoading(true);
      const [operational, currentStorage] = await Promise.all([
        api.getClinicOperationalSettings(),
        api.getStorageUsage(),
      ]);
      setSettings({
        ...operational,
        operatingHours: operational.operatingHours.map(day => ({
          ...day,
          startTime: day.startTime.slice(0, 5),
          endTime: day.endTime.slice(0, 5),
        })),
      });
      setStorage(currentStorage);
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível carregar as configurações.', 'error');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => { void load(); }, []);
  useEffect(() => {
    if (new URLSearchParams(location.search).has('google')) setSection('integrations');
  }, [location.search]);

  const hoursByDay = useMemo(() => new Map(settings?.operatingHours.map(day => [day.dayOfWeek, day])), [settings]);
  const updateDay = (dayOfWeek: number, patch: Partial<OperatingHoursDay>) => {
    setSettings(current => current ? {
      ...current,
      operatingHours: current.operatingHours.map(day => day.dayOfWeek === dayOfWeek ? { ...day, ...patch } : day),
    } : current);
  };

  const saveHours = async () => {
    if (!settings) return;
    const invalid = settings.operatingHours.some(day => day.isOpen && day.startTime >= day.endTime);
    if (invalid) {
      showToast('Em cada dia aberto, o início precisa ser anterior ao término.', 'warning');
      return;
    }

    try {
      setIsSaving(true);
      const updated = await api.saveClinicOperatingHours(settings.operatingHours);
      setSettings({
        ...updated,
        operatingHours: updated.operatingHours.map(day => ({
          ...day,
          startTime: day.startTime.slice(0, 5),
          endTime: day.endTime.slice(0, 5),
        })),
      });
      showToast('Horário de expediente salvo. A grade da agenda foi atualizada para os próximos acessos.', 'success');
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível salvar o horário de expediente.', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  const storagePercent = storage?.limitBytes ? Math.min(100, Math.round((storage.usedBytes / storage.limitBytes) * 100)) : 0;

  return (
    <div className="space-y-6">
      <AdminPageHeader
        eyebrow="Configurações"
        title="Operação e integrações"
        description="Defina como a agenda interna funciona, acompanhe os arquivos da clínica e gerencie conexões externas."
      />

      <div className="grid gap-2 rounded-xl border border-slate-800 bg-slate-950/35 p-2 sm:grid-cols-3">
        {[
          { id: 'operation' as const, label: 'Expediente', icon: CalendarClock },
          { id: 'integrations' as const, label: 'Integrações', icon: Settings2 },
          { id: 'storage' as const, label: 'Armazenamento', icon: HardDrive },
        ].map(item => {
          const Icon = item.icon;
          const active = section === item.id;
          return <button key={item.id} type="button" onClick={() => setSection(item.id)} className={`flex items-center justify-center gap-2 rounded-lg px-3 py-2.5 text-sm font-semibold transition-colors ${active ? 'bg-rose-600 text-white shadow-md shadow-rose-600/20' : 'text-slate-300 hover:bg-slate-800/70 hover:text-white'}`}><Icon className="h-4 w-4" />{item.label}</button>;
        })}
      </div>

      {isLoading && section !== 'integrations' ? <Card className="flex min-h-48 items-center justify-center gap-2 text-sm text-slate-400"><Loader2 className="h-4 w-4 animate-spin" /> Carregando configurações…</Card> : null}

      {!isLoading && section === 'operation' && settings && <>
        <Card className="space-y-5">
          <div className="flex flex-col justify-between gap-4 border-b border-slate-800 pb-5 sm:flex-row sm:items-start">
            <div className="flex gap-3">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border border-rose-500/30 bg-rose-500/10 text-rose-400"><CalendarClock className="h-5 w-5" /></div>
              <div><div className="flex flex-wrap items-center gap-2"><h2 className="font-display text-lg font-bold text-white">Horário de expediente</h2><Badge variant="info">{settings.timeZoneId}</Badge></div><p className="mt-1 max-w-2xl text-sm text-slate-400">Limita novos agendamentos e os horários mostrados na timeline. Não altera o horário divulgado na landing page.</p></div>
            </div>
            <Button onClick={() => void saveHours()} disabled={isSaving}><Save className="h-4 w-4" />{isSaving ? 'Salvando…' : 'Salvar horário'}</Button>
          </div>

          <section className="overflow-hidden rounded-xl border border-slate-800 bg-slate-950/25">
            <div className="flex flex-col justify-between gap-2 border-b border-slate-800 bg-slate-900/45 px-4 py-3 sm:flex-row sm:items-center">
              <div><h3 className="text-sm font-bold text-slate-100">Funcionamento semanal</h3><p className="mt-0.5 text-xs text-slate-500">Ative cada dia e defina o intervalo disponível para novos atendimentos.</p></div>
              <span className="text-xs font-medium text-slate-400">Clique no interruptor para abrir ou fechar o dia</span>
            </div>
            <div className="grid gap-px bg-slate-800 xl:grid-cols-2">
            {weekDays.map(({ dayOfWeek, label }) => {
              const day = hoursByDay.get(dayOfWeek);
              if (!day) return null;
              return <div key={dayOfWeek} className={`flex min-h-[76px] flex-wrap items-center gap-x-3 gap-y-2 bg-slate-950/55 px-4 py-3 transition-colors ${day.isOpen ? 'hover:bg-slate-900/70' : 'bg-slate-950/30'}`}>
                <div className="min-w-[130px] flex-1"><p className={`text-sm font-semibold ${day.isOpen ? 'text-slate-100' : 'text-slate-500'}`}>{label}</p><p className={`mt-0.5 text-[11px] ${day.isOpen ? 'text-emerald-400' : 'text-slate-600'}`}>{day.isOpen ? 'Em expediente' : 'Fechado'}</p></div>
                <button type="button" role="switch" aria-checked={day.isOpen} aria-label={`${day.isOpen ? 'Fechar' : 'Abrir'} ${label}`} onClick={() => updateDay(dayOfWeek, { isOpen: !day.isOpen })} className={`relative h-7 w-12 shrink-0 rounded-full border transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-rose-500/60 ${day.isOpen ? 'border-rose-400/50 bg-rose-600' : 'border-slate-700 bg-slate-800'}`}><span className={`absolute left-1 top-1 h-5 w-5 rounded-full bg-white shadow-sm transition-transform ${day.isOpen ? 'translate-x-5' : 'translate-x-0'}`} /></button>
                <div className={`flex items-center gap-1.5 rounded-lg border px-2 py-1.5 ${day.isOpen ? 'border-slate-700 bg-slate-900/80' : 'border-slate-800 bg-slate-950/30 opacity-45'}`}>
                  <input aria-label={`Início de ${label}`} type="time" value={day.startTime} disabled={!day.isOpen} onChange={event => updateDay(dayOfWeek, { startTime: event.target.value })} className="native-temporal-inline w-[78px] bg-transparent text-center text-xs font-semibold text-slate-100 outline-none disabled:cursor-not-allowed" />
                  <span className="text-[11px] text-slate-500">até</span>
                  <input aria-label={`Término de ${label}`} type="time" value={day.endTime} disabled={!day.isOpen} onChange={event => updateDay(dayOfWeek, { endTime: event.target.value })} className="native-temporal-inline w-[78px] bg-transparent text-center text-xs font-semibold text-slate-100 outline-none disabled:cursor-not-allowed" />
                </div>
              </div>;
            })}
            </div>
          </section>
        </Card>
        <div className="flex gap-3 rounded-xl border border-amber-500/20 bg-amber-500/5 p-4 text-sm text-amber-100"><Info className="mt-0.5 h-4 w-4 shrink-0 text-amber-400" /><p>Atendimentos já cadastrados fora de um novo expediente não são apagados nem bloqueados. Na timeline, eles aparecem em uma seção própria <strong>Fora do expediente</strong>, para que possam ser revisados.</p></div>
      </>}

      {section === 'integrations' && <div className="space-y-4"><div className="rounded-xl border border-slate-800 bg-slate-950/25 p-4"><h2 className="font-display text-lg font-bold text-white">Integração com Google Calendar</h2><p className="mt-1 text-sm text-slate-400">Visualize a agenda compartilhada e envie automaticamente os agendamentos criados no painel.</p></div><GoogleCalendarSettingsView /></div>}

      {!isLoading && section === 'storage' && storage && <Card className="space-y-5">
        <div className="flex gap-3 border-b border-slate-800 pb-5"><div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border border-violet-500/30 bg-violet-500/10 text-violet-400"><HardDrive className="h-5 w-5" /></div><div><h2 className="font-display text-lg font-bold text-white">Armazenamento da clínica</h2><p className="mt-1 text-sm text-slate-400">Uso controlado de fotos, documentos clínicos e termos finalizados.</p></div></div>
        <div className="grid gap-4 sm:grid-cols-3"><div className="rounded-xl border border-slate-800 bg-slate-950/35 p-4"><span className="text-xs text-slate-500">Em uso</span><p className="mt-1 font-display text-2xl font-bold text-white">{formatBytes(storage.usedBytes)}</p></div><div className="rounded-xl border border-slate-800 bg-slate-950/35 p-4"><span className="text-xs text-slate-500">Limite configurado</span><p className="mt-1 font-display text-2xl font-bold text-white">{formatBytes(storage.limitBytes)}</p></div><div className="rounded-xl border border-slate-800 bg-slate-950/35 p-4"><span className="text-xs text-slate-500">Ocupação</span><p className="mt-1 font-display text-2xl font-bold text-white">{storagePercent}%</p></div></div>
        <div><div className="mb-2 flex justify-between text-xs text-slate-400"><span>{formatBytes(storage.usedBytes)} utilizados</span><span>{formatBytes(Math.max(0, storage.limitBytes - storage.usedBytes))} disponíveis</span></div><div className="h-3 overflow-hidden rounded-full bg-slate-800"><div className={`h-full rounded-full ${storagePercent >= 90 ? 'bg-rose-500' : storagePercent >= 75 ? 'bg-amber-400' : 'bg-violet-500'}`} style={{ width: `${storagePercent}%` }} /></div></div>
        <p className="text-xs leading-relaxed text-slate-500">O limite é aplicado pelo servidor no envio de novos arquivos. Os valores podem ser ajustados no ambiente de hospedagem sem alterar a landing page ou os dados clínicos.</p>
      </Card>}
    </div>
  );
};
