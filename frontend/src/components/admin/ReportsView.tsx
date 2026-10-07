import { Download, FileSpreadsheet, Filter, RotateCcw, Search } from 'lucide-react';
import React, { useEffect, useMemo, useState } from 'react';
import { api } from '../../services/api';
import {
  AppointmentStatus,
  CustomReportRequest,
  CustomReportResult,
  PaymentStatus,
  ProcedureType,
} from '../../types';
import { Button, Card, Input, Select, SkeletonLoader } from '../ui/Components';
import { AdminPageHeader } from './AdminPageHeader';

const reportFields = [
  { key: 'clientName', label: 'Cliente' },
  { key: 'clientPhone', label: 'Telefone' },
  { key: 'clientCity', label: 'Cidade' },
  { key: 'clientBirthDate', label: 'Nascimento' },
  { key: 'clientSource', label: 'Origem' },
  { key: 'procedureName', label: 'Procedimento' },
  { key: 'scheduledDate', label: 'Data agendada' },
  { key: 'completedDate', label: 'Data concluída' },
  { key: 'appointmentStatus', label: 'Status do atendimento' },
  { key: 'paymentStatus', label: 'Status do pagamento' },
  { key: 'paymentMethod', label: 'Forma de pagamento' },
  { key: 'totalPrice', label: 'Valor total' },
  { key: 'amountPaid', label: 'Valor pago' },
  { key: 'outstandingAmount', label: 'Saldo' },
  { key: 'professionalName', label: 'Profissional' },
  { key: 'lastCompletedProcedure', label: 'Último procedimento' },
  { key: 'lastCompletedDate', label: 'Última conclusão' },
  { key: 'returnDueDate', label: 'Retorno recomendado' },
  { key: 'returnStatus', label: 'Situação do retorno' },
];

const defaultFields = ['clientName', 'procedureName', 'completedDate', 'appointmentStatus', 'paymentStatus', 'totalPrice', 'amountPaid', 'outstandingAmount', 'returnDueDate', 'returnStatus'];

const appointmentStatuses: Array<{ value: AppointmentStatus; label: string }> = [
  { value: 'Scheduled', label: 'Marcado' },
  { value: 'Confirmed', label: 'Confirmado' },
  { value: 'InProgress', label: 'Atendimento' },
  { value: 'Completed', label: 'Concluído' },
  { value: 'Cancelled', label: 'Cancelado' },
  { value: 'NoShow', label: 'Não compareceu' },
];

const paymentStatuses: Array<{ value: PaymentStatus; label: string }> = [
  { value: 'Pending', label: 'Pendente' },
  { value: 'Partial', label: 'Parcial' },
  { value: 'Paid', label: 'Pago' },
];

const months = [
  'Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho',
  'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro',
];

const initialFilters = () => ({
  fields: defaultFields,
  fromDate: '',
  toDate: '',
  procedureTypeId: '',
  appointmentStatus: '',
  paymentStatus: '',
  birthdayMonth: '',
  search: '',
  sortBy: 'completedDate',
  sortDescending: true,
});

export const ReportsView: React.FC = () => {
  const [procedures, setProcedures] = useState<ProcedureType[]>([]);
  const [filters, setFilters] = useState(initialFilters);
  const [result, setResult] = useState<CustomReportResult | null>(null);
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(true);
  const [isExporting, setIsExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const buildRequest = (requestedPage = page): CustomReportRequest => ({
    fields: filters.fields,
    ...(filters.fromDate ? { fromDate: filters.fromDate } : {}),
    ...(filters.toDate ? { toDate: filters.toDate } : {}),
    ...(filters.procedureTypeId ? { procedureTypeId: filters.procedureTypeId } : {}),
    appointmentStatuses: filters.appointmentStatus ? [filters.appointmentStatus as AppointmentStatus] : [],
    paymentStatuses: filters.paymentStatus ? [filters.paymentStatus as PaymentStatus] : [],
    ...(filters.birthdayMonth ? { birthdayMonth: Number(filters.birthdayMonth) } : {}),
    ...(filters.search.trim() ? { search: filters.search.trim() } : {}),
    ...(filters.sortBy ? { sortBy: filters.sortBy } : {}),
    sortDescending: filters.sortDescending,
    page: requestedPage,
    pageSize: 50,
  });

  const runReport = async (requestedPage = page) => {
    if (!filters.fields.length) {
      setError('Selecione ao menos uma coluna para montar o relatório.');
      return;
    }
    try {
      setIsLoading(true);
      setError(null);
      const [report, catalog] = await Promise.all([
        api.runCustomReport(buildRequest(requestedPage)),
        procedures.length ? Promise.resolve(procedures) : api.getProcedures(),
      ]);
      setResult(report);
      setProcedures(catalog);
      setPage(requestedPage);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Não foi possível gerar o relatório.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => { void runReport(1); }, []);

  const totalPages = Math.max(1, Math.ceil((result?.totalCount ?? 0) / 50));
  const selectedFieldSet = useMemo(() => new Set(filters.fields), [filters.fields]);

  const toggleField = (key: string) => setFilters(current => ({
    ...current,
    fields: current.fields.includes(key)
      ? current.fields.filter(field => field !== key)
      : [...current.fields, key],
  }));

  const reset = () => {
    setFilters(initialFilters());
    setPage(1);
    setError(null);
  };

  const exportCsv = async () => {
    if (!filters.fields.length) return;
    try {
      setIsExporting(true);
      setError(null);
      const { blob, fileName } = await api.exportCustomReport(buildRequest(1));
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = fileName;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Não foi possível exportar o CSV.');
    } finally {
      setIsExporting(false);
    }
  };

  return (
    <div className="space-y-6">
      <AdminPageHeader
        eyebrow="Consulta segura"
        title="Relatórios personalizados"
        description="Escolha campos e filtros para consultar os atendimentos registrados e exportar somente o necessário."
        actions={<Button onClick={() => void exportCsv()} disabled={!result?.rows.length || isExporting}><Download className="h-4 w-4" /> {isExporting ? 'Preparando CSV...' : 'Exportar CSV'}</Button>}
      />

      <Card className="space-y-5">
        <div className="flex flex-col gap-2 border-b border-slate-800 pb-4 sm:flex-row sm:items-start sm:justify-between">
          <div><h2 className="font-display text-lg font-bold text-white">Monte a consulta</h2><p className="mt-1 text-xs text-slate-400">Os dados clínicos descritivos e respostas de formulários não fazem parte desta exportação.</p></div>
          <Button variant="ghost" size="sm" onClick={reset}><RotateCcw className="h-3.5 w-3.5" /> Restaurar padrão</Button>
        </div>

        <div>
          <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-400">Colunas do relatório</p>
          <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {reportFields.map(field => <label key={field.key} className={`flex cursor-pointer items-center gap-2 rounded-lg border px-3 py-2 text-xs transition-colors ${selectedFieldSet.has(field.key) ? 'border-rose-500/40 bg-rose-500/10 text-slate-100' : 'border-slate-800 bg-slate-950/40 text-slate-400 hover:border-slate-700'}`}><input type="checkbox" checked={selectedFieldSet.has(field.key)} onChange={() => toggleField(field.key)} /> {field.label}</label>)}
          </div>
        </div>

        <div className="grid gap-4 border-t border-slate-800 pt-5 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          <Input label="Data inicial" type="date" value={filters.fromDate} onChange={event => setFilters({ ...filters, fromDate: event.target.value })} />
          <Input label="Data final" type="date" value={filters.toDate} onChange={event => setFilters({ ...filters, toDate: event.target.value })} />
          <Select label="Procedimento" value={filters.procedureTypeId} onChange={event => setFilters({ ...filters, procedureTypeId: event.target.value })}><option value="">Todos os procedimentos</option>{procedures.map(procedure => <option key={procedure.id} value={procedure.id}>{procedure.name}{procedure.isActive ? '' : ' (inativo)'}</option>)}</Select>
          <Select label="Status do atendimento" value={filters.appointmentStatus} onChange={event => setFilters({ ...filters, appointmentStatus: event.target.value })}><option value="">Todos os status</option>{appointmentStatuses.map(status => <option key={status.value} value={status.value}>{status.label}</option>)}</Select>
          <Select label="Status do pagamento" value={filters.paymentStatus} onChange={event => setFilters({ ...filters, paymentStatus: event.target.value })}><option value="">Todos os pagamentos</option>{paymentStatuses.map(status => <option key={status.value} value={status.value}>{status.label}</option>)}</Select>
          <Select label="Mês de aniversário" value={filters.birthdayMonth} onChange={event => setFilters({ ...filters, birthdayMonth: event.target.value })}><option value="">Todos os meses</option>{months.map((month, index) => <option key={month} value={index + 1}>{month}</option>)}</Select>
          <Select label="Ordenar por" value={filters.sortBy} onChange={event => setFilters({ ...filters, sortBy: event.target.value })}><option value="completedDate">Data concluída</option><option value="scheduledDate">Data agendada</option><option value="clientName">Cliente</option><option value="procedureName">Procedimento</option></Select>
          <Select label="Direção" value={filters.sortDescending ? 'desc' : 'asc'} onChange={event => setFilters({ ...filters, sortDescending: event.target.value === 'desc' })}><option value="desc">Mais recente primeiro</option><option value="asc">Mais antigo primeiro</option></Select>
          <div className="sm:col-span-2 lg:col-span-3 xl:col-span-4"><Input label="Buscar cliente, telefone ou procedimento" placeholder="Ex.: Maria, 6599... ou botox" value={filters.search} onChange={event => setFilters({ ...filters, search: event.target.value })} /></div>
        </div>
        <div className="flex flex-col gap-3 border-t border-slate-800 pt-4 sm:flex-row sm:justify-end"><Button variant="outline" onClick={reset}>Limpar filtros</Button><Button onClick={() => void runReport(1)} disabled={isLoading}><Filter className="h-4 w-4" /> {isLoading ? 'Consultando...' : 'Gerar relatório'}</Button></div>
      </Card>

      {error && <div role="alert" className="rounded-xl border border-rose-500/30 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">{error}</div>}

      <Card className="overflow-hidden p-0">
        <div className="flex flex-col gap-3 border-b border-slate-800 p-5 sm:flex-row sm:items-center sm:justify-between"><div className="flex items-center gap-3"><span className="flex h-9 w-9 items-center justify-center rounded-lg border border-sky-500/30 bg-sky-500/10 text-sky-400"><FileSpreadsheet className="h-4 w-4" /></span><div><h2 className="font-display font-bold text-white">Resultado da consulta</h2><p className="text-xs text-slate-400">{result ? `${result.totalCount.toLocaleString('pt-BR')} registro(s) encontrado(s)` : 'Carregando registros...'}</p></div></div><span className="text-xs text-slate-500">Até 10.000 linhas por CSV</span></div>
        {isLoading ? <div className="space-y-3 p-5"><SkeletonLoader className="h-10 w-full" /><SkeletonLoader className="h-10 w-full" /><SkeletonLoader className="h-10 w-full" /></div> : result?.rows.length ? <><div className="overflow-x-auto"><table className="min-w-full text-left text-xs"><thead className="bg-slate-950/60 text-[10px] uppercase tracking-wide text-slate-500"><tr>{result.columns.map(column => <th key={column.key} className="whitespace-nowrap px-4 py-3 font-semibold">{column.label}</th>)}</tr></thead><tbody className="divide-y divide-slate-800/80">{result.rows.map(row => <tr key={row.appointmentId} className="text-slate-300 hover:bg-slate-900/40">{result.columns.map(column => <td key={column.key} className="whitespace-nowrap px-4 py-3">{row.values[column.key] || <span className="text-slate-600">—</span>}</td>)}</tr>)}</tbody></table></div><div className="flex flex-col gap-3 border-t border-slate-800 p-4 text-xs text-slate-400 sm:flex-row sm:items-center sm:justify-between"><span>Página {page} de {totalPages}</span><div className="flex gap-2"><Button size="sm" variant="outline" disabled={page <= 1} onClick={() => void runReport(page - 1)}>Anterior</Button><Button size="sm" variant="outline" disabled={page >= totalPages} onClick={() => void runReport(page + 1)}>Próxima</Button></div></div></> : <div className="flex min-h-56 flex-col items-center justify-center p-8 text-center"><Search className="h-7 w-7 text-slate-600" /><p className="mt-3 font-medium text-slate-300">Nenhum registro encontrado</p><p className="mt-1 max-w-md text-xs text-slate-500">Ajuste os filtros ou amplie o intervalo de datas para consultar outros atendimentos.</p></div>}
      </Card>
    </div>
  );
};
