import React, { useEffect, useState } from 'react';
import { CalendarClock, Edit, Loader2 } from 'lucide-react';
import { Appointment, AppointmentStatus, PaymentMethod, ProcedureType } from '../../types';
import { api } from '../../services/api';
import { currencyInputFromNumber, formatCurrencyInput, parseCurrencyInput } from '../../utils/inputMasks';
import { clinicDateTimeInputValue, formatClinicDateTime } from '../../utils/clinicDateTime';
import { Badge, Button, Card, Dialog, Input, Select } from '../ui/Components';
import { useToast } from '../../contexts/ToastContext';

const statusOptions: Array<{ value: AppointmentStatus; label: string }> = [
  { value: 'Scheduled', label: 'Agendado' },
  { value: 'Confirmed', label: 'Confirmado' },
  { value: 'InProgress', label: 'Atendimento' },
  { value: 'Completed', label: 'Concluído' },
  { value: 'Cancelled', label: 'Cancelado' },
  { value: 'NoShow', label: 'Faltou' },
];

const statusBadge = (status: AppointmentStatus): 'success' | 'warning' | 'danger' | 'info' => {
  if (status === 'Completed' || status === 'Confirmed') return 'success';
  if (status === 'Cancelled' || status === 'NoShow') return 'danger';
  if (status === 'InProgress') return 'info';
  return 'warning';
};

const statusLabel = (status: AppointmentStatus) => statusOptions.find(option => option.value === status)?.label ?? status;

type EditForm = {
  procedureTypeId: string;
  scheduledDateTime: string;
  status: AppointmentStatus;
  totalPrice: string;
  amountPaid: string;
  paymentMethod: PaymentMethod;
  professionalName: string;
  notes: string;
};

const formFromAppointment = (appointment: Appointment): EditForm => ({
  procedureTypeId: appointment.procedureTypeId,
  scheduledDateTime: clinicDateTimeInputValue(appointment.scheduledDateTime),
  status: appointment.status,
  totalPrice: currencyInputFromNumber(appointment.totalPrice),
  amountPaid: currencyInputFromNumber(appointment.amountPaid),
  paymentMethod: appointment.paymentMethod,
  professionalName: appointment.professionalName || 'Dra. Mariana Siqueira',
  notes: appointment.notes || '',
});

export const ClientAppointmentHistoryPanel: React.FC<{ clientId: string; onChanged: () => void }> = ({ clientId, onChanged }) => {
  const { showToast } = useToast();
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [procedures, setProcedures] = useState<ProcedureType[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [editing, setEditing] = useState<Appointment | null>(null);
  const [form, setForm] = useState<EditForm | null>(null);

  const load = async () => {
    setIsLoading(true);
    try {
      const [allAppointments, availableProcedures] = await Promise.all([
        api.getAppointments(),
        api.getProcedures().catch(() => []),
      ]);
      setAppointments(allAppointments.filter(appointment => appointment.clientId === clientId));
      setProcedures(availableProcedures);
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível carregar o histórico de atendimentos.', 'error');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => { void load(); }, [clientId]);

  const openEdit = (appointment: Appointment) => {
    setEditing(appointment);
    setForm(formFromAppointment(appointment));
  };

  const changeStatus = async (appointment: Appointment, status: AppointmentStatus) => {
    if (status === appointment.status) return;
    try {
      await api.updateAppointmentStatus(appointment.id, status);
      showToast(`Atendimento marcado como ${statusLabel(status).toLowerCase()}.`, 'success');
      await load();
      onChanged();
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível alterar o status.', 'error');
    }
  };

  const saveEdit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!editing || !form) return;
    setIsSaving(true);
    try {
      await api.saveAppointment({
        ...editing,
        procedureTypeId: form.procedureTypeId,
        scheduledDateTime: form.scheduledDateTime,
        status: form.status,
        totalPrice: parseCurrencyInput(form.totalPrice),
        amountPaid: parseCurrencyInput(form.amountPaid),
        paymentMethod: form.paymentMethod,
        professionalName: form.professionalName,
        notes: form.notes,
      });
      showToast('Atendimento atualizado com sucesso.', 'success');
      setEditing(null);
      setForm(null);
      await load();
      onChanged();
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível atualizar o atendimento.', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  const currentTime = Date.now();
  const activeAppointments = appointments
    .filter(appointment => appointment.status === 'InProgress' || (new Date(appointment.scheduledDateTime).getTime() >= currentTime && ['Scheduled', 'Confirmed'].includes(appointment.status)))
    .sort((a, b) => new Date(a.scheduledDateTime).getTime() - new Date(b.scheduledDateTime).getTime());
  const historicalAppointments = appointments
    .filter(appointment => !activeAppointments.some(active => active.id === appointment.id))
    .sort((a, b) => new Date(b.scheduledDateTime).getTime() - new Date(a.scheduledDateTime).getTime());

  const renderAppointment = (appointment: Appointment) => (
    <article key={appointment.id} className="grid gap-3 rounded-xl border border-slate-800 bg-slate-950/50 p-3 sm:grid-cols-[minmax(0,1fr)_11rem_9rem] sm:items-center">
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2">
          <strong className="truncate text-sm text-white">{appointment.procedureName}</strong>
          <Badge variant={statusBadge(appointment.status)}>{statusLabel(appointment.status)}</Badge>
        </div>
        <p className="mt-1 text-xs text-slate-400">{formatClinicDateTime(appointment.scheduledDateTime)} · {appointment.professionalName || 'Profissional não informado'}</p>
        <p className="mt-1 text-[11px] text-slate-500">R$ {appointment.amountPaid.toFixed(2)} pago de R$ {appointment.totalPrice.toFixed(2)} · {appointment.paymentStatus === 'Paid' ? 'Quitado' : appointment.paymentStatus === 'Partial' ? 'Pagamento parcial' : 'Pendente'}</p>
      </div>
      <Select aria-label={`Status de ${appointment.procedureName}`} value={appointment.status} onChange={event => void changeStatus(appointment, event.target.value as AppointmentStatus)} className="py-1.5 text-xs" >
        {statusOptions.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
      </Select>
      <Button variant="outline" size="sm" className="w-full" onClick={() => openEdit(appointment)}>
        <Edit className="h-3.5 w-3.5" /> Editar
      </Button>
    </article>
  );

  return <>
    <Card className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-800 pb-3">
        <div>
          <h3 className="flex items-center gap-2 font-display font-semibold text-rose-400"><CalendarClock className="h-4 w-4" /> Histórico de atendimentos</h3>
          <p className="mt-1 text-[11px] text-slate-500">Agenda completa deste cliente, incluindo atendimentos futuros, realizados, cancelados e faltas.</p>
        </div>
        <Badge variant="info">{appointments.length} atendimento(s)</Badge>
      </div>

      {isLoading ? <div className="flex items-center justify-center gap-2 py-8 text-xs text-slate-500"><Loader2 className="h-4 w-4 animate-spin" /> Carregando atendimentos...</div> : appointments.length === 0 ? <p className="py-5 text-center text-xs text-slate-500">Nenhum atendimento vinculado a este cliente.</p> : <div className="max-h-[32rem] space-y-5 overflow-y-auto overscroll-contain pr-1 sm:max-h-[38rem]">
        {activeAppointments.length > 0 && <section className="space-y-2"><h4 className="text-xs font-semibold uppercase tracking-wide text-slate-400">Próximos e em andamento</h4><div className="space-y-2">{activeAppointments.map(renderAppointment)}</div></section>}
        {historicalAppointments.length > 0 && <section className="space-y-2"><h4 className="text-xs font-semibold uppercase tracking-wide text-slate-400">Histórico</h4><div className="space-y-2">{historicalAppointments.map(renderAppointment)}</div></section>}
      </div>}
    </Card>

    <Dialog isOpen={Boolean(editing && form)} onClose={() => { if (!isSaving) { setEditing(null); setForm(null); } }} title="Editar atendimento" maxWidth="max-w-2xl">
      {form && <form onSubmit={saveEdit} className="space-y-4">
        <div className="rounded-lg border border-slate-800 bg-slate-950/40 px-3 py-2 text-xs text-slate-400">Cliente: <strong className="text-white">{editing?.clientName}</strong></div>
        <Select label="Procedimento" required value={form.procedureTypeId} onChange={event => setForm({ ...form, procedureTypeId: event.target.value })}>
          {!procedures.some(procedure => procedure.id === form.procedureTypeId) && <option value={form.procedureTypeId}>{editing?.procedureName || 'Procedimento atual'}</option>}
          {procedures.filter(procedure => procedure.isActive || procedure.id === form.procedureTypeId).map(procedure => <option key={procedure.id} value={procedure.id}>{procedure.name}</option>)}
        </Select>
        <div className="grid gap-4 sm:grid-cols-2">
          <Input label="Data e hora" type="datetime-local" required value={form.scheduledDateTime} onChange={event => setForm({ ...form, scheduledDateTime: event.target.value })} />
          <Input label="Profissional responsável" value={form.professionalName} onChange={event => setForm({ ...form, professionalName: event.target.value })} />
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <Select label="Status do atendimento" value={form.status} onChange={event => setForm({ ...form, status: event.target.value as AppointmentStatus })}>{statusOptions.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}</Select>
          <Select label="Forma de pagamento" value={form.paymentMethod} onChange={event => setForm({ ...form, paymentMethod: event.target.value as PaymentMethod })}><option value="Pix">PIX</option><option value="CreditCard">Cartão de crédito</option><option value="DebitCard">Cartão de débito</option><option value="Cash">Dinheiro</option><option value="PackageSession">Pacote / sessão pré-paga</option></Select>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <Input label="Valor total (R$)" inputMode="numeric" value={form.totalPrice} onChange={event => setForm({ ...form, totalPrice: formatCurrencyInput(event.target.value) })} />
          <Input label="Valor pago (R$)" inputMode="numeric" value={form.amountPaid} onChange={event => setForm({ ...form, amountPaid: formatCurrencyInput(event.target.value) })} />
        </div>
        <Input label="Observações" value={form.notes} onChange={event => setForm({ ...form, notes: event.target.value })} />
        <div className="flex justify-end gap-2"><Button type="button" variant="outline" onClick={() => { setEditing(null); setForm(null); }} disabled={isSaving}>Cancelar</Button><Button type="submit" disabled={isSaving}>{isSaving ? 'Salvando...' : 'Salvar alterações'}</Button></div>
      </form>}
    </Dialog>
  </>;
};
