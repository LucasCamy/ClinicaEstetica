import React, { useEffect, useState } from 'react';
import { Download, Share, Sparkles, ShieldAlert } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import { Button, Input, Card } from '../../components/ui/Components';

type LoginPageProps = {
  onBackToLanding: () => void;
  onAuthenticated?: () => void;
};

type DeferredInstallPrompt = Event & {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
};

type MobileInstallPlatform = 'android' | 'ios' | null;

const isPwaStandalone = () =>
  window.matchMedia('(display-mode: standalone)').matches
  || (window.navigator as Navigator & { standalone?: boolean }).standalone === true;

const detectMobileInstallPlatform = (): MobileInstallPlatform => {
  const userAgent = window.navigator.userAgent;
  const isIos = /iPad|iPhone|iPod/i.test(userAgent)
    || (userAgent.includes('Macintosh') && window.navigator.maxTouchPoints > 1);
  if (isIos) return 'ios';
  return /Android/i.test(userAgent) ? 'android' : null;
};

export const LoginPage: React.FC<LoginPageProps> = ({ onBackToLanding, onAuthenticated }) => {
  const { login, loginMfa, loginRecovery } = useAuth();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [mfaCode, setMfaCode] = useState('');
  const [requiresMfa, setRequiresMfa] = useState(false);
  const [useRecoveryCode, setUseRecoveryCode] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [installPlatform, setInstallPlatform] = useState<MobileInstallPlatform>(null);
  const [deferredInstallPrompt, setDeferredInstallPrompt] = useState<DeferredInstallPrompt | null>(null);
  const [showAndroidInstallHelp, setShowAndroidInstallHelp] = useState(false);

  useEffect(() => {
    if (isPwaStandalone()) return undefined;
    setInstallPlatform(detectMobileInstallPlatform());

    const rememberInstallPrompt = (event: Event) => {
      event.preventDefault();
      setDeferredInstallPrompt(event as DeferredInstallPrompt);
    };
    const clearInstallPrompt = () => {
      setInstallPlatform(null);
      setDeferredInstallPrompt(null);
      setShowAndroidInstallHelp(false);
    };

    window.addEventListener('beforeinstallprompt', rememberInstallPrompt);
    window.addEventListener('appinstalled', clearInstallPrompt);
    return () => {
      window.removeEventListener('beforeinstallprompt', rememberInstallPrompt);
      window.removeEventListener('appinstalled', clearInstallPrompt);
    };
  }, []);

  const installAndroidApp = async () => {
    if (!deferredInstallPrompt) {
      setShowAndroidInstallHelp(true);
      return;
    }
    await deferredInstallPrompt.prompt();
    const result = await deferredInstallPrompt.userChoice;
    setDeferredInstallPrompt(null);
    if (result.outcome === 'accepted') setInstallPlatform(null);
    else setShowAndroidInstallHelp(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      if (requiresMfa) {
        if (useRecoveryCode) await loginRecovery(mfaCode);
        else await loginMfa(mfaCode);
        onAuthenticated?.();
      } else {
        const status = await login(username, password);
        if (status === 'RequiresTwoFactor') setRequiresMfa(true);
        else onAuthenticated?.();
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Falha na autenticação. Verifique suas credenciais.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-slate-950 flex flex-col justify-center items-center p-6 relative">
      <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-96 h-96 bg-rose-600/10 rounded-full blur-3xl pointer-events-none"></div>

      <div className="w-full max-w-md space-y-6 relative z-10">
        <div className="text-center space-y-2">
          <div className="theme-on-accent inline-flex w-12 h-12 rounded-2xl bg-gradient-to-tr from-rose-700 to-rose-400 items-center justify-center shadow-lg shadow-rose-600/30 mb-2">
            <Sparkles className="w-6 h-6 text-white" />
          </div>
          <h1 className="font-display text-2xl font-bold text-white tracking-tight">PAINEL<span className="text-rose-500">ESTÉTICA</span></h1>
          <p className="text-xs text-slate-400">Área Administrativa Restrita & Prontuários</p>
        </div>

        <Card className="glass-panel border-rose-500/30">
          <form onSubmit={handleSubmit} className="space-y-4">
            {error && (
              <div className="p-3 bg-rose-500/10 border border-rose-500/30 rounded-lg text-xs text-rose-400 flex items-center gap-2">
                <ShieldAlert className="w-4 h-4 text-rose-400 flex-shrink-0" />
                <span>{error}</span>
              </div>
            )}

            {requiresMfa ? (
              <>
                <div className="rounded-lg border border-amber-500/30 bg-amber-500/10 p-3 text-xs text-amber-200">
                  {useRecoveryCode
                    ? 'Informe um dos códigos de recuperação salvos ao ativar o MFA. Ele será invalidado após o uso.'
                    : 'Digite o código de seis números gerado pelo seu aplicativo autenticador.'}
                </div>
                <Input
                  label={useRecoveryCode ? 'Código de recuperação' : 'Código de autenticação'}
                  inputMode={useRecoveryCode ? 'text' : 'numeric'}
                  autoComplete={useRecoveryCode ? 'off' : 'one-time-code'}
                  placeholder={useRecoveryCode ? 'XXXXX-XXXXX' : '000000'}
                  required
                  value={mfaCode}
                  onChange={e => setMfaCode(e.target.value)}
                />
              </>
            ) : (
              <>
                <Input
                  label="Usuário"
                  autoComplete="username"
                  placeholder="Digite seu usuário..."
                  required
                  value={username}
                  onChange={e => setUsername(e.target.value)}
                />
                <Input
                  label="Senha de Acesso"
                  type="password"
                  autoComplete="current-password"
                  placeholder="••••••••"
                  required
                  value={password}
                  onChange={e => setPassword(e.target.value)}
                />
              </>
            )}

            <Button type="submit" variant="primary" size="lg" className="w-full" disabled={loading}>
              {loading ? 'Autenticando...' : requiresMfa ? 'Confirmar código' : 'Entrar no painel'}
            </Button>

            {requiresMfa && (
              <div className="space-y-3">
                <button
                  type="button"
                  className="w-full text-xs text-sky-400 hover:text-sky-300"
                  onClick={() => { setUseRecoveryCode(value => !value); setMfaCode(''); }}
                >
                  {useRecoveryCode ? 'Usar aplicativo autenticador' : 'Usar código de recuperação'}
                </button>
                <button
                  type="button"
                  className="w-full text-xs text-slate-400 hover:text-white"
                  onClick={() => { setRequiresMfa(false); setUseRecoveryCode(false); setMfaCode(''); setPassword(''); }}
                >
                  Voltar e informar outra conta
                </button>
              </div>
            )}
          </form>
        </Card>

        <div className="text-center">
          <button onClick={onBackToLanding} className="text-xs text-slate-400 hover:text-white transition-colors">
            ← Voltar para a Landing Page Pública
          </button>
        </div>

        {installPlatform === 'android' && (
          <div className="mx-auto max-w-xs text-center lg:hidden">
            <button
              type="button"
              onClick={() => void installAndroidApp()}
              className="mx-auto flex items-center gap-1.5 text-[11px] text-slate-500 transition-colors hover:text-rose-300"
            >
              <Download className="h-3.5 w-3.5" /> Instalar aplicativo neste dispositivo
            </button>
            {showAndroidInstallHelp && (
              <p className="mt-1.5 text-[10px] leading-relaxed text-slate-500">
                No menu ⋮ do navegador, escolha “Instalar aplicativo” ou “Adicionar à tela inicial”.
              </p>
            )}
          </div>
        )}

        {installPlatform === 'ios' && (
          <details className="mx-auto max-w-xs rounded-lg border border-slate-800 bg-slate-900/45 px-3 py-2 text-left text-[11px] text-slate-400 lg:hidden">
            <summary className="flex cursor-pointer list-none items-center gap-1.5 font-medium text-slate-300 marker:hidden">
              <Share className="h-3.5 w-3.5 text-rose-400" /> Instalar aplicativo no iPhone ou iPad
            </summary>
            <ol className="mt-2 list-decimal space-y-1 pl-4 leading-relaxed text-slate-500">
              <li>Toque em Compartilhar <Share className="mx-0.5 inline h-3 w-3" /> no navegador.</li>
              <li>Escolha “Adicionar à Tela de Início”.</li>
              <li>Confirme em “Adicionar”.</li>
            </ol>
          </details>
        )}
      </div>
    </div>
  );
};
