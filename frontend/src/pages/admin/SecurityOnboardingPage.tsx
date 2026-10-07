import React, { useEffect, useState } from 'react';
import { KeyRound, ShieldCheck } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import { Button, Card, Input } from '../../components/ui/Components';

export const SecurityOnboardingPage: React.FC = () => {
  const { currentUser, changePassword, setupMfa, enableMfa, refresh, logout } = useAuth();
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [sharedKey, setSharedKey] = useState('');
  const [authenticatorUri, setAuthenticatorUri] = useState('');
  const [code, setCode] = useState('');
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!currentUser?.requiresMfaSetup || currentUser.mustChangePassword) return;
    setupMfa()
      .then(result => { setSharedKey(result.sharedKey); setAuthenticatorUri(result.authenticatorUri); })
      .catch(error => setError(error instanceof Error ? error.message : 'Falha ao preparar o MFA.'));
  }, [currentUser?.requiresMfaSetup, currentUser?.mustChangePassword]);

  const submitPassword = async (event: React.FormEvent) => {
    event.preventDefault();
    setError('');
    if (newPassword !== confirmPassword) {
      setError('A confirmação da nova senha não confere.');
      return;
    }
    setLoading(true);
    try { await changePassword(currentPassword, newPassword); }
    catch (error) { setError(error instanceof Error ? error.message : 'Não foi possível trocar a senha.'); }
    finally { setLoading(false); }
  };

  const submitMfa = async (event: React.FormEvent) => {
    event.preventDefault();
    setError('');
    setLoading(true);
    try {
      const result = await enableMfa(code);
      setRecoveryCodes(result.recoveryCodes);
      setCode('');
    }
    catch (error) { setError(error instanceof Error ? error.message : 'Não foi possível ativar o MFA.'); }
    finally { setLoading(false); }
  };

  return (
    <div className="min-h-screen bg-slate-950 flex items-center justify-center p-6">
      <Card className="w-full max-w-lg space-y-5 border-rose-500/30">
        <div className="flex items-center gap-3">
          {currentUser?.mustChangePassword
            ? <KeyRound className="w-7 h-7 text-rose-400" />
            : <ShieldCheck className="w-7 h-7 text-emerald-400" />}
          <div>
            <h1 className="font-display text-xl font-bold text-white">Proteção da conta</h1>
            <p className="text-xs text-slate-400">Conclua esta etapa antes de acessar dados da clínica.</p>
          </div>
        </div>

        {error && <div className="rounded-lg border border-rose-500/30 bg-rose-500/10 p-3 text-xs text-rose-300">{error}</div>}

        {recoveryCodes.length > 0 ? (
          <div className="space-y-4">
            <div className="rounded-lg border border-amber-500/40 bg-amber-500/10 p-4 text-sm text-amber-100">
              Guarde estes códigos em local seguro. Cada código funciona uma única vez e eles não serão mostrados novamente.
            </div>
            <div className="grid grid-cols-2 gap-2 rounded-lg border border-slate-800 bg-slate-900 p-4">
              {recoveryCodes.map(recoveryCode => (
                <code key={recoveryCode} className="select-all text-center text-sm text-rose-300">{recoveryCode}</code>
              ))}
            </div>
            <Button type="button" className="w-full" onClick={() => void refresh()}>
              Já salvei os códigos
            </Button>
          </div>
        ) : currentUser?.mustChangePassword ? (
          <form className="space-y-4" onSubmit={submitPassword}>
            <p className="text-sm text-slate-300">A senha temporária deve ser substituída por uma senha exclusiva com pelo menos 12 caracteres.</p>
            <Input label="Senha temporária atual" type="password" autoComplete="current-password" required value={currentPassword} onChange={e => setCurrentPassword(e.target.value)} />
            <Input label="Nova senha" type="password" autoComplete="new-password" minLength={12} required value={newPassword} onChange={e => setNewPassword(e.target.value)} />
            <Input label="Confirmar nova senha" type="password" autoComplete="new-password" minLength={12} required value={confirmPassword} onChange={e => setConfirmPassword(e.target.value)} />
            <Button type="submit" className="w-full" disabled={loading}>{loading ? 'Salvando...' : 'Trocar senha'}</Button>
          </form>
        ) : (
          <form className="space-y-4" onSubmit={submitMfa}>
            <p className="text-sm text-slate-300">Adicione uma nova conta no Microsoft Authenticator, Google Authenticator ou outro aplicativo TOTP.</p>
            <div className="rounded-lg bg-slate-900 border border-slate-800 p-4 space-y-2">
              <div className="text-[11px] uppercase tracking-wide text-slate-500">Chave de configuração</div>
              <code className="block break-all text-sm text-rose-300 select-all">{sharedKey || 'Carregando...'}</code>
              {authenticatorUri && <a className="text-xs text-sky-400 hover:underline break-all" href={authenticatorUri}>Abrir no aplicativo autenticador</a>}
            </div>
            <Input label="Código de seis números" inputMode="numeric" autoComplete="one-time-code" required value={code} onChange={e => setCode(e.target.value)} />
            <Button type="submit" className="w-full" disabled={loading || !sharedKey}>{loading ? 'Validando...' : 'Ativar autenticação multifator'}</Button>
          </form>
        )}

        <button type="button" className="w-full text-xs text-slate-500 hover:text-white" onClick={() => void logout()}>Sair e concluir depois</button>
      </Card>
    </div>
  );
};
