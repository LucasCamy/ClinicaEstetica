import React, { useEffect, useMemo, useState } from 'react';
import { AlertCircle, ArrowDownLeft, ArrowUpRight, CheckCircle, Clock, Edit, Plus, Trash2 } from 'lucide-react';
import { FinanceOverview, FinancialEntry, FinancialEntryStatus, FinancialEntryType, FinancialLedgerItem, FinancialLedgerSource, OutstandingReceivable, PaymentMethod } from '../../types';
import { api } from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';
import { useToast } from '../../contexts/ToastContext';
import { currencyInputFromNumber, formatCurrencyInput, parseCurrencyInput } from '../../utils/inputMasks';
import { formatClinicDate } from '../../utils/clinicDateTime';
import { Badge, Button, Card, Dialog, Input, Select, SkeletonLoader } from '../ui/Components';
import { AdminPageHeader } from './AdminPageHeader';

type EntryForm = {
  type: FinancialEntryType;
  status: Exclude<FinancialEntryStatus, 'Cancelled'>;
  category: string;
  description: string;
  amount: string;
  paymentMethod: PaymentMethod;
  effectiveDate: string;
  notes: string;
};

const paymentMethodLabels: Record<PaymentMethod, string> = {
  Pix: 'PIX',
  CreditCard: 'Cartão de crédito',
  DebitCard: 'Cartão de débito',
  Cash: 'Dinheiro',
  PackageSession: 'Pacote / sessão pré-paga',
};

const entryStatusLabels: Record<FinancialEntryStatus, string> = {
  Planned: 'Previsto',
  Settled: 'Realizado',
  Cancelled: 'Cancelado',
};

const entryTypeLabels: Record<FinancialEntryType, string> = {
  Income: 'Entrada',
  Expense: 'Saída',
};

const ledgerSourceLabels: Record<FinancialLedgerSource, string> = {
  AppointmentReceipt: 'Atendimento',
  ManualEntry: 'Manual',
};

const todayValue = () => {
  const now = new Date();
  return new Date(now.getTime() - now.getTimezoneOffset() * 60_000).toISOString().slice(0, 10);
};

const monthRange = (value: string) => {
  if (!/^\d{4}-\d{2}$/.test(value)) return monthRange(todayValue().slice(0, 7));
  const [year, month] = value.split('-').map(Number);
  const lastDay = new Date(year, month, 0).getDate();
  return { from: `${value}-01`, to: `${value}-${String(lastDay).padStart(2, '0')}` };
};

const formFromEntry = (entry?: FinancialEntry): EntryForm => ({
  type: entry?.type ?? 'Expense',
  status: entry?.status === 'Planned' ? 'Planned' : 'Settled',
  category: entry?.category ?? 'Materiais e insumos',
  description: entry?.description ?? '',
  amount: entry ? currencyInputFromNumber(entry.amount) : currencyInputFromNumber(0),
  paymentMethod: entry?.paymentMethod ?? 'Pix',
  effectiveDate: entry?.effectiveDate ?? todayValue(),
  notes: entry?.notes ?? '',
});

const formatCurrency = (value: number) => value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
const formatDate = (value: string) => new Date(`${value.slice(0, 10)}T12:00:00`).toLocaleDateString('pt-BR');
const statusBadge = (status: FinancialEntryStatus): 'success' | 'warning' | 'danger' => status === 'Settled' ? 'success' : status === 'Planned' ? 'warning' : 'danger';

export const FinanceView: React.FC = () => {
  const { can } = useAuth();
  const { showToast } = useToast();
  const [month, setMonth] = useState(() => todayValue().slice(0, 7));
  const [overview, setOverview] = useState<FinanceOverview | null>(null);
  const [ledgerItems, setLedgerItems] = useState<FinancialLedgerItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [search, setSearch] = useState('');
  const [sourceFilter, setSourceFilter] = useState<FinancialLedgerSource | ''>('');
  const [typeFilter, setTypeFilter] = useState<FinancialEntryType | ''>('');
  const [statusFilter, setStatusFilter] = useState<FinancialEntryStatus | ''>('');
  const [categoryFilter, setCategoryFilter] = useState('');
  const [isEntryDialogOpen, setIsEntryDialogOpen] = useState(false);
  const [editingEntry, setEditingEntry] = useState<FinancialEntry | null>(null);
  const [entryForm, setEntryForm] = useState<EntryForm>(() => formFromEntry());
  const [receivableToUpdate, setReceivableToUpdate] = useState<OutstandingReceivable | null>(null);
  const [paymentAmount, setPaymentAmount] = useState(currencyInputFromNumber(0));
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>('Pix');
  const [entryToCancel, setEntryToCancel] = useState<FinancialEntry | null>(null);
  const canManage = can('finance.manage');
  const { from, to } = monthRange(month);

  const load = async () => {
    setIsLoading(true);
    try {
      const [loadedOverview, loadedLedgerItems] = await Promise.all([
        api.getFinanceOverview(from, to),
        api.getFinancialLedger({
          from,
          to,
          source: sourceFilter || undefined,
          type: typeFilter || undefined,
          status: statusFilter || undefined,
          category: categoryFilter || undefined,
        }),
      ]);
      setOverview(loadedOverview);
      setLedgerItems(loadedLedgerItems);
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível carregar os dados financeiros.', 'error');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => { void load(); }, [from, to, sourceFilter, typeFilter, statusFilter, categoryFilter]);

  const filteredLedgerItems = useMemo(() => {
    const normalized = search.trim().toLowerCase();
    if (!normalized) return ledgerItems;
    return ledgerItems.filter(item => `${item.category} ${item.description} ${item.clientName ?? ''} ${item.procedureName ?? ''}`.toLowerCase().includes(normalized));
  }, [ledgerItems, search]);

  const ledgerCategories = useMemo(
    () => Array.from(new Set(['Atendimentos', ...ledgerItems.map(item => item.category)])).sort((left, right) => left.localeCompare(right, 'pt-BR')),
    [ledgerItems],
  );

  const flowMax = Math.max(1, ...(overview?.cashFlow.flatMap(day => [day.income, day.expense]) ?? [0]));
  const visibleFlow = overview?.cashFlow.slice(-14) ?? [];
  const categoryMax = Math.max(1, ...(overview?.expenseCategories.map(category => category.amount) ?? [0]));

  const openCreate = () => {
    setEditingEntry(null);
    setEntryForm(formFromEntry());
    setIsEntryDialogOpen(true);
  };

  const openEdit = (entry: FinancialEntry) => {
    setEditingEntry(entry);
    setEntryForm(formFromEntry(entry));
    setIsEntryDialogOpen(true);
  };

  const saveEntry = async (event: React.FormEvent) => {
    event.preventDefault();
    setIsSaving(true);
    try {
      await api.saveFinancialEntry({
        id: editingEntry?.id,
        type: entryForm.type,
        status: entryForm.status,
        category: entryForm.category,
        description: entryForm.description,
        amount: parseCurrencyInput(entryForm.amount),
        paymentMethod: entryForm.paymentMethod,
        effectiveDate: entryForm.effectiveDate,
        notes: entryForm.notes,
      });
      showToast(editingEntry ? 'Lançamento financeiro atualizado.' : 'Lançamento financeiro registrado.', 'success');
      setIsEntryDialogOpen(false);
      await load();
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível salvar o lançamento.', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  const settleEntry = async (entry: FinancialEntry) => {
    setIsSaving(true);
    try {
      await api.settleFinancialEntry(entry.id, todayValue());
      showToast('Lançamento marcado como realizado.', 'success');
      await load();
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível confirmar o lançamento.', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  const cancelEntry = async () => {
    if (!entryToCancel) return;
    setIsSaving(true);
    try {
      await api.cancelFinancialEntry(entryToCancel.id);
      showToast('Lançamento cancelado e preservado na auditoria.', 'success');
      setEntryToCancel(null);
      await load();
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível cancelar o lançamento.', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  const openReceivable = (item: OutstandingReceivable) => {
    setReceivableToUpdate(item);
    setPaymentAmount(currencyInputFromNumber(item.amountPaid));
    setPaymentMethod(item.paymentMethod);
  };

  const savePayment = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!receivableToUpdate) return;
    setIsSaving(true);
    try {
      await api.updateAppointmentStatus(
        receivableToUpdate.appointmentId,
        receivableToUpdate.status,
        parseCurrencyInput(paymentAmount),
        paymentMethod,
      );
      showToast('Valor recebido atualizado no atendimento.', 'success');
      setReceivableToUpdate(null);
      await load();
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível atualizar o recebimento.', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  const expenseCategories = entryForm.type === 'Expense'
    ? ['Materiais e insumos', 'Aluguel e condomínio', 'Marketing', 'Equipamentos', 'Equipe e serviços', 'Impostos e taxas', 'Outros']
    : ['Receita avulsa', 'Venda de produto', 'Reembolso recebido', 'Outros'];

  return (
    <div className="space-y-6 sm:space-y-8">
      <AdminPageHeader
        eyebrow="Financeiro e caixa"
        title="Saúde financeira da clínica"
        description="Recebimentos dos atendimentos, despesas e previsões do período."
        actions={<><div className="w-full sm:w-[10.5rem]"><Input aria-label="Mês de referência" type="month" value={month} onChange={event => setMonth(event.target.value)} /></div>{canManage && <Button onClick={openCreate}><Plus className="h-4 w-4" /> Novo lançamento</Button>}</>}
      />

      {isLoading ? (
        <>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">{Array.from({ length: 5 }, (_, index) => <SkeletonLoader key={index} className="h-28" />)}</div>
          <div className="grid gap-6 xl:grid-cols-5"><SkeletonLoader className="h-80 xl:col-span-3" /><SkeletonLoader className="h-80 xl:col-span-2" /></div>
        </>
      ) : !overview ? (
        <Card className="py-12 text-center"><AlertCircle className="mx-auto h-8 w-8 text-rose-400" /><h2 className="mt-3 font-display text-lg font-bold text-white">Dados financeiros indisponíveis</h2><p className="mt-1 text-sm text-slate-400">Atualize a página para tentar novamente.</p></Card>
      ) : (
        <>
          <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5" aria-label="Resumo financeiro">
            <Card className="border-l-4 border-l-emerald-500 p-5"><span className="text-[11px] font-semibold uppercase tracking-wide text-slate-400">Entradas realizadas</span><p className="mt-2 font-display text-2xl font-bold text-emerald-400">{formatCurrency(overview.appointmentIncome + overview.manualIncome)}</p><p className="mt-1 text-[11px] text-slate-500">Atendimentos: {formatCurrency(overview.appointmentIncome)}</p></Card>
            <Card className="border-l-4 border-l-rose-500 p-5"><span className="text-[11px] font-semibold uppercase tracking-wide text-slate-400">Saídas realizadas</span><p className="mt-2 font-display text-2xl font-bold text-rose-400">{formatCurrency(overview.manualExpense)}</p><p className="mt-1 text-[11px] text-slate-500">Despesas manuais do período</p></Card>
            <Card className="border-l-4 border-l-sky-500 p-5"><span className="text-[11px] font-semibold uppercase tracking-wide text-slate-400">Caixa líquido</span><p className={`mt-2 font-display text-2xl font-bold ${overview.netCash >= 0 ? 'text-sky-400' : 'text-rose-400'}`}>{formatCurrency(overview.netCash)}</p><p className="mt-1 text-[11px] text-slate-500">Entradas menos saídas</p></Card>
            <Card className="border-l-4 border-l-amber-500 p-5"><span className="text-[11px] font-semibold uppercase tracking-wide text-slate-400">A receber</span><p className="mt-2 font-display text-2xl font-bold text-amber-400">{formatCurrency(overview.receivable)}</p><p className="mt-1 text-[11px] text-slate-500">{overview.pendingReceivables} atendimento(s) com saldo</p></Card>
            <Card className="border-l-4 border-l-violet-500 p-5"><span className="text-[11px] font-semibold uppercase tracking-wide text-slate-400">Previsão do período</span><p className={`mt-2 font-display text-2xl font-bold ${overview.projectedBalance >= 0 ? 'text-violet-400' : 'text-rose-400'}`}>{formatCurrency(overview.projectedBalance)}</p><p className="mt-1 text-[11px] text-slate-500">Inclui recebíveis e lançamentos previstos</p></Card>
          </section>

          <section className="grid gap-6 xl:grid-cols-5">
            <Card className="xl:col-span-3">
              <div className="flex flex-col gap-2 border-b border-slate-800 pb-4 sm:flex-row sm:items-start sm:justify-between"><div><h2 className="font-display text-lg font-bold text-white">Fluxo de caixa diário</h2><p className="mt-0.5 text-xs text-slate-400">Entradas e saídas realizadas nos últimos 14 dias do período.</p></div><div className="flex gap-3 text-[10px] text-slate-400"><span className="flex items-center gap-1.5"><i className="h-2 w-2 rounded-full bg-emerald-400" /> Entradas</span><span className="flex items-center gap-1.5"><i className="h-2 w-2 rounded-full bg-rose-400" /> Saídas</span></div></div>
              <div className="mt-6 grid h-48 items-end gap-1.5 sm:gap-2" style={{ gridTemplateColumns: `repeat(${Math.max(visibleFlow.length, 1)}, minmax(0, 1fr))` }}>
                {visibleFlow.map(day => <div key={day.date} className="group flex h-full min-w-0 flex-col justify-end gap-1" title={`${formatDate(day.date)} — entradas ${formatCurrency(day.income)}, saídas ${formatCurrency(day.expense)}`}><div className="mx-auto w-full max-w-3 rounded-t bg-emerald-400/80 transition-opacity group-hover:bg-emerald-300" style={{ height: day.income ? `${Math.max(4, (day.income / flowMax) * 100)}%` : '0%' }} /><div className="mx-auto w-full max-w-3 rounded-t bg-rose-400/80 transition-opacity group-hover:bg-rose-300" style={{ height: day.expense ? `${Math.max(4, (day.expense / flowMax) * 100)}%` : '0%' }} /><span className="mt-1 truncate text-center text-[9px] text-slate-500">{formatDate(day.date).slice(0, 5)}</span></div>)}
              </div>
            </Card>

            <Card className="xl:col-span-2">
              <div className="border-b border-slate-800 pb-4"><h2 className="font-display text-lg font-bold text-white">Despesas por categoria</h2><p className="mt-0.5 text-xs text-slate-400">Somente lançamentos já realizados.</p></div>
              {overview.expenseCategories.length ? <div className="mt-5 space-y-4">{overview.expenseCategories.map(category => <div key={category.category}><div className="mb-1.5 flex items-center justify-between gap-3 text-xs"><span className="truncate font-medium text-slate-300">{category.category}</span><span className="shrink-0 text-slate-400">{formatCurrency(category.amount)}</span></div><div className="h-2 overflow-hidden rounded-full bg-slate-800"><div className="h-full rounded-full bg-gradient-to-r from-rose-500 to-orange-400" style={{ width: `${(category.amount / categoryMax) * 100}%` }} /></div></div>)}</div> : <div className="flex min-h-40 flex-col items-center justify-center text-center"><ArrowDownLeft className="h-7 w-7 text-slate-600" /><p className="mt-3 text-sm font-medium text-slate-300">Nenhuma despesa realizada</p><p className="mt-1 text-xs text-slate-500">Registre o primeiro lançamento para acompanhar os custos.</p></div>}
            </Card>
          </section>

          <section className="grid gap-6 xl:grid-cols-5">
            <Card className="xl:col-span-3">
              <div className="flex flex-col gap-3 border-b border-slate-800 pb-4 sm:flex-row sm:items-start sm:justify-between"><div><h2 className="font-display text-lg font-bold text-white">Contas a receber</h2><p className="mt-0.5 text-xs text-slate-400">Saldos de atendimentos não cancelados até o fim do período.</p></div><Badge variant="warning">{overview.pendingReceivables} pendência(s)</Badge></div>
              {overview.outstandingReceivables.length ? <div className="mt-2 divide-y divide-slate-800/80">{overview.outstandingReceivables.map(item => <div key={item.appointmentId} className="flex flex-col gap-3 py-3 sm:flex-row sm:items-center sm:justify-between"><div className="min-w-0"><div className="flex flex-wrap items-center gap-2"><p className="truncate text-sm font-semibold text-white">{item.clientName}</p>{item.isOverdue && <Badge variant="danger">Em atraso</Badge>}</div><p className="mt-0.5 truncate text-xs text-slate-400">{item.procedureName} • {formatClinicDate(item.scheduledDateTime)}</p><p className="mt-1 text-[11px] text-slate-500">Recebido {formatCurrency(item.amountPaid)} de {formatCurrency(item.totalPrice)}</p></div><div className="flex shrink-0 items-center justify-between gap-3 sm:justify-end"><strong className="text-sm text-amber-400">{formatCurrency(item.outstandingAmount)}</strong>{canManage && <Button size="sm" variant="outline" onClick={() => openReceivable(item)}>Registrar</Button>}</div></div>)}</div> : <p className="py-10 text-center text-sm text-slate-500">Nenhum saldo pendente no período.</p>}
            </Card>

            <Card className="xl:col-span-2">
              <div className="flex items-start gap-3"><span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border border-violet-500/30 bg-violet-500/10 text-violet-400"><Clock className="h-5 w-5" /></span><div><h2 className="font-display text-lg font-bold text-white">Lançamentos previstos</h2><p className="mt-0.5 text-xs text-slate-400">Impacto futuro dentro do período.</p></div></div><div className="mt-6 grid grid-cols-2 gap-3"><div className="rounded-xl border border-emerald-500/20 bg-emerald-500/5 p-3"><span className="text-[10px] font-semibold uppercase text-emerald-300">Entradas</span><p className="mt-1 font-display text-lg font-bold text-emerald-400">{formatCurrency(overview.plannedIncome)}</p></div><div className="rounded-xl border border-rose-500/20 bg-rose-500/5 p-3"><span className="text-[10px] font-semibold uppercase text-rose-300">Saídas</span><p className="mt-1 font-display text-lg font-bold text-rose-400">{formatCurrency(overview.plannedExpense)}</p></div></div><p className="mt-5 text-xs leading-relaxed text-slate-400">Os valores previstos não entram no caixa atual até serem confirmados.</p>
            </Card>
          </section>

          <section>
            <div className="mb-3 flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
              <div>
                <h2 className="font-display text-lg font-bold text-white">Livro-caixa</h2>
                <p className="mt-0.5 text-xs text-slate-400">Recebimentos de atendimentos e lançamentos manuais reunidos no mesmo histórico.</p>
              </div>
              <div className="grid w-full gap-2 sm:grid-cols-2 xl:w-auto xl:grid-cols-5">
                <Select aria-label="Filtrar origem" value={sourceFilter} onChange={event => setSourceFilter(event.target.value as FinancialLedgerSource | '')}>
                  <option value="">Todas as origens</option>
                  <option value="AppointmentReceipt">Atendimentos</option>
                  <option value="ManualEntry">Manuais</option>
                </Select>
                <Select aria-label="Filtrar tipo" value={typeFilter} onChange={event => setTypeFilter(event.target.value as FinancialEntryType | '')}>
                  <option value="">Entradas e saídas</option>
                  <option value="Income">Entradas</option>
                  <option value="Expense">Saídas</option>
                </Select>
                <Select aria-label="Filtrar situação" value={statusFilter} onChange={event => setStatusFilter(event.target.value as FinancialEntryStatus | '')}>
                  <option value="">Todas as situações</option>
                  <option value="Settled">Realizados</option>
                  <option value="Planned">Previstos</option>
                  <option value="Cancelled">Cancelados</option>
                </Select>
                <div>
                  <Input aria-label="Filtrar categoria" list="ledger-category-filters" placeholder="Categoria" value={categoryFilter} onChange={event => setCategoryFilter(event.target.value)} />
                  <datalist id="ledger-category-filters">{ledgerCategories.map(category => <option key={category} value={category} />)}</datalist>
                </div>
                <Input aria-label="Buscar movimentação" placeholder="Buscar" value={search} onChange={event => setSearch(event.target.value)} />
              </div>
            </div>
            <Card className="overflow-hidden p-0">
              {filteredLedgerItems.length ? <div className="divide-y divide-slate-800/80">{filteredLedgerItems.map(item => {
                const isManualEntry = item.source === 'ManualEntry';
                const canActOnManualEntry = canManage && isManualEntry && item.status !== 'Cancelled';
                return <article key={`${item.source}-${item.id}`} className="grid grid-cols-[minmax(0,1fr)_auto] gap-3 p-4 sm:grid-cols-[minmax(0,1fr)_7rem_8rem_7.5rem] sm:items-center">
                  <div className="order-1 col-span-2 sm:col-span-4">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className={`flex h-7 w-7 items-center justify-center rounded-full ${item.type === 'Income' ? 'bg-emerald-500/10 text-emerald-400' : 'bg-rose-500/10 text-rose-400'}`}>{item.type === 'Income' ? <ArrowUpRight className="h-4 w-4" /> : <ArrowDownLeft className="h-4 w-4" />}</span>
                      <p className="min-w-0 break-words text-sm font-semibold text-white">{item.description}</p>
                      <Badge variant={statusBadge(item.status)}>{entryStatusLabels[item.status]}</Badge>
                      <Badge variant={isManualEntry ? 'default' : 'info'}>{ledgerSourceLabels[item.source]}</Badge>
                    </div>
                  </div>
                  <div className="order-2 col-span-2 min-w-0 sm:col-span-1">
                    <p className="mt-1 truncate text-xs text-slate-400">{item.category} • {paymentMethodLabels[item.paymentMethod]} • {formatDate(item.effectiveDate)}</p>
                    {!isManualEntry && <p className="mt-1 text-[11px] text-slate-500">Valor acumulado informado no atendimento{item.paymentStatus ? ` • ${item.paymentStatus === 'Paid' ? 'Pago' : item.paymentStatus === 'Partial' ? 'Parcial' : 'Pendente'}` : ''}</p>}
                  </div>
                  <div className={`order-3 col-span-2 min-h-8 items-center justify-start gap-1.5 sm:order-5 sm:col-span-1 ${canActOnManualEntry ? 'flex' : 'hidden sm:flex'}`} aria-label={canActOnManualEntry ? 'Ações do lançamento' : undefined}>
                    {canManage && isManualEntry && item.status === 'Planned' && <Button type="button" size="sm" variant="outline" onClick={() => void settleEntry(item)} disabled={isSaving} title="Marcar como realizado"><CheckCircle className="h-3.5 w-3.5" /></Button>}
                    {canManage && isManualEntry && item.status !== 'Cancelled' && <Button type="button" size="sm" variant="outline" onClick={() => openEdit(item)} disabled={isSaving} title="Editar lançamento"><Edit className="h-3.5 w-3.5" /></Button>}
                    {canManage && isManualEntry && item.status !== 'Cancelled' && <Button type="button" size="sm" variant="danger" onClick={() => setEntryToCancel(item)} disabled={isSaving} title="Cancelar lançamento"><Trash2 className="h-3.5 w-3.5" /></Button>}
                  </div>
                  <span className="order-4 col-start-1 self-center text-xs text-slate-400 sm:order-3 sm:col-auto">{entryTypeLabels[item.type]}</span>
                  <strong className={`order-5 col-start-2 justify-self-end ${item.type === 'Income' ? 'text-emerald-400' : 'text-rose-400'} sm:order-4 sm:col-auto`}>{item.type === 'Income' ? '+' : '−'} {formatCurrency(item.amount)}</strong>
                </article>;
              })}</div> : <p className="py-10 text-center text-sm text-slate-500">Nenhuma movimentação encontrada para estes filtros.</p>}
            </Card>
          </section>
        </>
      )}

      <Dialog isOpen={isEntryDialogOpen} onClose={() => { if (!isSaving) setIsEntryDialogOpen(false); }} title={editingEntry ? 'Editar lançamento' : 'Novo lançamento financeiro'} maxWidth="max-w-2xl">
        <form onSubmit={saveEntry} className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2"><Select label="Movimentação" value={entryForm.type} onChange={event => setEntryForm({ ...entryForm, type: event.target.value as FinancialEntryType, category: event.target.value === 'Expense' ? 'Materiais e insumos' : 'Receita avulsa' })}><option value="Expense">Saída / despesa</option><option value="Income">Entrada manual</option></Select><Select label="Situação" value={entryForm.status} onChange={event => setEntryForm({ ...entryForm, status: event.target.value as EntryForm['status'] })}><option value="Settled">Realizado</option><option value="Planned">Previsto</option></Select></div>
          <div className="grid gap-4 sm:grid-cols-2"><Input label="Descrição" required value={entryForm.description} onChange={event => setEntryForm({ ...entryForm, description: event.target.value })} placeholder={entryForm.type === 'Expense' ? 'Ex.: Compra de dermocosméticos' : 'Ex.: Venda de produto'} /><div><Input label="Categoria" required list="finance-category-suggestions" value={entryForm.category} onChange={event => setEntryForm({ ...entryForm, category: event.target.value })} /><datalist id="finance-category-suggestions">{expenseCategories.map(category => <option key={category} value={category} />)}</datalist></div></div>
          <div className="grid gap-4 sm:grid-cols-2"><Input label="Valor (R$)" required inputMode="numeric" value={entryForm.amount} onChange={event => setEntryForm({ ...entryForm, amount: formatCurrencyInput(event.target.value) })} /><Input label={entryForm.status === 'Settled' ? 'Data de realização' : 'Data prevista'} required type="date" value={entryForm.effectiveDate} onChange={event => setEntryForm({ ...entryForm, effectiveDate: event.target.value })} /></div>
          <Select label="Forma de pagamento" value={entryForm.paymentMethod} onChange={event => setEntryForm({ ...entryForm, paymentMethod: event.target.value as PaymentMethod })}>{(Object.keys(paymentMethodLabels) as PaymentMethod[]).map(method => <option key={method} value={method}>{paymentMethodLabels[method]}</option>)}</Select>
          <Input label="Observações" value={entryForm.notes} onChange={event => setEntryForm({ ...entryForm, notes: event.target.value })} placeholder="Opcional" />
          <div className="flex justify-end gap-2"><Button type="button" variant="outline" onClick={() => setIsEntryDialogOpen(false)} disabled={isSaving}>Cancelar</Button><Button type="submit" disabled={isSaving}>{isSaving ? 'Salvando...' : editingEntry ? 'Salvar alterações' : 'Registrar lançamento'}</Button></div>
        </form>
      </Dialog>

      <Dialog isOpen={Boolean(receivableToUpdate)} onClose={() => { if (!isSaving) setReceivableToUpdate(null); }} title="Registrar recebimento" maxWidth="max-w-md">
        {receivableToUpdate && <form onSubmit={savePayment} className="space-y-4"><div className="rounded-lg border border-slate-800 bg-slate-950/40 p-3 text-sm"><strong className="text-white">{receivableToUpdate.clientName}</strong><p className="mt-1 text-xs text-slate-400">{receivableToUpdate.procedureName} • saldo atual: <span className="text-amber-400">{formatCurrency(receivableToUpdate.outstandingAmount)}</span></p></div><Input label="Total já recebido neste atendimento (R$)" required inputMode="numeric" value={paymentAmount} onChange={event => setPaymentAmount(formatCurrencyInput(event.target.value))} /><Select label="Forma de pagamento" value={paymentMethod} onChange={event => setPaymentMethod(event.target.value as PaymentMethod)}>{(Object.keys(paymentMethodLabels) as PaymentMethod[]).map(method => <option key={method} value={method}>{paymentMethodLabels[method]}</option>)}</Select><p className="text-xs text-slate-500">Informe o valor acumulado recebido. O sistema recalcula o saldo automaticamente.</p><div className="flex justify-end gap-2"><Button type="button" variant="outline" onClick={() => setReceivableToUpdate(null)} disabled={isSaving}>Cancelar</Button><Button type="submit" disabled={isSaving}>{isSaving ? 'Atualizando...' : 'Atualizar recebimento'}</Button></div></form>}
      </Dialog>

      <Dialog isOpen={Boolean(entryToCancel)} onClose={() => { if (!isSaving) setEntryToCancel(null); }} title="Cancelar lançamento" maxWidth="max-w-md">
        <div className="space-y-4"><p className="text-sm text-slate-300">Deseja cancelar <strong className="text-white">{entryToCancel?.description}</strong>? O lançamento não será apagado: continuará disponível para auditoria, mas deixará de afetar o caixa.</p><div className="flex justify-end gap-2"><Button variant="outline" onClick={() => setEntryToCancel(null)} disabled={isSaving}>Voltar</Button><Button variant="danger" onClick={() => void cancelEntry()} disabled={isSaving}>{isSaving ? 'Cancelando...' : 'Cancelar lançamento'}</Button></div></div>
      </Dialog>
    </div>
  );
};
