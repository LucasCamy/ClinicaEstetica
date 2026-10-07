import React, { useEffect, useState } from 'react';
import { CalendarDays, CheckCircle2, Link2, RefreshCw, ShieldCheck, Unplug, AlertTriangle } from 'lucide-react';
import { useLocation, useNavigate } from 'react-router-dom';
import { GoogleCalendarConnection, GoogleCalendarListItem } from '../../types';
import { api } from '../../services/api';
import { useToast } from '../../contexts/ToastContext';
import { Badge, Button, Card, Select, useConfirmationDialog } from '../ui/Components';

const formatDateTime = (value?: string | null) => value
  ? new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value))
  : 'Ainda não sincronizada';

export const GoogleCalendarSettingsView: React.FC = () => {
  const { showToast } = useToast();
  const location = useLocation();
  const navigate = useNavigate();
  const [connection, setConnection] = useState<GoogleCalendarConnection | null>(null);
  const [calendars, setCalendars] = useState<GoogleCalendarListItem[]>([]);
  const [selectedCalendarId, setSelectedCalendarId] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [isBusy, setIsBusy] = useState(false);
  const { confirm, confirmationDialog } = useConfirmationDialog();

  const load = async (showFeedback = false) => {
    try {
      setIsLoading(true);
      const current = await api.getGoogleCalendarConnection();
      setConnection(current);
      setSelectedCalendarId(current.calendarId || '');
      if (current.isConfigured && current.isConnected) {
        const available = await api.getGoogleCalendars();
        setCalendars(available);
      } else {
        setCalendars([]);
      }
      if (showFeedback) showToast('Status da integração atualizado.', 'success');
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível consultar a integração do Google.', 'error');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => { void load(); }, []);

  useEffect(() => {
    const result = new URLSearchParams(location.search).get('google');
    if (!result) return;
    const messages: Record<string, [string, 'success' | 'error' | 'warning']> = {
      connected: ['Conta Google conectada. Agora selecione a agenda compartilhada.', 'success'],
      denied: ['A autorização do Google foi cancelada.', 'warning'],
      invalid: ['A autorização do Google expirou ou não pôde ser validada. Tente novamente.', 'error'],
      authorization_failed: ['O Google autorizou a conta, mas não foi possível concluir a credencial local. Tente conectar novamente; se persistir, verifique o log do backend.', 'error'],
      failed: ['Não foi possível concluir a conexão com o Google. Confira as credenciais e tente novamente.', 'error'],
    };
    const message = messages[result];
    if (message) showToast(message[0], message[1]);
    navigate('/admin/configuracoes', { replace: true });
    void load();
  }, [location.search, navigate, showToast]);

  const connect = async () => {
    try {
      setIsBusy(true);
      const { authorizationUrl } = await api.startGoogleCalendarConnection();
      window.location.assign(authorizationUrl);
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível iniciar a conexão com o Google.', 'error');
      setIsBusy(false);
    }
  };

  const selectCalendar = async () => {
    if (!selectedCalendarId) {
      showToast('Escolha uma agenda antes de salvar.', 'warning');
      return;
    }
    try {
      setIsBusy(true);
      const updated = await api.selectGoogleCalendar(selectedCalendarId);
      setConnection(updated);
      showToast('Agenda compartilhada selecionada. Os eventos externos aparecerão somente na grade da agenda.', 'success');
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível selecionar a agenda.', 'error');
    } finally {
      setIsBusy(false);
    }
  };

  const disconnect = async () => {
    const accepted = await confirm({
      title: 'Desconectar conta Google',
      description: 'Os agendamentos já existentes no Google não serão apagados. Apenas a sincronização e a leitura da agenda compartilhada serão interrompidas.',
      confirmLabel: 'Desconectar conta',
      variant: 'danger',
    });
    if (!accepted) return;
    try {
      setIsBusy(true);
      await api.disconnectGoogleCalendar();
      setConnection(null);
      setCalendars([]);
      setSelectedCalendarId('');
      showToast('Conta Google desconectada. Nenhum evento externo foi removido.', 'success');
      await load();
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível desconectar a conta Google.', 'error');
    } finally {
      setIsBusy(false);
    }
  };

  return (
    <>
    <div className="space-y-4">
      <div className="flex justify-end">
        <Button variant="outline" onClick={() => void load(true)} disabled={isLoading || isBusy}><RefreshCw className={`h-4 w-4 ${isLoading ? 'animate-spin' : ''}`} /> Atualizar status</Button>
      </div>
      <Card className="space-y-5">
        <div className="flex flex-col justify-between gap-4 border-b border-slate-800 pb-5 sm:flex-row sm:items-start">
          <div className="flex gap-3">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border border-sky-500/30 bg-sky-500/10 text-sky-400"><CalendarDays className="h-5 w-5" /></div>
            <div>
              <div className="flex flex-wrap items-center gap-2"><h2 className="font-display text-lg font-bold text-white">Agenda compartilhada</h2>{connection?.hasSelectedCalendar ? <Badge variant="success">Ativa</Badge> : <Badge variant="warning">Pendente</Badge>}</div>
              <p className="mt-1 max-w-2xl text-sm text-slate-400">Os compromissos do Google são mostrados com cartão azul na timeline. Eles são consultados sob demanda e não entram no banco, prontuário, financeiro ou relatórios.</p>
            </div>
          </div>
          {connection?.isConnected && <Button variant="danger" size="sm" onClick={() => void disconnect()} disabled={isBusy}><Unplug className="h-4 w-4" /> Desconectar</Button>}
        </div>

        {isLoading ? <p className="text-sm text-slate-400">Consultando a integração…</p> : !connection?.isConfigured ? (
          <div className="rounded-xl border border-amber-500/25 bg-amber-500/5 p-4 text-sm text-amber-200"><div className="flex items-start gap-2"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" /><div><strong>Credenciais pendentes no servidor.</strong><p className="mt-1 text-amber-100/75">Configure o Client ID, Client Secret e a URL de retorno do OAuth no arquivo .env. Nenhuma conta Google será solicitada antes disso.</p></div></div></div>
        ) : !connection?.isConnected ? (
          <div className="flex flex-col gap-4 rounded-xl border border-slate-800 bg-slate-950/40 p-4 sm:flex-row sm:items-center sm:justify-between"><div><p className="font-medium text-slate-100">Nenhuma conta conectada</p><p className="mt-1 text-xs text-slate-400">Conecte a conta Google da profissional que possui edição da agenda compartilhada.</p></div><Button onClick={() => void connect()} disabled={isBusy}><Link2 className="h-4 w-4" /> Conectar conta Google</Button></div>
        ) : (
          <div className="space-y-4">
            <div className="grid gap-3 text-sm sm:grid-cols-3"><div className="rounded-lg border border-slate-800 bg-slate-950/40 p-3"><span className="text-xs text-slate-500">Conta</span><p className="mt-1 font-medium text-slate-100">Conectada com segurança</p></div><div className="rounded-lg border border-slate-800 bg-slate-950/40 p-3"><span className="text-xs text-slate-500">Agenda atual</span><p className="mt-1 truncate font-medium text-slate-100">{connection.calendarName || 'Ainda não selecionada'}</p></div><div className="rounded-lg border border-slate-800 bg-slate-950/40 p-3"><span className="text-xs text-slate-500">Última leitura</span><p className="mt-1 font-medium text-slate-100">{formatDateTime(connection.lastSuccessfulSyncAtUtc)}</p></div></div>
            <div className="grid gap-3 rounded-xl border border-slate-800 bg-slate-950/30 p-4 lg:grid-cols-[minmax(0,1fr)_auto]"><Select label="Agenda que a conta pode editar" value={selectedCalendarId} onChange={event => setSelectedCalendarId(event.target.value)} disabled={isBusy}><option value="">Selecione a agenda compartilhada…</option>{calendars.map(calendar => <option key={calendar.id} value={calendar.id}>{calendar.name}{calendar.isPrimary ? ' — principal' : ''}</option>)}</Select><div className="flex items-end"><Button className="w-full lg:w-auto" onClick={() => void selectCalendar()} disabled={isBusy || !calendars.length}><CheckCircle2 className="h-4 w-4" /> Usar esta agenda</Button></div></div>
            <p className="flex gap-2 text-xs leading-relaxed text-slate-500"><ShieldCheck className="mt-0.5 h-4 w-4 shrink-0 text-emerald-400" />Ao enviar um atendimento do painel, o título será somente “primeiro nome — procedimento”. Dados clínicos, financeiros e documentos nunca são enviados ao Google.</p>
          </div>
        )}
      </Card>
    </div>
    {confirmationDialog}
    </>
  );
};
