import {
  AlertCircle,
  Calendar as CalendarIcon,
  Camera,
  CheckCircle,
  ChevronLeft, ChevronRight,
  ClipboardList,
  Clock,
  DollarSign,
  Edit,
  Eye,
  FileSignature,
  FileText,
  FileUp,
  LayoutDashboard,
  Phone,
  Plus,
  RefreshCw,
  Sparkles, Stethoscope,
  Trash2,
  TrendingDown,
  Upload,
  UserCog,
  Users,
  XCircle
} from 'lucide-react';
import React, { useEffect, useState } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import { useToast } from '../../contexts/ToastContext';
import { api } from '../../services/api';
import {
  Appointment,
  AppointmentStatus,
  Client,
  ClientSummary,
  ClinicalStorageUsage,
  DashboardStats,
  GoogleCalendarEvent,
  OperatingHoursDay,
  PaymentMethod,
  PhotoType,
  ProcedureType
} from '../../types';
import { clinicDateKey, clinicDateTimeInputValue, clinicTimeKey, clinicTodayKey, formatClinicDateTime } from '../../utils/clinicDateTime';
import { currencyInputFromNumber, formatCity, formatCpf, formatCurrencyInput, formatPhone, formatRg, parseCurrencyInput } from '../../utils/inputMasks';
import { Badge, Button, Card, Dialog, Input, Select, SkeletonCard, SkeletonForm, SkeletonLoader, SkeletonMetric, SkeletonTable, ProgressBar, useConfirmationDialog } from '../ui/Components';
import { AdminPageHeader } from './AdminPageHeader';
import { ClientAppointmentHistoryPanel } from './ClientAppointmentHistoryPanel';
import { ClientFormsPanel } from './ClientFormsPanel';
import { ClientTermsPanel } from './ClientTermsPanel';
import { ClientSelectCombobox as SharedClientSelectCombobox } from './ClientSelectCombobox';

const appointmentStatusLabels: Record<AppointmentStatus, string> = {
  Scheduled: 'Agendado',
  Confirmed: 'Confirmado',
  InProgress: 'Atendimento',
  Completed: 'Concluído',
  Cancelled: 'Cancelado',
  NoShow: 'Faltou',
};

const appointmentStatusBadge = (status: AppointmentStatus): 'success' | 'warning' | 'danger' | 'info' => {
  if (status === 'Completed' || status === 'Confirmed') return 'success';
  if (status === 'Cancelled' || status === 'NoShow') return 'danger';
  if (status === 'InProgress') return 'info';
  return 'warning';
};

const defaultOperatingHours: OperatingHoursDay[] = Array.from({ length: 7 }, (_, dayOfWeek) => ({
  dayOfWeek,
  isOpen: true,
  startTime: '07:00',
  endTime: '21:00',
}));

const operatingHoursForDate = (dateKey: string, operatingHours: OperatingHoursDay[]) => {
  const dayOfWeek = new Date(`${dateKey}T12:00:00`).getDay();
  const configured = operatingHours.find(item => item.dayOfWeek === dayOfWeek) ?? defaultOperatingHours[dayOfWeek];
  // A API serializa TimeOnly com segundos; a timeline trabalha somente por minuto.
  return { ...configured, startTime: configured.startTime.slice(0, 5), endTime: configured.endTime.slice(0, 5) };
};

const isTimeWithinOperatingHours = (time: string, operatingHours: OperatingHoursDay) =>
  operatingHours.isOpen && time >= operatingHours.startTime && time < operatingHours.endTime;

const isAppointmentWithinOperatingHours = (appointment: Appointment, operatingHours: OperatingHoursDay[]) =>
  isTimeWithinOperatingHours(
    clinicTimeKey(appointment.scheduledDateTime),
    operatingHoursForDate(clinicDateKey(appointment.scheduledDateTime), operatingHours),
  );

const isGoogleEventWithinOperatingHours = (event: GoogleCalendarEvent, operatingHours: OperatingHoursDay[]) => {
  const dateKey = clinicDateKey(event.startUtc);
  const dailyHours = operatingHoursForDate(dateKey, operatingHours);
  if (!dailyHours.isOpen) return false;
  if (event.isAllDay) return true;
  const start = clinicTimeKey(event.startUtc);
  const end = clinicTimeKey(event.endUtc);
  return start < dailyHours.endTime && end > dailyHours.startTime;
};

// DASHBOARD VIEW
export const DashboardView: React.FC = () => {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.getDashboardStats()
      .then(setStats)
      .finally(() => setLoading(false));
  }, []);

  const formatCurrency = (value: number) => value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  const formatDay = (value: string) => new Intl.DateTimeFormat('pt-BR', { weekday: 'short', day: '2-digit' })
    .format(new Date(`${value}T12:00:00`))
    .replace('.', '');
  const formatBirthday = (value: string) => new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: 'short' })
    .format(new Date(`${value}T12:00:00`))
    .replace('.', '');
  const formatDate = (value: string) => new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: 'short', year: 'numeric' })
    .format(new Date(`${value}T12:00:00`))
    .replace('.', '');
  const scheduleMax = Math.max(1, ...(stats?.upcomingSchedule.map(day =>
    day.scheduled + day.confirmed + day.inProgress + day.completed + day.cancelled + day.noShow) ?? [0]));
  const topProcedureMax = Math.max(1, ...(stats?.topProcedures.map(procedure => procedure.appointments) ?? [0]));

  const metrics = stats ? [
    { label: 'Clientes ativos', value: stats.totalClients.toLocaleString('pt-BR'), detail: 'Cadastros disponíveis', icon: Users, tone: 'rose' },
    { label: 'Agenda de hoje', value: stats.todayAppointments.toLocaleString('pt-BR'), detail: 'Sem cancelamentos e faltas', icon: CalendarIcon, tone: 'amber' },
    { label: 'Agenda no mês', value: stats.appointmentsThisMonth.toLocaleString('pt-BR'), detail: `${stats.scheduledAppointmentsThisMonth + stats.confirmedAppointmentsThisMonth} compromisso(s) pendente(s)`, icon: Clock, tone: 'sky' },
    { label: 'Procedimentos ativos', value: stats.activeProcedures.toLocaleString('pt-BR'), detail: 'Disponíveis no catálogo', icon: FileText, tone: 'violet' },
    { label: 'Formulários finalizados', value: stats.finalizedFormsThisMonth.toLocaleString('pt-BR'), detail: `${stats.publishedForms} modelo(s) publicado(s)`, icon: ClipboardList, tone: 'emerald' },
    { label: 'Termos finalizados', value: stats.finalizedTermsThisMonth.toLocaleString('pt-BR'), detail: `${stats.publishedTerms} modelo(s) publicado(s)`, icon: FileSignature, tone: 'sky' },
    { label: 'Recebido no mês', value: formatCurrency(stats.monthlyRevenue), detail: `${stats.pendingLeads} novo(s) lead(s)`, icon: DollarSign, tone: 'green' },
    { label: 'Saídas no mês', value: formatCurrency(stats.monthlyExpenses), detail: 'Despesas realizadas no período', icon: TrendingDown, tone: 'rose' },
    { label: 'Retornos próximos', value: stats.dueReturnsCount.toLocaleString('pt-BR'), detail: 'Vencidos ou previstos em até 14 dias', icon: Sparkles, tone: 'amber' },
  ] : [];

  const statusCards = stats ? [
    { label: 'Marcados', value: stats.scheduledAppointmentsThisMonth, className: 'border-amber-500/30 bg-amber-500/10 text-amber-300', icon: Clock },
    { label: 'Confirmados', value: stats.confirmedAppointmentsThisMonth, className: 'border-sky-500/30 bg-sky-500/10 text-sky-300', icon: CheckCircle },
    { label: 'Atendimento', value: stats.inProgressAppointmentsThisMonth, className: 'border-violet-500/30 bg-violet-500/10 text-violet-300', icon: AlertCircle },
    { label: 'Concluídos', value: stats.completedAppointmentsThisMonth, className: 'border-emerald-500/30 bg-emerald-500/10 text-emerald-300', icon: CheckCircle },
    { label: 'Cancelados', value: stats.cancelledAppointmentsThisMonth, className: 'border-rose-500/30 bg-rose-500/10 text-rose-300', icon: XCircle },
    { label: 'Não compareceu', value: stats.noShowAppointmentsThisMonth, className: 'border-slate-600 bg-slate-800/70 text-slate-300', icon: UserCog },
  ] : [];

  return (
    <div className="space-y-6 sm:space-y-8">
      <AdminPageHeader
        eyebrow="Visão operacional"
        title="Dashboard da clínica"
        description="Acompanhe agenda, cadastros, formulários e receita em um só lugar."
        actions={<div className="inline-flex w-fit items-center gap-2 rounded-lg border border-slate-800 bg-slate-900/60 px-3 py-2 text-xs text-slate-400"><CalendarIcon className="h-4 w-4 text-rose-400" /> Dados atualizados ao abrir</div>}
      />

      {loading ? (
        <>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {Array.from({ length: 9 }, (_, index) => <SkeletonMetric key={index} />)}
          </div>
          <div className="grid gap-6 xl:grid-cols-5">
            <SkeletonCard lines={6} className="xl:col-span-3 h-[360px]" />
            <SkeletonCard lines={5} className="xl:col-span-2 h-[360px]" />
          </div>
        </>
      ) : !stats ? (
        <Card className="py-12 text-center">
          <AlertCircle className="mx-auto h-8 w-8 text-rose-400" />
          <h2 className="mt-3 font-display text-lg font-bold text-white">Não foi possível carregar o dashboard</h2>
          <p className="mt-1 text-sm text-slate-400">Atualize a página para tentar novamente.</p>
        </Card>
      ) : (
        <>
          <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3" aria-label="Indicadores gerais">
            {metrics.map(metric => {
              const Icon = metric.icon;
              const toneStyles = ({
                rose: 'border-rose-500/40 bg-rose-500/10 text-rose-400',
                amber: 'border-amber-500/40 bg-amber-500/10 text-amber-400',
                sky: 'border-sky-500/40 bg-sky-500/10 text-sky-400',
                violet: 'border-violet-500/40 bg-violet-500/10 text-violet-400',
                emerald: 'border-emerald-500/40 bg-emerald-500/10 text-emerald-400',
                green: 'border-green-500/40 bg-green-500/10 text-green-400',
              } as Record<string, string>)[metric.tone] ?? 'border-slate-500/40 bg-slate-500/10 text-slate-400';
              return (
                <Card key={metric.label} className="relative overflow-hidden p-5">
                  <div className={`absolute right-0 top-0 h-20 w-20 -translate-y-7 translate-x-7 rounded-full ${toneStyles.split(' ')[1]}`} />
                  <div className="relative flex items-start justify-between gap-3">
                    <div>
                      <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-400">{metric.label}</p>
                      <p className="mt-2 font-display text-2xl font-bold text-white sm:text-3xl">{metric.value}</p>
                      <p className="mt-1 text-[11px] text-slate-500">{metric.detail}</p>
                    </div>
                    <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border ${toneStyles}`}><Icon className="h-5 w-5" /></span>
                  </div>
                </Card>
              );
            })}
          </section>

          <section aria-labelledby="agenda-status-title">
            <div className="mb-3 flex items-end justify-between gap-4">
              <div>
                <h2 id="agenda-status-title" className="font-display text-lg font-bold text-white">Situação da agenda</h2>
                <p className="mt-0.5 text-xs text-slate-400">Movimentações registradas no mês atual.</p>
              </div>
              <span className="text-xs font-medium text-slate-500">{stats.appointmentsThisMonth} no total</span>
            </div>
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-6">
              {statusCards.map(item => {
                const Icon = item.icon;
                return <div key={item.label} className={`rounded-xl border p-3 ${item.className}`}><div className="flex items-center justify-between gap-2"><span className="text-[10px] font-semibold uppercase tracking-wide">{item.label}</span><Icon className="h-4 w-4 opacity-80" /></div><p className="mt-2 font-display text-2xl font-bold">{item.value}</p></div>;
              })}
            </div>
          </section>

          <section aria-labelledby="retornos-title">
            <Card>
              <div className="flex flex-col gap-3 border-b border-slate-800 pb-4 sm:flex-row sm:items-start sm:justify-between">
                <div><h2 id="retornos-title" className="font-display text-lg font-bold text-white">Retornos recomendados</h2><p className="mt-0.5 text-xs text-slate-400">Clientes cujo último atendimento concluído exige acompanhamento.</p></div>
                <Badge variant={stats.dueReturnsCount ? 'warning' : 'success'}>{stats.dueReturnsCount ? `${stats.dueReturnsCount} atenção` : 'Em dia'}</Badge>
              </div>
              {stats.dueReturns.length ? <div className="mt-2 divide-y divide-slate-800/80">{stats.dueReturns.map(item => <div key={`${item.clientId}-${item.procedureName}`} className="flex flex-col gap-2 py-3 sm:flex-row sm:items-center sm:justify-between"><div className="min-w-0"><p className="truncate text-sm font-semibold text-slate-200">{item.clientName} <span className="font-normal text-slate-500">• {item.procedureName}</span></p><p className="mt-0.5 text-[11px] text-slate-500">Última conclusão: {formatDate(item.lastCompletedDate)} · retorno recomendado: {formatDate(item.returnDueDate)}</p></div><Badge variant={item.daysUntil < 0 ? 'danger' : item.daysUntil === 0 ? 'warning' : 'info'}>{item.daysUntil < 0 ? `Vencido há ${Math.abs(item.daysUntil)}d` : item.daysUntil === 0 ? 'Vence hoje' : `${item.daysUntil}d`}</Badge></div>)}</div> : <div className="flex min-h-28 items-center justify-center text-center"><p className="max-w-md text-xs text-slate-500">Configure o prazo de retorno em um procedimento e marque os atendimentos como concluídos para acompanhar os próximos retornos aqui.</p></div>}
            </Card>
          </section>

          <section className="grid gap-6 xl:grid-cols-5">
            <Card className="xl:col-span-3">
              <div className="flex flex-col gap-2 border-b border-slate-800 pb-4 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <h2 className="font-display text-lg font-bold text-white">Agenda dos próximos 7 dias</h2>
                  <p className="mt-0.5 text-xs text-slate-400">Distribuição por status para antecipar a operação.</p>
                </div>
                <Badge variant="info">{stats.upcomingSchedule.reduce((total, day) => total + day.scheduled + day.confirmed + day.inProgress, 0)} pendente(s)</Badge>
              </div>
              <div className="mt-4 flex flex-wrap gap-x-4 gap-y-2 text-[10px] text-slate-400">
                <span className="flex items-center gap-1.5"><i className="h-2 w-2 rounded-full bg-amber-400" /> Marcado</span>
                <span className="flex items-center gap-1.5"><i className="h-2 w-2 rounded-full bg-sky-400" /> Confirmado</span>
                <span className="flex items-center gap-1.5"><i className="h-2 w-2 rounded-full bg-violet-400" /> Atendimento</span>
                <span className="flex items-center gap-1.5"><i className="h-2 w-2 rounded-full bg-emerald-400" /> Concluído</span>
                <span className="flex items-center gap-1.5"><i className="h-2 w-2 rounded-full bg-rose-400" /> Cancelado</span>
                <span className="flex items-center gap-1.5"><i className="h-2 w-2 rounded-full bg-slate-500" /> Falta</span>
              </div>
              <div className="mt-5 space-y-3.5">
                {stats.upcomingSchedule.map(day => {
                  const total = day.scheduled + day.confirmed + day.inProgress + day.completed + day.cancelled + day.noShow;
                  const barWidth = `${(total / scheduleMax) * 100}%`;
                  return (
                    <div key={day.date} className="grid grid-cols-[3.8rem_1fr_1.5rem] items-center gap-3 sm:grid-cols-[4.5rem_1fr_2rem]">
                      <span className="text-xs font-semibold capitalize text-slate-300">{formatDay(day.date)}</span>
                      <div className="h-3 overflow-hidden rounded-full bg-slate-800/80" aria-label={`${formatDay(day.date)}: ${total} agendamento(s)`}>
                        {total > 0 && <div className="flex h-full overflow-hidden rounded-full" style={{ width: barWidth }}>
                          {day.scheduled > 0 && <span className="bg-amber-400" style={{ width: `${(day.scheduled / total) * 100}%` }} />}
                          {day.confirmed > 0 && <span className="bg-sky-400" style={{ width: `${(day.confirmed / total) * 100}%` }} />}
                          {day.inProgress > 0 && <span className="bg-violet-400" style={{ width: `${(day.inProgress / total) * 100}%` }} />}
                          {day.completed > 0 && <span className="bg-emerald-400" style={{ width: `${(day.completed / total) * 100}%` }} />}
                          {day.cancelled > 0 && <span className="bg-rose-400" style={{ width: `${(day.cancelled / total) * 100}%` }} />}
                          {day.noShow > 0 && <span className="bg-slate-500" style={{ width: `${(day.noShow / total) * 100}%` }} />}
                        </div>}
                      </div>
                      <span className="text-right font-mono text-xs text-slate-400">{total}</span>
                    </div>
                  );
                })}
              </div>
            </Card>

            <Card className="xl:col-span-2">
              <div className="border-b border-slate-800 pb-4">
                <h2 className="font-display text-lg font-bold text-white">Aniversariantes</h2>
                <p className="mt-0.5 text-xs text-slate-400">Próximos 30 dias.</p>
              </div>
              {stats.upcomingBirthdays.length ? <div className="mt-3 divide-y divide-slate-800/80">{stats.upcomingBirthdays.map(birthday => <div key={birthday.clientId} className="flex items-center gap-3 py-3"><span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-rose-500/10 text-rose-400"><Sparkles className="h-4 w-4" /></span><div className="min-w-0 flex-1"><p className="truncate text-sm font-semibold text-white">{birthday.name}</p><p className="text-[11px] text-slate-400">{formatBirthday(birthday.birthDate)} • fará {birthday.turningAge} anos</p></div><Badge variant={birthday.daysUntil === 0 ? 'success' : 'default'}>{birthday.daysUntil === 0 ? 'Hoje' : `${birthday.daysUntil}d`}</Badge></div>)}</div> : <div className="flex min-h-44 flex-col items-center justify-center text-center"><Users className="h-7 w-7 text-slate-600" /><p className="mt-3 text-sm font-medium text-slate-300">Sem aniversariantes próximos</p><p className="mt-1 text-xs text-slate-500">Cadastre a data de nascimento das clientes.</p></div>}
            </Card>
          </section>

          <section className="grid gap-6 lg:grid-cols-5">
            <Card className="lg:col-span-3">
              <div className="flex items-start justify-between gap-4 border-b border-slate-800 pb-4">
                <div><h2 className="font-display text-lg font-bold text-white">Procedimentos mais agendados</h2><p className="mt-0.5 text-xs text-slate-400">Ranking do mês, sem cancelamentos e faltas.</p></div>
                <FileText className="h-5 w-5 shrink-0 text-rose-400" />
              </div>
              {stats.topProcedures.length ? <div className="mt-4 space-y-4">{stats.topProcedures.map((procedure, index) => <div key={procedure.procedureName}><div className="mb-1.5 flex items-center justify-between gap-3 text-xs"><span className="min-w-0 truncate font-medium text-slate-200"><span className="mr-2 font-mono text-rose-400">{String(index + 1).padStart(2, '0')}</span>{procedure.procedureName}</span><span className="shrink-0 text-slate-400">{procedure.appointments}x • {formatCurrency(procedure.amountReceived)}</span></div><div className="h-2 overflow-hidden rounded-full bg-slate-800"><div className="h-full rounded-full bg-gradient-to-r from-rose-500 to-amber-400" style={{ width: `${(procedure.appointments / topProcedureMax) * 100}%` }} /></div></div>)}</div> : <p className="py-9 text-center text-sm text-slate-500">Ainda não há agendamentos elegíveis neste mês.</p>}
            </Card>
            <Card className="lg:col-span-2">
              <div className="flex items-center gap-3"><span className="flex h-10 w-10 items-center justify-center rounded-xl border border-sky-500/30 bg-sky-500/10 text-sky-400"><Users className="h-5 w-5" /></span><div><h2 className="font-display text-base font-bold text-white">Novos contatos</h2><p className="text-xs text-slate-400">Leads aguardando retorno.</p></div></div><p className="mt-6 font-display text-4xl font-bold text-white">{stats.pendingLeads}</p><p className="mt-2 text-sm leading-relaxed text-slate-400">Priorize o retorno para transformar os contatos recebidos pela landing page em novos atendimentos.</p><div className="mt-5 rounded-lg border border-slate-800 bg-slate-950/40 p-3 text-xs text-slate-400"><strong className="text-slate-200">Dica:</strong> confirme o procedimento de interesse antes de sugerir horários.</div>
            </Card>
          </section>
        </>
      )}
    </div>
  );
};

// INTERACTIVE CALENDAR GRID COMPONENT
export const CalendarGrid: React.FC<{
  appointments: Appointment[];
  operatingHours: OperatingHoursDay[];
  onSelectSlot: (dateStr: string) => void;
  onEditAppointment: (appt: Appointment) => void;
}> = ({ appointments, operatingHours, onSelectSlot, onEditAppointment }) => {
  const { showToast } = useToast();
  const [currentDate, setCurrentDate] = useState(() => new Date(`${clinicTodayKey()}T12:00:00`));
  const [selectedDate, setSelectedDate] = useState(clinicTodayKey);
  const [googleEvents, setGoogleEvents] = useState<GoogleCalendarEvent[]>([]);
  const [googleNotice, setGoogleNotice] = useState<string | null>(null);
  const [isRefreshingGoogle, setIsRefreshingGoogle] = useState(false);

  const year = currentDate.getFullYear();
  const month = currentDate.getMonth();

  const monthNames = [
    "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho",
    "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"
  ];

  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const firstDayIndex = new Date(year, month, 1).getDay();

  const prevMonth = () => setCurrentDate(new Date(year, month - 1, 1));
  const nextMonth = () => setCurrentDate(new Date(year, month + 1, 1));
  const setToday = () => {
    const todayKey = clinicTodayKey();
    const today = new Date(`${todayKey}T12:00:00`);
    setCurrentDate(today);
    setSelectedDate(todayKey);
  };

  const refreshGoogleEvents = async (manual = false) => {
    try {
      setIsRefreshingGoogle(true);
      // A margem cobre início e fim do mês no fuso da clínica, mesmo em outro fuso no dispositivo.
      const fromUtc = new Date(Date.UTC(year, month, 1) - 12 * 60 * 60 * 1000).toISOString();
      const toUtc = new Date(Date.UTC(year, month + 1, 1) + 12 * 60 * 60 * 1000).toISOString();
      const result = await api.getGoogleCalendarEvents(fromUtc, toUtc);
      setGoogleEvents(result.events);
      setGoogleNotice(result.notice || null);
      if (manual) showToast(result.notice || `${result.events.length} evento(s) externo(s) atualizado(s).`, result.notice ? 'info' : 'success');
    } catch (error) {
      setGoogleEvents([]);
      setGoogleNotice('Não foi possível atualizar a agenda compartilhada agora.');
      if (manual) showToast(error instanceof Error ? error.message : 'Não foi possível atualizar a agenda compartilhada.', 'error');
    } finally {
      setIsRefreshingGoogle(false);
    }
  };

  useEffect(() => { void refreshGoogleEvents(); }, [year, month]);

  const apptsByDate: { [key: string]: Appointment[] } = {};
  appointments.forEach(a => {
    const dateKey = clinicDateKey(a.scheduledDateTime);
    if (!apptsByDate[dateKey]) apptsByDate[dateKey] = [];
    apptsByDate[dateKey].push(a);
  });

  const selectedDayAppts = apptsByDate[selectedDate] || [];
  const selectedDayOperatingHours = operatingHoursForDate(selectedDate, operatingHours);
  const selectedDayTimelineAppointments = selectedDayAppts.filter(appointment => isAppointmentWithinOperatingHours(appointment, operatingHours));
  const selectedDayOutsideAppointments = selectedDayAppts.filter(appointment => !isAppointmentWithinOperatingHours(appointment, operatingHours));
  const googleEventsByDate: { [key: string]: GoogleCalendarEvent[] } = {};
  googleEvents.filter(event => isGoogleEventWithinOperatingHours(event, operatingHours)).forEach(event => {
    const dateKey = clinicDateKey(event.startUtc);
    if (!googleEventsByDate[dateKey]) googleEventsByDate[dateKey] = [];
    googleEventsByDate[dateKey].push(event);
  });
  const selectedDayGoogleEvents = googleEventsByDate[selectedDate] || [];
  const [startHour, startMinute] = selectedDayOperatingHours.startTime.split(':').map(Number);
  const [endHour, endMinute] = selectedDayOperatingHours.endTime.split(':').map(Number);
  const firstVisibleHour = Math.floor(startHour + startMinute / 60);
  const lastVisibleHour = Math.ceil(endHour + endMinute / 60);
  const hours = selectedDayOperatingHours.isOpen
    ? Array.from({ length: Math.max(0, lastVisibleHour - firstVisibleHour) }, (_, index) => index + firstVisibleHour)
    : [];

  return (
    <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
      <Card className="lg:col-span-2 space-y-4 p-4 sm:p-6">
        <div className="flex items-center justify-between flex-wrap gap-3 border-b border-slate-800 pb-3">
          <div className="flex items-center gap-3">
            <h2 className="font-display text-lg sm:text-xl font-bold text-white">
              {monthNames[month]} <span className="text-rose-400">{year}</span>
            </h2>
            <Button variant="outline" size="sm" onClick={setToday}>Hoje</Button>
          </div>

          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" onClick={() => void refreshGoogleEvents(true)} disabled={isRefreshingGoogle} title="Atualizar eventos da agenda compartilhada"><RefreshCw className={`w-3.5 h-3.5 ${isRefreshingGoogle ? 'animate-spin' : ''}`} /><span className="hidden sm:inline">Google</span></Button>
            <Button variant="outline" size="sm" onClick={prevMonth}><ChevronLeft className="w-4 h-4" /></Button>
            <Button variant="outline" size="sm" onClick={nextMonth}><ChevronRight className="w-4 h-4" /></Button>
          </div>
        </div>

        <div className="grid grid-cols-7 gap-1 text-center font-semibold text-[11px] sm:text-xs text-slate-400 uppercase pb-1">
          <span>Dom</span><span>Seg</span><span>Ter</span><span>Qua</span><span>Qui</span><span>Sex</span><span>Sáb</span>
        </div>

        <div className="grid grid-cols-7 gap-1 sm:gap-1.5 text-xs">
          {Array.from({ length: firstDayIndex }).map((_, i) => (
            <div key={`empty-${i}`} className="h-12 sm:h-16 rounded-lg bg-slate-950/30"></div>
          ))}

          {Array.from({ length: daysInMonth }).map((_, i) => {
            const dayNum = i + 1;
            const formattedDay = dayNum < 10 ? `0${dayNum}` : `${dayNum}`;
            const formattedMonth = (month + 1) < 10 ? `0${month + 1}` : `${month + 1}`;
            const dateStr = `${year}-${formattedMonth}-${formattedDay}`;

            const dayAppts = apptsByDate[dateStr] || [];
            const dayGoogleEvents = googleEventsByDate[dateStr] || [];
            const isSelected = selectedDate === dateStr;
            const isTodayStr = clinicTodayKey() === dateStr;

            return (
              <button
                key={dateStr}
                onClick={() => setSelectedDate(dateStr)}
                className={`h-12 sm:h-16 p-1 sm:p-1.5 rounded-xl border flex flex-col justify-between transition-all text-left relative overflow-hidden ${
                  isSelected
                    ? 'border-rose-500 bg-rose-600/10 shadow-md ring-1 ring-rose-500'
                    : isTodayStr
                    ? 'border-amber-500/50 bg-slate-900/90'
                    : 'border-slate-800 hover:border-slate-700 bg-slate-900/40'
                }`}
              >
                <div className="flex items-center justify-between w-full">
                  <span className={`font-mono text-xs font-semibold ${isTodayStr ? 'text-amber-400 font-bold' : isSelected ? 'text-rose-400 font-bold' : 'text-slate-300'}`}>
                    {dayNum}
                  </span>
                  {dayAppts.length > 0 && (
                    <span className="px-1 py-0.2 rounded-full bg-rose-500/20 text-rose-400 text-[9px] font-bold">
                      {dayAppts.length}
                    </span>
                  )}
                  {dayGoogleEvents.length > 0 && (
                    <span className="px-1 py-0.2 rounded-full bg-sky-500/15 text-sky-400 text-[9px] font-bold">
                      G{dayGoogleEvents.length}
                    </span>
                  )}
                </div>

                {dayAppts.length > 0 && (
                  <div className="hidden sm:block space-y-0.5">
                    <div className="flex gap-1">
                      {dayAppts.slice(0, 3).map((a, idx) => (
                        <span
                          key={idx}
                          className={`w-1.5 h-1.5 rounded-full ${
                            a.status === 'Confirmed' || a.status === 'Completed' ? 'bg-emerald-400' :
                            a.status === 'Cancelled' || a.status === 'NoShow' ? 'bg-rose-500' : 'bg-amber-400'
                          }`}
                        />
                      ))}
                    </div>
                  </div>
                )}
                {dayGoogleEvents.length > 0 && <div className="hidden sm:flex gap-1"><span className="h-1.5 w-1.5 rounded-full bg-sky-400" /></div>}
              </button>
            );
          })}
        </div>
      </Card>

      <Card className="space-y-4 p-4 sm:p-6">
        <div className="flex items-center justify-between border-b border-slate-800 pb-3">
          <div>
            <h3 className="font-display font-bold text-white text-base">Timeline do Dia</h3>
            <span className="text-xs text-rose-400 font-mono">
              {new Date(selectedDate + 'T00:00:00').toLocaleDateString('pt-BR', { dateStyle: 'medium' })}
            </span>
          </div>
          <Button variant="primary" size="sm" disabled={!selectedDayOperatingHours.isOpen} onClick={() => onSelectSlot(`${selectedDate}T${selectedDayOperatingHours.isOpen ? selectedDayOperatingHours.startTime : '09:00'}`)}>
            <Plus className="w-3.5 h-3.5" /> Novo
          </Button>
        </div>

        {googleNotice && <p className="rounded-lg border border-sky-500/20 bg-sky-500/5 px-3 py-2 text-xs text-sky-200">Agenda Google: {googleNotice}</p>}

        {!selectedDayOperatingHours.isOpen && <p className="rounded-lg border border-slate-800 bg-slate-950/35 px-3 py-2 text-xs text-slate-400">Não há expediente configurado para este dia. Novos agendamentos ficam bloqueados.</p>}

        <div className="space-y-2 max-h-[500px] overflow-y-auto pr-1">
          {selectedDayGoogleEvents.filter(event => event.isAllDay).map(event => (
            <div key={event.id} className="rounded-lg border border-sky-500/35 bg-sky-500/10 p-2.5 text-xs text-sky-100">
              <div className="flex items-center justify-between gap-2"><span className="font-semibold text-sky-300">Google Calendar · dia todo</span>{event.htmlLink && <a href={event.htmlLink} target="_blank" rel="noreferrer" className="text-[10px] font-medium text-sky-300 hover:text-sky-100">Abrir</a>}</div>
              <p className="mt-1 font-medium">{event.title}</p>
            </div>
          ))}
          {hours.map(hour => {
            const hourStr = hour < 10 ? `0${hour}` : `${hour}`;
            const matchedAppts = selectedDayTimelineAppointments
              .filter(a => clinicTimeKey(a.scheduledDateTime).startsWith(`${hourStr}:`))
              .sort((first, second) => first.scheduledDateTime.localeCompare(second.scheduledDateTime));
            const matchedGoogleEvents = selectedDayGoogleEvents
              .filter(event => !event.isAllDay && clinicTimeKey(event.startUtc).startsWith(`${hourStr}:`))
              .sort((first, second) => first.startUtc.localeCompare(second.startUtc));

            return (
              <div key={hour} className="flex gap-3 text-xs border-b border-slate-800/60 pb-2">
                <span className="font-mono text-slate-400 w-12 pt-1 font-semibold shrink-0">{hourStr}:00</span>

                <div className="flex-1 space-y-1.5">
                  {matchedAppts.length === 0 && matchedGoogleEvents.length === 0 ? (
                    <button
                      onClick={() => onSelectSlot(`${selectedDate}T${hour === firstVisibleHour ? selectedDayOperatingHours.startTime : `${hourStr}:00`}`)}
                      className="w-full text-left p-1.5 rounded bg-slate-900/30 hover:bg-slate-800/50 border border-dashed border-slate-800/80 text-slate-500 hover:text-slate-300 transition-colors"
                    >
                      + Livre para agendamento
                    </button>
                  ) : (
                    <>
                      {matchedAppts.map(appt => (
                      <div
                        key={appt.id}
                        onClick={() => onEditAppointment(appt)}
                        className="p-2.5 rounded-lg bg-slate-900 border border-slate-700/80 hover:border-rose-500/50 transition-all cursor-pointer space-y-1 shadow-sm"
                      >
                        <div className="flex items-center justify-between gap-2">
                          <div className="flex min-w-0 items-center gap-2">
                            <span className="shrink-0 font-mono text-[10px] font-semibold text-rose-400">{clinicTimeKey(appt.scheduledDateTime)}</span>
                            <span className="truncate font-bold text-white text-xs">{appt.clientName}</span>
                          </div>
                          <Badge variant={appointmentStatusBadge(appt.status)}>
                            {appointmentStatusLabels[appt.status]}
                          </Badge>
                        </div>
                        <p className="text-[11px] text-slate-300">{appt.procedureName}</p>
                        <div className="flex items-center justify-between text-[10px] text-slate-400 pt-1 border-t border-slate-800">
                          <span className="font-mono text-rose-400 font-semibold">R$ {appt.totalPrice.toFixed(2)}</span>
                          <span>{appt.professionalName || 'Dra. Mariana'}</span>
                        </div>
                      </div>
                      ))}
                      {matchedGoogleEvents.map(event => (
                        <div key={event.id} className="space-y-1 rounded-lg border border-sky-500/35 bg-sky-500/10 p-2.5 shadow-sm">
                          <div className="flex items-center justify-between gap-2"><div className="flex min-w-0 items-center gap-2"><span className="shrink-0 font-mono text-[10px] font-semibold text-sky-300">{clinicTimeKey(event.startUtc)}</span><span className="truncate font-bold text-sky-100 text-xs">{event.title}</span></div>{event.htmlLink && <a href={event.htmlLink} target="_blank" rel="noreferrer" onClick={clickEvent => clickEvent.stopPropagation()} className="shrink-0 text-[10px] font-semibold text-sky-300 hover:text-sky-100">Abrir</a>}</div>
                          <p className="border-t border-sky-400/15 pt-1 text-[10px] text-sky-200/80">Agenda compartilhada · até {clinicTimeKey(event.endUtc)}</p>
                        </div>
                      ))}
                    </>
                  )}
                </div>
              </div>
            );
          })}
          {selectedDayOutsideAppointments.length > 0 && <div className="mt-4 border-t border-amber-500/20 pt-3">
            <div className="mb-2 flex items-start gap-2"><AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-amber-400" /><div><p className="text-xs font-semibold text-amber-200">Fora do expediente</p><p className="text-[10px] leading-relaxed text-slate-500">Atendimentos preservados após uma alteração de horário. Eles continuam editáveis.</p></div></div>
            <div className="space-y-1.5">
              {selectedDayOutsideAppointments.sort((first, second) => first.scheduledDateTime.localeCompare(second.scheduledDateTime)).map(appt => (
                <button key={appt.id} type="button" onClick={() => onEditAppointment(appt)} className="w-full rounded-lg border border-amber-500/25 bg-amber-500/5 p-2.5 text-left transition-colors hover:border-amber-400/50">
                  <div className="flex items-center justify-between gap-2"><span className="font-mono text-[10px] font-semibold text-amber-300">{clinicTimeKey(appt.scheduledDateTime)}</span><Badge variant={appointmentStatusBadge(appt.status)}>{appointmentStatusLabels[appt.status]}</Badge></div>
                  <p className="mt-1 truncate text-xs font-bold text-white">{appt.clientName}</p><p className="mt-0.5 truncate text-[11px] text-slate-300">{appt.procedureName}</p>
                </button>
              ))}
            </div>
          </div>}
        </div>
      </Card>
    </div>
  );
};

// APPOINTMENTS VIEW (WITH COMBOBOX CLIENT SEARCH)
export const AppointmentsView: React.FC<{ procedures: ProcedureType[]; clients: Client[] }> = ({ procedures, clients }) => {
  const { showToast } = useToast();
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [operatingHours, setOperatingHours] = useState<OperatingHoursDay[]>(defaultOperatingHours);
  const [viewMode, setViewMode] = useState<'calendar' | 'list'>('calendar');
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedDateFilter, setSelectedDateFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState('ALL');
  const [paymentFilter, setPaymentFilter] = useState('ALL');
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingAppointment, setEditingAppointment] = useState<Appointment | null>(null);
  const { confirm, confirmationDialog } = useConfirmationDialog();

  const [form, setForm] = useState({
    clientId: '',
    procedureTypeId: '',
    scheduledDateTime: clinicDateTimeInputValue(new Date()),
    status: 'Scheduled' as AppointmentStatus,
    totalPrice: currencyInputFromNumber(0),
    amountPaid: currencyInputFromNumber(0),
    paymentMethod: 'Pix' as PaymentMethod,
    professionalName: 'Dra. Mariana Siqueira',
    notes: ''
  });

  const loadData = async () => {
    const loadedAppointments = await api.getAppointments();
    setAppointments(loadedAppointments);
    try {
      const settings = await api.getClinicOperationalSettings();
      setOperatingHours(settings.operatingHours);
    } catch {
      // Usuários sem acesso às configurações mantêm a grade padrão; o backend continua aplicando a regra oficial.
      setOperatingHours(defaultOperatingHours);
    }
  };
  useEffect(() => { loadData(); }, []);

  const confirmGoogleConflict = async (procedure?: ProcedureType) => {
    const [datePart] = form.scheduledDateTime.split('T');
    const [year, month, day] = datePart.split('-').map(Number);
    if (!year || !month || !day) return true;

    try {
      const fromUtc = new Date(Date.UTC(year, month - 1, day) - 12 * 60 * 60 * 1000).toISOString();
      const toUtc = new Date(Date.UTC(year, month - 1, day + 1) + 12 * 60 * 60 * 1000).toISOString();
      const google = await api.getGoogleCalendarEvents(fromUtc, toUtc);
      if (!google.isConnected || !google.hasSelectedCalendar) return true;

      const start = new Date(form.scheduledDateTime).getTime();
      const duration = Math.max(5, procedure?.durationMinutes || 60);
      const end = start + duration * 60_000;
      const conflicts = google.events.filter(event => isGoogleEventWithinOperatingHours(event, operatingHours) && (event.isAllDay || (
        start < new Date(event.endUtc).getTime() && end > new Date(event.startUtc).getTime()
      )));
      if (!conflicts.length) return true;

      return await confirm({
        title: 'Conflito com agenda compartilhada',
        description: `Há ${conflicts.length} compromisso(s) da agenda compartilhada neste horário (${conflicts.slice(0, 2).map(event => event.title).join(', ')}). Deseja confirmar este agendamento mesmo assim?`,
        confirmLabel: 'Agendar mesmo assim',
        variant: 'primary',
      });
    } catch {
      // Falha de leitura não impede a agenda interna; o envio ao Google continuará sendo tentado após salvar.
      return true;
    }
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    const targetClient = clients.find(c => c.id === form.clientId);
    const targetProc = procedures.find(p => p.id === form.procedureTypeId);
    if (!await confirmGoogleConflict(targetProc)) return;

    if (editingAppointment) {
      const saved = await api.saveAppointment({
        ...editingAppointment,
        scheduledDateTime: form.scheduledDateTime,
        totalPrice: parseCurrencyInput(form.totalPrice),
        amountPaid: parseCurrencyInput(form.amountPaid),
        paymentMethod: form.paymentMethod,
        status: form.status,
        professionalName: form.professionalName,
        notes: form.notes
      });
      showToast(saved.googleCalendarSyncWarning || 'Agendamento atualizado com sucesso!', saved.googleCalendarSyncWarning ? 'warning' : 'success');
    } else {
      const saved = await api.saveAppointment({
        clientId: form.clientId,
        clientName: targetClient?.name || 'Cliente',
        procedureTypeId: form.procedureTypeId,
        procedureName: targetProc?.name || 'Procedimento',
        scheduledDateTime: form.scheduledDateTime,
        status: form.status,
        totalPrice: parseCurrencyInput(form.totalPrice) || targetProc?.price || 0,
        amountPaid: parseCurrencyInput(form.amountPaid),
        paymentMethod: form.paymentMethod,
        professionalName: form.professionalName,
        notes: form.notes
      });
      showToast(saved.googleCalendarSyncWarning || 'Novo agendamento realizado!', saved.googleCalendarSyncWarning ? 'warning' : 'success');
    }

    setIsModalOpen(false);
    setEditingAppointment(null);
    loadData();
  };

  const handleOpenEdit = (appt: Appointment) => {
    setEditingAppointment(appt);
    setForm({
      clientId: appt.clientId,
      procedureTypeId: appt.procedureTypeId,
      scheduledDateTime: clinicDateTimeInputValue(appt.scheduledDateTime),
      status: appt.status,
      totalPrice: currencyInputFromNumber(appt.totalPrice),
      amountPaid: currencyInputFromNumber(appt.amountPaid),
      paymentMethod: appt.paymentMethod,
      professionalName: appt.professionalName || 'Dra. Mariana Siqueira',
      notes: appt.notes || ''
    });
    setIsModalOpen(true);
  };

  const handleSelectSlot = (dateStr: string) => {
    setEditingAppointment(null);
    setForm({
      clientId: '',
      procedureTypeId: '',
      scheduledDateTime: dateStr,
      status: 'Scheduled',
      totalPrice: currencyInputFromNumber(0),
      amountPaid: currencyInputFromNumber(0),
      paymentMethod: 'Pix',
      professionalName: 'Dra. Mariana Siqueira',
      notes: ''
    });
    setIsModalOpen(true);
  };

  const handleStatusChange = async (id: string, status: AppointmentStatus) => {
    await api.updateAppointmentStatus(id, status);
    showToast(`Status alterado para ${status}!`, 'info');
    loadData();
  };

  const filteredAppointments = appointments.filter(appt => {
    const matchesSearch =
      appt.clientName.toLowerCase().includes(searchTerm.toLowerCase()) ||
      appt.procedureName.toLowerCase().includes(searchTerm.toLowerCase());

    const matchesDate = !selectedDateFilter || clinicDateKey(appt.scheduledDateTime) === selectedDateFilter;
    const matchesStatus = statusFilter === 'ALL' || appt.status === statusFilter;
    const matchesPayment = paymentFilter === 'ALL' || appt.paymentStatus === paymentFilter;

    return matchesSearch && matchesDate && matchesStatus && matchesPayment;
  });

  return (
    <div className="space-y-6">
      <AdminPageHeader
        title="Agenda profissional & atendimentos"
        description="Gerencie horários no calendário interativo ou consulte a lista detalhada."
        actions={<>
          <div className="flex w-full rounded-lg border border-slate-800 bg-slate-900 p-1 sm:w-auto">
            <button
              onClick={() => setViewMode('calendar')}
              className={`flex flex-1 items-center justify-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-semibold transition-all ${
                viewMode === 'calendar' ? 'bg-rose-600 text-white shadow-sm' : 'text-slate-400 hover:text-white'
              }`}
            >
              <CalendarIcon className="w-3.5 h-3.5" /> Grade Calendário
            </button>
            <button
              onClick={() => setViewMode('list')}
              className={`flex flex-1 items-center justify-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-semibold transition-all ${
                viewMode === 'list' ? 'bg-rose-600 text-white shadow-sm' : 'text-slate-400 hover:text-white'
              }`}
            >
              <FileText className="w-3.5 h-3.5" /> Modo Lista / Tabela
            </button>
          </div>

          <Button variant="primary" onClick={() => { setEditingAppointment(null); setIsModalOpen(true); }}>
            <Plus className="w-4 h-4" /> Novo Agendamento
          </Button>
        </>}
      />

      {viewMode === 'calendar' ? (
        <CalendarGrid
          appointments={appointments}
          operatingHours={operatingHours}
          onSelectSlot={handleSelectSlot}
          onEditAppointment={handleOpenEdit}
        />
      ) : (
        <>
          <Card className="p-4">
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              <Input
                label="Buscar cliente ou procedimento"
                placeholder="Buscar por cliente ou procedimento..."
                value={searchTerm}
                onChange={e => setSearchTerm(e.target.value)}
              />
              <Input
                type="date"
                label="Filtrar por Data"
                value={selectedDateFilter}
                onChange={e => setSelectedDateFilter(e.target.value)}
              />
              <Select label="Status Atendimento" value={statusFilter} onChange={e => setStatusFilter(e.target.value)}>
                <option value="ALL">Todos os Status</option>
                <option value="Scheduled">Agendado</option>
                <option value="Confirmed">Confirmado</option>
                <option value="InProgress">Atendimento</option>
                <option value="Completed">Concluído</option>
                <option value="Cancelled">Cancelado</option>
                <option value="NoShow">Faltou</option>
              </Select>
              <Select label="Status Financeiro" value={paymentFilter} onChange={e => setPaymentFilter(e.target.value)}>
                <option value="ALL">Todos os Pagamentos</option>
                <option value="Paid">Pago (100%)</option>
                <option value="Partial">Parcial / Sinal</option>
                <option value="Pending">Pendente</option>
              </Select>
            </div>
          </Card>

          <Card className="hidden lg:block">
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm text-slate-300">
                <thead className="bg-slate-900/80 text-xs uppercase text-slate-400 border-b border-slate-800">
                  <tr>
                    <th className="p-3">Data / Hora</th>
                    <th className="p-3">Cliente</th>
                    <th className="p-3">Procedimento</th>
                    <th className="p-3">Profissional</th>
                    <th className="p-3">Valor Total</th>
                    <th className="p-3">Pago</th>
                    <th className="p-3">Status</th>
                    <th className="p-3 text-right">Ações</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800/60">
                  {filteredAppointments.map(appt => (
                    <tr key={appt.id} className="hover:bg-slate-900/40">
                      <td className="p-3 font-mono text-xs text-slate-200">
                        {formatClinicDateTime(appt.scheduledDateTime)}
                      </td>
                      <td className="p-3 font-semibold text-white">{appt.clientName}</td>
                      <td className="p-3 text-slate-300">{appt.procedureName}</td>
                      <td className="p-3 text-xs text-slate-400">{appt.professionalName || 'Dra. Mariana'}</td>
                      <td className="p-3 font-mono text-rose-400 font-semibold">R$ {appt.totalPrice.toFixed(2)}</td>
                      <td className="p-3 font-mono text-emerald-400">
                        R$ {appt.amountPaid.toFixed(2)} ({appt.paymentMethod})
                      </td>
                      <td className="p-3">
                        <Select
                          value={appt.status}
                          onChange={e => handleStatusChange(appt.id, e.target.value as AppointmentStatus)}
                          className="text-xs py-1 px-2"
                        >
                          <option value="Scheduled">Agendado</option>
                          <option value="Confirmed">Confirmado</option>
                          <option value="InProgress">Atendimento</option>
                          <option value="Completed">Concluído</option>
                          <option value="Cancelled">Cancelado</option>
                          <option value="NoShow">Faltou</option>
                        </Select>
                      </td>
                      <td className="p-3 text-right">
                        <Button variant="outline" size="sm" onClick={() => handleOpenEdit(appt)}>
                          <Edit className="w-3.5 h-3.5" /> Editar
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:hidden gap-4">
            {filteredAppointments.map(appt => (
              <Card key={appt.id} className="space-y-3 p-4 border-l-4 border-l-rose-500">
                <div className="flex items-center justify-between border-b border-slate-800 pb-2">
                  <span className="font-mono text-xs text-rose-400 font-semibold flex items-center gap-1">
                    <CalendarIcon className="w-3.5 h-3.5" />
                    {formatClinicDateTime(appt.scheduledDateTime)}
                  </span>
                  <Badge variant={appt.paymentStatus === 'Paid' ? 'success' : appt.paymentStatus === 'Partial' ? 'warning' : 'danger'}>
                    {appt.paymentStatus === 'Paid' ? '100% Pago' : appt.paymentStatus === 'Partial' ? 'Sinal' : 'Pendente'}
                  </Badge>
                </div>
                <div>
                  <h3 className="font-display font-bold text-white text-base">{appt.clientName}</h3>
                  <p className="text-xs text-slate-300 font-medium">{appt.procedureName}</p>
                </div>
                <div className="flex items-center justify-between text-xs pt-1 border-t border-slate-800">
                  <span className="font-mono font-bold text-rose-400">R$ {appt.totalPrice.toFixed(2)}</span>
                  <Select
                    value={appt.status}
                    onChange={e => handleStatusChange(appt.id, e.target.value as AppointmentStatus)}
                    className="text-xs py-1 px-2 w-auto"
                  >
                    <option value="Scheduled">Agendado</option>
                    <option value="Confirmed">Confirmado</option>
                    <option value="InProgress">Atendimento</option>
                    <option value="Completed">Concluído</option>
                    <option value="Cancelled">Cancelado</option>
                    <option value="NoShow">Faltou</option>
                  </Select>
                </div>
                <Button variant="outline" size="sm" className="w-full" onClick={() => handleOpenEdit(appt)}>
                  <Edit className="w-3.5 h-3.5" /> Editar Agendamento
                </Button>
              </Card>
            ))}
          </div>
        </>
      )}

      {/* CREATE / EDIT APPOINTMENT MODAL WITH COMBOBOX */}
      <Dialog
        isOpen={isModalOpen}
        onClose={() => { setIsModalOpen(false); setEditingAppointment(null); }}
        title={editingAppointment ? "Editar Agendamento" : "Agendar Novo Atendimento"}
      >
        <form onSubmit={handleCreate} className="space-y-4">
          {!editingAppointment && (
            <>
              {/* SEARCHABLE CLIENT COMBOBOX */}
              <SharedClientSelectCombobox
                clients={clients}
                selectedClientId={form.clientId}
                onSelectClient={(client) => setForm({...form, clientId: client.id})}
              />

              <Select
                label="Procedimento Estético"
                required
                value={form.procedureTypeId}
                onChange={e => {
                  const proc = procedures.find(p => p.id === e.target.value);
                  setForm({...form, procedureTypeId: e.target.value, totalPrice: currencyInputFromNumber(proc?.price || 0)});
                }}
              >
                <option value="">Selecione o procedimento...</option>
                {procedures.filter(p => p.isActive).map(p => <option key={p.id} value={p.id}>{p.name} - R$ {p.price}</option>)}
              </Select>
            </>
          )}

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Input
              label="Data e Hora"
              type="datetime-local"
              required
              value={form.scheduledDateTime}
              onChange={e => setForm({...form, scheduledDateTime: e.target.value})}
            />
            <Input
              label="Profissional Responsável"
              value={form.professionalName}
              onChange={e => setForm({...form, professionalName: e.target.value})}
            />
          </div>

          {editingAppointment && (
            <Select
              label="Status do Atendimento"
              value={form.status}
              onChange={e => setForm({...form, status: e.target.value as AppointmentStatus})}
            >
              <option value="Scheduled">Agendado</option>
              <option value="Confirmed">Confirmado</option>
              <option value="InProgress">Atendimento</option>
              <option value="Completed">Concluído</option>
              <option value="Cancelled">Cancelado</option>
              <option value="NoShow">Faltou</option>
            </Select>
          )}

          <div className="grid grid-cols-2 gap-4">
            <Input
              label="Valor Total (R$)"
              inputMode="numeric"
              value={form.totalPrice}
              onChange={e => setForm({...form, totalPrice: formatCurrencyInput(e.target.value)})}
            />
            <Input
              label="Valor Pago Inicial (R$)"
              inputMode="numeric"
              value={form.amountPaid}
              onChange={e => setForm({...form, amountPaid: formatCurrencyInput(e.target.value)})}
            />
          </div>

          <Select
            label="Forma de Pagamento"
            value={form.paymentMethod}
            onChange={e => setForm({...form, paymentMethod: e.target.value as PaymentMethod})}
          >
            <option value="Pix">PIX</option>
            <option value="CreditCard">Cartão de Crédito</option>
            <option value="DebitCard">Cartão de Débito</option>
            <option value="Cash">Dinheiro</option>
            <option value="PackageSession">Pacote / Sessão Pré-paga</option>
          </Select>

          <Input
            label="Observações da Recepção"
            value={form.notes}
            onChange={e => setForm({...form, notes: e.target.value})}
          />

          <Button type="submit" variant="primary" className="w-full">
            {editingAppointment ? "Salvar Alterações" : "Confirmar e Agendar"}
          </Button>
        </form>
      </Dialog>
      {confirmationDialog}
    </div>
  );
};

type ProcedureForm = {
  name: string;
  description: string;
  price: string;
  durationMinutes: string;
  imageUrl: string;
  isPublicWebsite: boolean;
  isPriceHiddenOnWebsite: boolean;
  isActive: boolean;
  isFeaturedInCarousel: boolean;
  recommendedReturnDays: string;
};

const emptyProcedureForm = (): ProcedureForm => ({
  name: '',
  description: '',
  price: currencyInputFromNumber(0),
  durationMinutes: '60',
  imageUrl: '',
  isPublicWebsite: true,
  isPriceHiddenOnWebsite: false,
  isActive: true,
  isFeaturedInCarousel: false,
  recommendedReturnDays: '',
});

// PROCEDURE CATALOG MANAGEMENT
export const ProceduresView: React.FC<{ procedures: ProcedureType[]; onRefresh: () => void | Promise<void> }> = ({ procedures, onRefresh }) => {
  const { can } = useAuth();
  const { showToast } = useToast();
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingProcedure, setEditingProcedure] = useState<ProcedureType | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [form, setForm] = useState<ProcedureForm>(emptyProcedureForm);
  const [pendingDeactivation, setPendingDeactivation] = useState<ProcedureType | null>(null);
  const canManage = can('procedures.manage');

  const closeModal = () => {
    if (isSaving) return;
    setIsModalOpen(false);
    setEditingProcedure(null);
    setImageFile(null);
  };

  const openCreate = () => {
    setEditingProcedure(null);
    setForm(emptyProcedureForm());
    setImageFile(null);
    setIsModalOpen(true);
  };

  const openEdit = (procedure: ProcedureType) => {
    setEditingProcedure(procedure);
    setForm({
      name: procedure.name,
      description: procedure.description,
      price: currencyInputFromNumber(procedure.price),
      durationMinutes: String(procedure.durationMinutes),
      imageUrl: procedure.imageUrl,
      isPublicWebsite: procedure.isPublicWebsite,
      isPriceHiddenOnWebsite: procedure.isPriceHiddenOnWebsite,
      isActive: procedure.isActive,
      isFeaturedInCarousel: procedure.isFeaturedInCarousel,
      recommendedReturnDays: procedure.recommendedReturnDays ? String(procedure.recommendedReturnDays) : '',
    });
    setImageFile(null);
    setIsModalOpen(true);
  };

  const handleImageSelection = (file: File | null) => {
    if (!file) return;
    const allowedTypes = ['image/jpeg', 'image/png', 'image/webp'];
    if (!allowedTypes.includes(file.type) || file.size > 25 * 1024 * 1024) {
      showToast('Use uma imagem JPG, PNG ou WEBP de até 25 MB.', 'error');
      return;
    }
    setImageFile(file);
  };

  const handleSave = async (event: React.FormEvent) => {
    event.preventDefault();
    const durationMinutes = Number(form.durationMinutes);

    if (durationMinutes < 5 || durationMinutes > 1440) {
      showToast('A duração deve ficar entre 5 minutos e 24 horas.', 'error');
      return;
    }

    const recommendedReturnDays = form.recommendedReturnDays ? Number(form.recommendedReturnDays) : null;
    if (recommendedReturnDays !== null && (recommendedReturnDays < 1 || recommendedReturnDays > 3650)) {
      showToast('O retorno recomendado deve ficar entre 1 dia e 10 anos.', 'error');
      return;
    }

    try {
      setIsSaving(true);
      const savedProcedure = await api.saveProcedure({
        ...(editingProcedure ? { id: editingProcedure.id } : {}),
        name: form.name.trim(),
        description: form.description.trim(),
        price: parseCurrencyInput(form.price),
        durationMinutes,
        imageUrl: form.imageUrl.trim(),
        isPublicWebsite: form.isPublicWebsite,
        isPriceHiddenOnWebsite: form.isPriceHiddenOnWebsite,
        isActive: form.isActive,
        isFeaturedInCarousel: form.isFeaturedInCarousel,
        recommendedReturnDays,
      });

      if (imageFile) {
        try {
          const updatedProcedure = await api.uploadProcedureImage(savedProcedure.id, imageFile);
          setImageFile(null);
          setEditingProcedure(updatedProcedure);
          setForm(current => ({ ...current, imageUrl: updatedProcedure.imageUrl }));
        } catch (error) {
          setEditingProcedure(savedProcedure);
          await onRefresh();
          showToast(error instanceof Error ? `Procedimento salvo, mas a imagem não foi enviada: ${error.message}` : 'Procedimento salvo, mas não foi possível enviar a imagem.', 'error');
          return;
        }
      }

      showToast(editingProcedure ? 'Procedimento atualizado com sucesso!' : 'Procedimento cadastrado com sucesso!', 'success');
      setIsModalOpen(false);
      setEditingProcedure(null);
      setImageFile(null);
      await onRefresh();
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível salvar o procedimento.', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  const handleDeactivate = async () => {
    if (!pendingDeactivation) return;
    try {
      setIsSaving(true);
      await api.deactivateProcedure(pendingDeactivation.id);
      showToast('Procedimento inativado. Os atendimentos e evoluções existentes foram preservados.', 'success');
      setPendingDeactivation(null);
      await onRefresh();
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível inativar o procedimento.', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  const handleReactivate = async (procedure: ProcedureType) => {
    try {
      setIsSaving(true);
      await api.saveProcedure({ ...procedure, isActive: true });
      showToast('Procedimento reativado no catálogo interno.', 'success');
      await onRefresh();
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível reativar o procedimento.', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <AdminPageHeader title="Procedimentos & tabela de preços" description="Mantenha os serviços, valores, duração e visibilidade da landing page atualizados." actions={canManage ? <Button variant="primary" onClick={openCreate}><Plus className="h-4 w-4" /> Cadastrar procedimento</Button> : undefined} />

      {!procedures.length ? (
        <Card className="py-12 text-center">
          <Stethoscope className="mx-auto h-8 w-8 text-slate-600" />
          <p className="mt-3 font-medium text-slate-200">Nenhum procedimento cadastrado.</p>
          {canManage && <Button className="mt-4" variant="primary" onClick={openCreate}><Plus className="h-4 w-4" /> Criar o primeiro</Button>}
        </Card>
      ) : (
        <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
          {procedures.map(procedure => (
            <Card key={procedure.id} className="flex h-full flex-col gap-3 p-4">
              {procedure.imageUrl ? (
                <img src={procedure.imageUrl} alt={procedure.name} className="h-40 w-full rounded-lg object-cover" />
              ) : (
                <div className="flex h-40 w-full items-center justify-center rounded-lg border border-dashed border-slate-700 bg-slate-900/50 text-xs text-slate-500">Sem imagem de capa</div>
              )}
              <div className="flex items-start justify-between gap-3">
                <div>
                  <h3 className="font-display text-lg font-bold text-white">{procedure.name}</h3>
                  <p className="mt-1 text-xs leading-relaxed text-slate-400">{procedure.description}</p>
                </div>
                {canManage && <div className="flex shrink-0 gap-1.5"><Button type="button" variant="outline" size="sm" onClick={() => openEdit(procedure)} aria-label={`Editar ${procedure.name}`}><Edit className="h-3.5 w-3.5" /></Button>{procedure.isActive ? <Button type="button" variant="danger" size="sm" onClick={() => setPendingDeactivation(procedure)} aria-label={`Inativar ${procedure.name}`}><Trash2 className="h-3.5 w-3.5" /></Button> : <Button type="button" variant="outline" size="sm" onClick={() => void handleReactivate(procedure)} disabled={isSaving}>Reativar</Button>}</div>}
              </div>
              <div className="mt-auto flex items-center justify-between border-t border-slate-800 pt-3">
                <span className="font-mono font-bold text-rose-400">{currencyInputFromNumber(procedure.price)}</span>
                <span className="text-xs text-slate-500">{procedure.durationMinutes} min</span>
              </div>
              <div className="flex flex-wrap gap-1.5 text-[10px]">
                <Badge variant={procedure.isActive ? 'success' : 'default'}>{procedure.isActive ? 'Ativo' : 'Inativo'}</Badge>
                <Badge variant={procedure.isPublicWebsite ? 'info' : 'default'}>{procedure.isPublicWebsite ? 'No site' : 'Interno'}</Badge>
                {procedure.isPriceHiddenOnWebsite && <Badge variant="default">Preço oculto</Badge>}
                {procedure.isFeaturedInCarousel && <Badge variant="warning">Destaque</Badge>}
                {procedure.recommendedReturnDays && <Badge variant="info">Retorno: {procedure.recommendedReturnDays} dia(s)</Badge>}
              </div>
            </Card>
          ))}
        </div>
      )}

      <Dialog isOpen={isModalOpen} onClose={closeModal} title={editingProcedure ? 'Editar Procedimento' : 'Cadastrar Procedimento'} maxWidth="max-w-2xl">
        <form onSubmit={handleSave} className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-[1fr_10rem_12rem]">
            <Input label="Nome do procedimento" required maxLength={160} value={form.name} onChange={event => setForm({ ...form, name: event.target.value })} />
            <Input label="Duração (minutos)" required inputMode="numeric" pattern="[0-9]*" value={form.durationMinutes} onChange={event => setForm({ ...form, durationMinutes: event.target.value.replace(/\D/g, '').slice(0, 4) })} />
            <Input label="Retorno recomendado (dias)" inputMode="numeric" pattern="[0-9]*" placeholder="Não configurar" value={form.recommendedReturnDays} onChange={event => setForm({ ...form, recommendedReturnDays: event.target.value.replace(/\D/g, '').slice(0, 4) })} />
          </div>
          <Input label="Descrição" required maxLength={2000} value={form.description} onChange={event => setForm({ ...form, description: event.target.value })} />
          <div className="grid gap-4 sm:grid-cols-2">
            <Input label="Valor" required inputMode="numeric" value={form.price} onChange={event => setForm({ ...form, price: formatCurrencyInput(event.target.value) })} />
            <Input label="URL da imagem de capa (opcional)" type="url" maxLength={500} placeholder="https://..." value={form.imageUrl} onChange={event => setForm({ ...form, imageUrl: event.target.value })} />
          </div>
          <div className="rounded-lg border border-dashed border-slate-700 bg-slate-950/40 p-3">
            <label className="flex cursor-pointer items-center justify-between gap-4 text-sm text-slate-200">
              <span><strong>Enviar imagem de capa</strong><span className="mt-0.5 block text-[11px] text-slate-500">JPG, PNG ou WEBP, até 25 MB. A imagem é reduzida com segurança quando necessário.</span></span>
              <span className="inline-flex shrink-0 items-center gap-1.5 rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-xs font-medium text-slate-200"><Upload className="h-3.5 w-3.5" /> Escolher arquivo</span>
              <input className="sr-only" type="file" accept="image/jpeg,image/png,image/webp,.jpg,.jpeg,.png,.webp" onChange={event => handleImageSelection(event.target.files?.[0] ?? null)} />
            </label>
            {imageFile && <div className="mt-3 flex items-center justify-between gap-3 rounded-md bg-slate-900/80 px-3 py-2 text-xs text-emerald-300"><span className="truncate">Arquivo selecionado: {imageFile.name}</span><button type="button" className="shrink-0 text-slate-400 hover:text-white" onClick={() => setImageFile(null)}>Remover</button></div>}
          </div>
          <div className="grid gap-3 rounded-lg border border-slate-800 bg-slate-950/40 p-3 sm:grid-cols-3">
            <label className="flex cursor-pointer items-center gap-2 text-xs text-slate-300"><input type="checkbox" checked={form.isActive} onChange={event => setForm({ ...form, isActive: event.target.checked })} /> Ativo no catálogo</label>
            <label className="flex cursor-pointer items-center gap-2 text-xs text-slate-300"><input type="checkbox" checked={form.isPublicWebsite} onChange={event => setForm({ ...form, isPublicWebsite: event.target.checked })} /> Exibir na landing page</label>
            <label className="flex cursor-pointer items-center gap-2 text-xs text-slate-300"><input type="checkbox" checked={form.isPriceHiddenOnWebsite} onChange={event => setForm({ ...form, isPriceHiddenOnWebsite: event.target.checked })} /> Ocultar preço no site</label>
            <label className="flex cursor-pointer items-center gap-2 text-xs text-slate-300"><input type="checkbox" checked={form.isFeaturedInCarousel} onChange={event => setForm({ ...form, isFeaturedInCarousel: event.target.checked })} /> Destacar no carrossel</label>
          </div>
          <div className="flex flex-col-reverse gap-3 pt-1 sm:flex-row sm:justify-end">
            <Button type="button" variant="outline" onClick={closeModal} disabled={isSaving}>Cancelar</Button>
            <Button type="submit" variant="primary" disabled={isSaving}>{isSaving ? 'Salvando...' : editingProcedure ? 'Salvar alterações' : 'Cadastrar procedimento'}</Button>
          </div>
        </form>
      </Dialog>

      <Dialog isOpen={Boolean(pendingDeactivation)} onClose={() => { if (!isSaving) setPendingDeactivation(null); }} title="Inativar procedimento" maxWidth="max-w-md">
        <div className="space-y-4">
          <p className="text-sm text-slate-300">Deseja inativar <strong className="text-white">{pendingDeactivation?.name}</strong>? Ele deixará de aparecer em novos agendamentos e na landing page, mas os atendimentos, valores e evoluções já registrados permanecerão intactos.</p>
          <div className="flex justify-end gap-2"><Button variant="outline" onClick={() => setPendingDeactivation(null)} disabled={isSaving}>Cancelar</Button><Button variant="danger" onClick={() => void handleDeactivate()} disabled={isSaving}>{isSaving ? 'Inativando...' : 'Inativar procedimento'}</Button></div>
        </div>
      </Dialog>
    </div>
  );
};

// CLIENTS & PRONTUÁRIOS LIST VIEW
export const ClientsView: React.FC<{ clients: Client[]; onSelectClient: (id: string) => void; onRefresh: () => void }> = ({ clients, onSelectClient, onRefresh }) => {
  const { showToast } = useToast();
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingClient, setEditingClient] = useState<Client | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [form, setForm] = useState({
    name: '', email: '', phone: '', cpf: '', rg: '', birthDate: '', profession: '', address: '', city: '', state: 'SP', source: 'Instagram' as any
  });

  const handleOpenCreate = () => {
    setEditingClient(null);
    setForm({ name: '', email: '', phone: '', cpf: '', rg: '', birthDate: '', profession: '', address: '', city: '', state: 'SP', source: 'Instagram' });
    setIsModalOpen(true);
  };

  const handleOpenEdit = (client: Client) => {
    setEditingClient(client);
    setForm({
      name: client.name,
      email: client.email,
      phone: formatPhone(client.phone),
      cpf: formatCpf(client.cpf),
      rg: formatRg(client.rg || ''),
      birthDate: client.birthDate ? client.birthDate.slice(0, 10) : '',
      profession: client.profession || '',
      address: client.address || '',
      city: formatCity(client.city || ''),
      state: client.state || 'SP',
      source: client.source
    });
    setIsModalOpen(true);
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (editingClient) {
      await api.saveClient({ ...editingClient, ...form });
      showToast('Dados do cliente atualizados com sucesso!', 'success');
    } else {
      await api.saveClient(form);
      showToast('Novo cliente cadastrado!', 'success');
    }
    setIsModalOpen(false);
    onRefresh();
  };

  const filteredClients = clients.filter(c =>
    c.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
    c.cpf.includes(searchTerm) ||
    c.phone.includes(searchTerm)
  );

  return (
    <div className="space-y-6">
      <AdminPageHeader title="Gestão de clientes & prontuários" description="Controle completo da pasta do paciente, origem e histórico de evolução." actions={<Button variant="primary" onClick={handleOpenCreate}><Plus className="w-4 h-4" /> Cadastrar cliente</Button>} />

      <Card className="p-4">
        <Input
          placeholder="Buscar por nome do cliente, CPF ou telefone..."
          value={searchTerm}
          onChange={e => setSearchTerm(e.target.value)}
        />
      </Card>

      <Card className="hidden lg:block">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm text-slate-300">
            <thead className="bg-slate-900/80 text-xs uppercase text-slate-400 border-b border-slate-800">
              <tr>
                <th className="p-3">Nome</th>
                <th className="p-3">CPF / RG</th>
                <th className="p-3">Contato</th>
                <th className="p-3">Profissão</th>
                <th className="p-3">Origem</th>
                <th className="p-3">Prontuário</th>
                <th className="p-3 text-right">Ação</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60">
              {filteredClients.map(cli => (
                <tr key={cli.id} className="hover:bg-slate-900/40">
                  <td className="p-3 font-semibold text-white">{cli.name}</td>
                  <td className="p-3 font-mono text-xs text-slate-400">{cli.cpf || 'Sem CPF'} <br/> {cli.rg}</td>
                  <td className="p-3 text-xs">{cli.phone} <br/><span className="text-slate-500">{cli.email}</span></td>
                  <td className="p-3 text-xs text-slate-300">{cli.profession || 'Não informada'}</td>
                  <td className="p-3"><Badge variant="info">{cli.source}</Badge></td>
                  <td className="p-3 font-semibold text-xs text-slate-300">
                    {cli.medicalRecords?.length || 0} Evolução(ões)
                  </td>
                  <td className="p-3 text-right space-x-1">
                    <Button variant="outline" size="sm" onClick={() => handleOpenEdit(cli)}>
                      <Edit className="w-3.5 h-3.5" />
                    </Button>
                    <Button variant="primary" size="sm" onClick={() => onSelectClient(cli.id)}>
                      <Eye className="w-3.5 h-3.5" /> Abrir Pasta
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:hidden gap-4">
        {filteredClients.map(cli => (
          <Card key={cli.id} className="space-y-3 p-4 border-l-4 border-l-amber-500 flex flex-col justify-between">
            <div className="space-y-2">
              <div className="flex items-center justify-between border-b border-slate-800 pb-2">
                <h3 className="font-display font-bold text-white text-base truncate">{cli.name}</h3>
                <Badge variant="info">{cli.source}</Badge>
              </div>
              <div className="space-y-1 text-xs text-slate-300">
                <p className="flex items-center gap-1.5 text-slate-400">
                  <Phone className="w-3.5 h-3.5 text-rose-400 shrink-0" /> {cli.phone}
                </p>
                <p><strong className="text-slate-400">CPF:</strong> {cli.cpf || 'Não informado'}</p>
                <p><strong className="text-slate-400">Profissão:</strong> {cli.profession || 'Não informada'}</p>
              </div>
            </div>

            <div className="pt-2 border-t border-slate-800 space-y-2">
              <div className="text-xs text-slate-400 font-medium">
                {cli.medicalRecords?.length || 0} Evolução(ões) registradas
              </div>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" className="w-1/2" onClick={() => handleOpenEdit(cli)}>
                  <Edit className="w-3.5 h-3.5" /> Editar
                </Button>
                <Button variant="primary" size="sm" className="w-1/2" onClick={() => onSelectClient(cli.id)}>
                  <Eye className="w-3.5 h-3.5" /> Pasta
                </Button>
              </div>
            </div>
          </Card>
        ))}
      </div>

      <Dialog
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        title={editingClient ? "Editar Dados do Cliente" : "Cadastrar Novo Cliente"}
      >
        <form onSubmit={handleSave} className="space-y-4">
          <Input label="Nome Completo" required value={form.name} onChange={e => setForm({...form, name: e.target.value})} />
          <div className="grid grid-cols-2 gap-4">
            <Input label="CPF" placeholder="000.000.000-00" required inputMode="numeric" autoComplete="off" maxLength={14} value={form.cpf} onChange={e => setForm({...form, cpf: formatCpf(e.target.value)})} />
            <Input label="RG" placeholder="00.000.000-0" autoComplete="off" maxLength={12} value={form.rg} onChange={e => setForm({...form, rg: formatRg(e.target.value)})} />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <Input label="Telefone / WhatsApp" placeholder="(00) 00000-0000" required inputMode="tel" autoComplete="tel" maxLength={15} value={form.phone} onChange={e => setForm({...form, phone: formatPhone(e.target.value)})} />
            <Input label="Data de Nascimento" type="date" value={form.birthDate} onChange={e => setForm({...form, birthDate: e.target.value})} />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <Input label="E-mail" type="email" value={form.email} onChange={e => setForm({...form, email: e.target.value})} />
            <Input label="Profissão" value={form.profession} onChange={e => setForm({...form, profession: e.target.value})} />
          </div>
          <Select label="Origem do cadastro" value={form.source} onChange={e => setForm({...form, source: e.target.value as any})}>
            <option value="Instagram">Instagram</option>
            <option value="Indication">Indicação de Amigo</option>
            <option value="GoogleAds">Google Ads / Pesquisa</option>
            <option value="WhatsApp">WhatsApp Direto</option>
            <option value="WalkIn">Passante / Fachada</option>
          </Select>
          <Input label="Endereço" autoComplete="street-address" value={form.address} onChange={e => setForm({...form, address: e.target.value})} />
          <div className="grid grid-cols-[minmax(0,1fr)_6rem] gap-4">
            <Input label="Cidade" placeholder="Ex.: Cuiabá" autoComplete="address-level2" maxLength={100} value={form.city} onChange={e => setForm({...form, city: formatCity(e.target.value)})} />
            <Input label="UF" placeholder="MT" autoComplete="address-level1" maxLength={2} value={form.state} onChange={e => setForm({...form, state: e.target.value.replace(/[^a-z]/gi, '').toUpperCase().slice(0, 2)})} />
          </div>
          <Button type="submit" variant="primary" className="w-full">
            {editingClient ? "Salvar Atualizações" : "Cadastrar Cliente"}
          </Button>
        </form>
      </Dialog>
    </div>
  );
};

// PATIENT PASTA & ADVANCED MEDICAL RECORD WITH PHOTO & DOCUMENT UPLOAD
export const ClientDetailView: React.FC<{ clientId: string; onBack: () => void }> = ({ clientId, onBack }) => {
  const { showToast } = useToast();
  const [client, setClient] = useState<Client | null>(null);
  const [summary, setSummary] = useState<ClientSummary | null>(null);
  const [storageUsage, setStorageUsage] = useState<ClinicalStorageUsage | null>(null);
  const [isRecordModalOpen, setIsRecordModalOpen] = useState(false);
  const [isPhotoModalOpen, setIsPhotoModalOpen] = useState(false);
  const [isDocModalOpen, setIsDocModalOpen] = useState(false);
  const [photoFile, setPhotoFile] = useState<File | null>(null);
  const [documentFile, setDocumentFile] = useState<File | null>(null);
  const [isUploadingPhoto, setIsUploadingPhoto] = useState(false);
  const [isUploadingDocument, setIsUploadingDocument] = useState(false);

  const [newRecord, setNewRecord] = useState({
    procedureName: 'Harmonização Facial & Botox',
    treatedArea: '',
    parametersUsed: '',
    clinicalNotes: '',
    postCareInstructions: ''
  });

  const [photoForm, setPhotoForm] = useState({
    procedureName: 'Harmonização Facial & Botox',
    type: 'Before' as PhotoType,
    isPublicForWebsite: false,
    consentGiven: false
  });

  const [docForm, setDocForm] = useState({
    documentType: 'Termo de Consentimento LGPD'
  });

  const loadClient = () => {
    Promise.all([api.getClientById(clientId), api.getClientSummary(clientId), api.getClientStorageUsage(clientId)]).then(([c, loadedSummary, usage]) => {
      if (c) {
        setClient(c);
        setSummary(loadedSummary);
        setStorageUsage(usage);
      }
    }).catch(error => {
      showToast(error instanceof Error ? error.message : 'Não foi possível carregar o prontuário.', 'error');
    });
  };

  useEffect(() => { loadClient(); }, [clientId]);

  const handleAddMedicalRecord = async (e: React.FormEvent) => {
    e.preventDefault();
    await api.createMedicalRecord(clientId, newRecord);
    showToast('Novo prontuário de sessão registrado!', 'success');
    setIsRecordModalOpen(false);
    setNewRecord({ procedureName: 'Harmonização Facial & Botox', treatedArea: '', parametersUsed: '', clinicalNotes: '', postCareInstructions: '' });
    loadClient();
  };

  const handleAddPhoto = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!photoFile) {
      showToast('Selecione uma imagem antes de continuar.', 'error');
      return;
    }

    setIsUploadingPhoto(true);
    try {
      await api.uploadClientPhoto(clientId, {
        file: photoFile,
        ...photoForm
      });
      showToast('Foto de acompanhamento anexada ao prontuário!', 'success');
      setIsPhotoModalOpen(false);
      setPhotoFile(null);
      setPhotoForm({
        procedureName: 'Harmonização Facial & Botox',
        type: 'Before',
        isPublicForWebsite: false,
        consentGiven: false
      });
      loadClient();
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível enviar a foto.', 'error');
    } finally {
      setIsUploadingPhoto(false);
    }
  };

  const handleAddDoc = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!documentFile) {
      showToast('Selecione um documento antes de continuar.', 'error');
      return;
    }

    setIsUploadingDocument(true);
    try {
      await api.uploadClientDocument(clientId, {
        file: documentFile,
        documentType: docForm.documentType
      });
      showToast('Documento anexado à pasta clínica!', 'success');
      setIsDocModalOpen(false);
      setDocumentFile(null);
      setDocForm({ documentType: 'Termo de Consentimento LGPD' });
      loadClient();
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível enviar o documento.', 'error');
    } finally {
      setIsUploadingDocument(false);
    }
  };

  const handleClosePhotoModal = () => {
    if (isUploadingPhoto) return;
    setPhotoFile(null);
    setIsPhotoModalOpen(false);
  };

  const handleCloseDocumentModal = () => {
    if (isUploadingDocument) return;
    setDocumentFile(null);
    setIsDocModalOpen(false);
  };

  if (!client) return null;

  const age = client.birthDate ? new Date().getFullYear() - new Date(client.birthDate).getFullYear() : null;
  const formatBytes = (bytes: number) => {
    if (bytes < 1024 * 1024) return `${Math.max(0, bytes / 1024).toFixed(1)} KB`;
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
    return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`;
  };
  const formatCurrency = (value: number) => value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  const formatDateTime = (value?: string) => value
    ? new Date(value).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })
    : 'Nenhum registro';

  return (
    <div className="space-y-6">
      <AdminPageHeader
        title={`Prontuário & pasta completa: ${client.name}`}
        description="Acompanhe dados cadastrais, evoluções, formulários e arquivos deste paciente."
        actions={<>
          <Button variant="ghost" size="sm" onClick={onBack}>← Voltar à lista</Button>
          <Button variant="outline" size="sm" onClick={() => setIsPhotoModalOpen(true)}>
            <Camera className="w-4 h-4" /> Anexar Foto
          </Button>
          <Button variant="outline" size="sm" onClick={() => setIsDocModalOpen(true)}>
            <FileUp className="w-4 h-4" /> Anexar Documento
          </Button>
          <Button variant="primary" size="sm" onClick={() => setIsRecordModalOpen(true)}>
            <Stethoscope className="w-4 h-4" /> Nova Evolução
          </Button>
        </>}
      />

      {storageUsage && (
        <Card className="space-y-3" aria-label="Uso do armazenamento clínico">
          <div className="flex flex-wrap items-center justify-between gap-2 text-xs">
            <span className="font-semibold text-slate-200">Armazenamento deste paciente</span>
            <span className="text-slate-400">
              {formatBytes(storageUsage.clientUsedBytes)} de {formatBytes(storageUsage.clientLimitBytes)}
            </span>
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-slate-800">
            <div
              className="h-full rounded-full bg-rose-500 transition-[width] duration-300"
              style={{ width: `${Math.min(100, storageUsage.clientUsedBytes / storageUsage.clientLimitBytes * 100)}%` }}
            />
          </div>
          <p className="text-[11px] text-slate-500">
            Clínica: {formatBytes(storageUsage.installationUsedBytes)} de {formatBytes(storageUsage.installationLimitBytes)}. Fotos são normalizadas e comprimidas automaticamente.
          </p>
        </Card>
      )}

      <div className="grid md:grid-cols-3 gap-6">
        <Card className="space-y-3">
          <h3 className="font-display font-semibold text-rose-400 border-b border-slate-800 pb-2 flex items-center justify-between">
            <span>Dados Demográficos</span>
            {age && <Badge variant="info">{age} anos</Badge>}
          </h3>
          <p className="text-xs text-slate-300"><strong className="text-slate-400">CPF:</strong> {client.cpf || 'Não informado'}</p>
          <p className="text-xs text-slate-300"><strong className="text-slate-400">RG:</strong> {client.rg || 'Não informado'}</p>
          <p className="text-xs text-slate-300"><strong className="text-slate-400">WhatsApp:</strong> {client.phone}</p>
          <p className="text-xs text-slate-300"><strong className="text-slate-400">Email:</strong> {client.email}</p>
          <p className="text-xs text-slate-300"><strong className="text-slate-400">Profissão:</strong> {client.profession || 'Não informada'}</p>
          <p className="text-xs text-slate-300"><strong className="text-slate-400">Cidade/UF:</strong> {client.city || 'São Paulo'} - {client.state || 'SP'}</p>
          <p className="text-xs text-slate-300"><strong className="text-slate-400">Origem:</strong> {client.source}</p>
        </Card>

        <Card className="md:col-span-2 space-y-4">
          <div className="flex flex-wrap items-start justify-between gap-3 border-b border-slate-800 pb-4">
            <div>
              <h3 className="font-display font-semibold text-rose-400 flex items-center gap-2"><LayoutDashboard className="w-4 h-4" /> Resumo do Paciente</h3>
              <p className="mt-1 text-[11px] text-slate-500">Indicadores clínicos, operacionais, documentais e financeiros deste cadastro.</p>
            </div>
            {summary?.nextAppointmentAt && <Badge variant="info"><CalendarIcon className="mr-1 h-3 w-3" /> Próximo: {formatDateTime(summary.nextAppointmentAt)}</Badge>}
          </div>

          {!summary ? <SkeletonLoader className="h-44 w-full" /> : <>
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <div className="rounded-xl border border-slate-800 bg-slate-950/50 p-3"><span className="text-[10px] font-semibold uppercase text-slate-500">Atendimentos</span><p className="mt-1 font-display text-2xl font-bold text-white">{summary.totalAppointments}</p><p className="text-[11px] text-slate-400">{summary.completedAppointments} concluído(s) • {summary.upcomingAppointments} futuro(s)</p></div>
              <div className="rounded-xl border border-slate-800 bg-slate-950/50 p-3"><span className="text-[10px] font-semibold uppercase text-slate-500">Procedimentos realizados</span><p className="mt-1 font-display text-2xl font-bold text-white">{summary.clinicalRecordsCount}</p><p className="text-[11px] text-slate-400">Última evolução: {formatDateTime(summary.lastClinicalRecordAt)}</p></div>
              <div className="rounded-xl border border-slate-800 bg-slate-950/50 p-3"><span className="text-[10px] font-semibold uppercase text-slate-500">Arquivos e formulários</span><p className="mt-1 font-display text-2xl font-bold text-white">{summary.documentsCount + summary.photosCount}</p><p className="text-[11px] text-slate-400">{summary.documentsCount} documento(s) • {summary.photosCount} foto(s)</p></div>
              <div className="rounded-xl border border-emerald-500/20 bg-emerald-500/5 p-3"><span className="text-[10px] font-semibold uppercase text-emerald-300/70">Financeiro</span><p className="mt-1 font-display text-2xl font-bold text-emerald-300">{formatCurrency(summary.totalPaid)}</p><p className="text-[11px] text-emerald-100/70">Recebido • saldo: {formatCurrency(summary.outstandingBalance)}</p></div>
            </div>

            <div className="grid gap-4 rounded-xl border border-slate-800 bg-slate-950/40 p-3 sm:grid-cols-2">
              <div><h4 className="text-xs font-semibold text-slate-200">Procedimentos mais realizados</h4>{summary.mostPerformedProcedures.length ? <div className="mt-2 space-y-1.5">{summary.mostPerformedProcedures.map(item => <div key={item.procedureName} className="flex items-center justify-between text-xs text-slate-400"><span className="truncate pr-3">{item.procedureName}</span><Badge variant="info">{item.count}x</Badge></div>)}</div> : <p className="mt-2 text-xs text-slate-500">Nenhuma evolução clínica registrada.</p>}</div>
              <div><h4 className="text-xs font-semibold text-slate-200">Formulários digitais</h4><div className="mt-2 flex flex-wrap gap-2"><Badge variant="warning">{summary.draftFormsCount} rascunho(s)</Badge><Badge variant="success">{summary.finalizedFormsCount} finalizado(s)</Badge><Badge variant="info">{summary.signedFormsCount} assinado(s)</Badge></div><p className="mt-3 text-[11px] text-slate-500">Contratado: {formatCurrency(summary.totalContracted)} • Pagamentos de agendamentos não cancelados.</p></div>
            </div>
          </>}
        </Card>
      </div>

      <ClientAppointmentHistoryPanel clientId={clientId} onChanged={loadClient} />

      <ClientFormsPanel clientId={clientId} clientName={client.name} />

      <ClientTermsPanel clientId={clientId} clientName={client.name} />

      {/* GALERIA DE FOTOS & ANEXOS DE DOCUMENTOS */}
      <div className="grid md:grid-cols-2 gap-6">
        {/* FOTOS DE ACOMPANHAMENTO */}
        <Card className="space-y-4">
          <div className="flex items-center justify-between border-b border-slate-800 pb-3">
            <h3 className="font-display font-semibold text-rose-400 flex items-center gap-2">
              <Camera className="w-4 h-4" /> Fotos de Acompanhamento (Antes/Depois)
            </h3>
            <Button variant="outline" size="sm" onClick={() => setIsPhotoModalOpen(true)}>
              <Plus className="w-3.5 h-3.5" /> Anexar Foto
            </Button>
          </div>

          {!client.photos || client.photos.length === 0 ? (
            <p className="text-xs text-slate-500 py-4 text-center">Nenhuma foto de acompanhamento cadastrada.</p>
          ) : (
            <div className="grid grid-cols-2 gap-3">
              {client.photos.map(p => (
                <div key={p.id} className="relative rounded-lg overflow-hidden border border-slate-800 group">
                  <img src={p.contentUrl || p.filePath} alt={p.procedureName} className="w-full h-32 object-cover" />
                  <div className="absolute inset-0 bg-gradient-to-t from-slate-950 via-transparent p-2 flex flex-col justify-between">
                    <Badge variant={p.type === 'Before' ? 'warning' : 'success'}>
                      {p.type === 'Before' ? 'Antes' : p.type === 'After' ? 'Depois' : 'Evolução'}
                    </Badge>
                    <div>
                      <div className="text-[10px] text-white font-medium truncate">{p.procedureName}</div>
                      <div className="text-[9px] text-slate-300">{p.width}×{p.height} • {formatBytes(p.fileSizeBytes)}</div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </Card>

        {/* DOCUMENTOS E TERMOS PDF */}
        <Card className="space-y-4">
          <div className="flex items-center justify-between border-b border-slate-800 pb-3">
            <h3 className="font-display font-semibold text-rose-400 flex items-center gap-2">
              <FileUp className="w-4 h-4" /> Documentos & Termos LGPD
            </h3>
            <Button variant="outline" size="sm" onClick={() => setIsDocModalOpen(true)}>
              <Plus className="w-3.5 h-3.5" /> Anexar Termo
            </Button>
          </div>

          {!client.documents || client.documents.length === 0 ? (
            <p className="text-xs text-slate-500 py-4 text-center">Nenhum documento anexado.</p>
          ) : (
            <div className="space-y-2">
              {client.documents.map(d => (
                <div key={d.id} className="p-3 bg-slate-900 rounded-lg border border-slate-800 flex items-center justify-between text-xs">
                  <div>
                    <div className="font-semibold text-white">{d.fileName}</div>
                    <div className="text-[10px] text-slate-400">{d.documentType} • {formatBytes(d.fileSizeBytes)} • {new Date(d.uploadedAt).toLocaleDateString('pt-BR')}</div>
                  </div>
                  {d.contentUrl && (
                    <a
                      href={d.contentUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="inline-flex items-center justify-center rounded-lg border border-slate-700 bg-slate-900/60 px-3 py-1.5 text-xs font-medium text-slate-200 transition-colors hover:border-slate-500 hover:bg-slate-800 focus:outline-none focus-visible:ring-2 focus-visible:ring-rose-500/60"
                    >
                      Abrir / baixar
                    </a>
                  )}
                </div>
              ))}
            </div>
          )}
        </Card>
      </div>

      {/* HISTÓRICO DE PRONTUÁRIOS */}
      <Card className="space-y-4">
        <div className="flex items-center justify-between border-b border-slate-800 pb-4">
          <h3 className="font-display text-lg font-bold text-white flex items-center gap-2">
            <Stethoscope className="w-5 h-5 text-rose-500" /> Histórico de Prontuários por Atendimento (Evolução Clínica)
          </h3>
          <Button variant="primary" size="sm" onClick={() => setIsRecordModalOpen(true)}>
            <Plus className="w-4 h-4" /> Nova Evolução
          </Button>
        </div>

        {client.medicalRecords?.length === 0 ? (
          <p className="text-xs text-slate-500 py-4 text-center">Nenhum prontuário de sessão registrado ainda.</p>
        ) : (
          <div className="space-y-4">
            {client.medicalRecords?.map(rec => (
              <div key={rec.id} className="p-4 bg-slate-900/90 rounded-xl border border-slate-800 space-y-2">
                <div className="flex items-center justify-between">
                  <h4 className="font-display font-bold text-rose-400 text-sm">{rec.procedureName}</h4>
                  <span className="text-xs text-slate-400 font-mono">
                    {new Date(rec.sessionDate).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })}
                  </span>
                </div>
                <p className="text-xs text-slate-300"><strong className="text-slate-400">Áreas Tratadas:</strong> {rec.treatedArea}</p>
                <p className="text-xs text-slate-300"><strong className="text-slate-400">Parâmetros / Produtos Utilizados:</strong> {rec.parametersUsed}</p>
                <p className="text-xs text-slate-300"><strong className="text-slate-400">Evolução / Observações Médicas:</strong> {rec.clinicalNotes}</p>
                {rec.postCareInstructions && (
                  <p className="text-xs text-amber-300"><strong className="text-amber-400">Recomendações Pós:</strong> {rec.postCareInstructions}</p>
                )}
              </div>
            ))}
          </div>
        )}
      </Card>

      {/* NEW MEDICAL RECORD MODAL */}
      <Dialog isOpen={isRecordModalOpen} onClose={() => setIsRecordModalOpen(false)} title="Novo Prontuário de Atendimento (Evolução Clínica)">
        <form onSubmit={handleAddMedicalRecord} className="space-y-4">
          <Input
            label="Procedimento Realizado"
            required
            value={newRecord.procedureName}
            onChange={e => setNewRecord({...newRecord, procedureName: e.target.value})}
          />
          <Input
            label="Áreas Tratadas (ex: Terço superior da face, glúteo)"
            required
            value={newRecord.treatedArea}
            onChange={e => setNewRecord({...newRecord, treatedArea: e.target.value})}
          />
          <Input
            label="Parâmetros Utilizados / Produtos Aplicados"
            required
            value={newRecord.parametersUsed}
            onChange={e => setNewRecord({...newRecord, parametersUsed: e.target.value})}
          />
          <div className="space-y-1.5">
            <label className="block text-xs font-medium text-slate-300">Evolução Clínica / Reação da Pele</label>
            <textarea
              className="w-full px-3.5 py-2 rounded-lg bg-slate-900/90 border border-slate-800 text-slate-100 text-sm"
              rows={3}
              value={newRecord.clinicalNotes}
              onChange={e => setNewRecord({...newRecord, clinicalNotes: e.target.value})}
            />
          </div>
          <Input
            label="Recomendações Pós-Procedimento"
            value={newRecord.postCareInstructions}
            onChange={e => setNewRecord({...newRecord, postCareInstructions: e.target.value})}
          />
          <Button type="submit" variant="primary" className="w-full">Registrar Prontuário da Sessão</Button>
        </form>
      </Dialog>

      {/* ADD PHOTO MODAL */}
      <Dialog isOpen={isPhotoModalOpen} onClose={handleClosePhotoModal} title="Anexar Foto de Acompanhamento (LGPD)">
        <form onSubmit={handleAddPhoto} className="space-y-4">
          <Input
            type="file"
            label="Arquivo da Foto"
            accept=".jpg,.jpeg,.png,.webp,image/jpeg,image/png,image/webp"
            required
            onChange={e => setPhotoFile(e.target.files?.[0] ?? null)}
          />
          <p className="text-[11px] text-slate-500">
            JPG, PNG ou WEBP, até 25 MB. A imagem será orientada, reduzida para até 2560 px e salva sem metadados pessoais.{photoFile ? ` Selecionado: ${photoFile.name} (${(photoFile.size / 1024 / 1024).toFixed(2)} MB).` : ''}
          </p>
          <Input
            label="Procedimento Relacionado"
            required
            value={photoForm.procedureName}
            onChange={e => setPhotoForm({...photoForm, procedureName: e.target.value})}
          />
          <Select
            label="Tipo de Foto"
            value={photoForm.type}
            onChange={e => setPhotoForm({...photoForm, type: e.target.value as PhotoType})}
          >
            <option value="Before">Foto de Antes (Pré-Procedimento)</option>
            <option value="After">Foto de Depois (Resultado)</option>
            <option value="Progress">Acompanhamento / Evolução</option>
          </Select>
          <label className="flex items-center gap-2 text-xs text-slate-300 pt-2">
            <input
              type="checkbox"
              checked={photoForm.isPublicForWebsite && photoForm.consentGiven}
              onChange={e => setPhotoForm({
                ...photoForm,
                isPublicForWebsite: e.target.checked,
                consentGiven: e.target.checked
              })}
            />
            Paciente autorizou uso da foto na galeria pública do site (LGPD)
          </label>
          <Button type="submit" variant="primary" className="w-full" disabled={isUploadingPhoto}>
            {isUploadingPhoto ? 'Enviando foto...' : 'Anexar Foto ao Prontuário'}
          </Button>
        </form>
      </Dialog>

      {/* ADD DOCUMENT MODAL */}
      <Dialog isOpen={isDocModalOpen} onClose={handleCloseDocumentModal} title="Anexar Documento / Termo LGPD">
        <form onSubmit={handleAddDoc} className="space-y-4">
          <Input
            type="file"
            label="Arquivo"
            accept=".pdf,.docx,.xlsx,.csv,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/csv"
            required
            onChange={e => setDocumentFile(e.target.files?.[0] ?? null)}
          />
          <p className="text-[11px] text-slate-500">
            PDF, DOCX, XLSX ou CSV, até 15 MB.{documentFile ? ` Selecionado: ${documentFile.name} (${(documentFile.size / 1024 / 1024).toFixed(2)} MB).` : ''}
          </p>
          <Select
            label="Tipo de Documento"
            value={docForm.documentType}
            onChange={e => setDocForm({...docForm, documentType: e.target.value})}
          >
            <option value="Termo de Consentimento LGPD">Termo de Consentimento LGPD</option>
            <option value="Exames de Sangue / Laboratório">Exames de Sangue / Laboratório</option>
            <option value="Laudo Médico / Avaliação">Laudo Médico / Avaliação</option>
            <option value="Contrato de Prestação de Serviços">Contrato de Prestação de Serviços</option>
          </Select>
          <Button type="submit" variant="primary" className="w-full" disabled={isUploadingDocument}>
            {isUploadingDocument ? 'Enviando documento...' : 'Anexar Documento à Pasta'}
          </Button>
        </form>
      </Dialog>
    </div>
  );
};
