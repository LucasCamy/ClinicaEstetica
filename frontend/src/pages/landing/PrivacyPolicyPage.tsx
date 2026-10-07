import {
  ArrowLeft,
  Calendar,
  CheckCircle2,
  ChevronRight,
  ExternalLink,
  Eye,
  FileCheck,
  FileText,
  Lock,
  MessageCircle,
  RefreshCw,
  Server,
  Shield,
  ShieldAlert,
  ShieldCheck,
  Smartphone,
  UserCheck
} from 'lucide-react';
import React, { useEffect, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { Footer, Navbar, defaultCmsContent } from '../../components/landing/LandingComponents';
import { api } from '../../services/api';
import { CmsContent } from '../../types';

export const PrivacyPolicyPage: React.FC<{ onOpenAdmin: () => void }> = ({ onOpenAdmin }) => {
  const [cms, setCms] = useState<CmsContent>(defaultCmsContent);
  const location = useLocation();

  useEffect(() => {
    api.getCms()
      .then(loadedCms => {
        if (loadedCms) {
          setCms(loadedCms);
        }
      })
      .catch(() => {
        // Mantém defaultCmsContent se houver indisponibilidade temporária
      });
  }, []);

  useEffect(() => {
    if (location.hash) {
      const element = document.getElementById(location.hash.replace('#', ''));
      if (element) {
        element.scrollIntoView({ behavior: 'smooth' });
        return;
      }
    }
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }, [location.hash]);

  const clinicName = cms.doctorName ? `${cms.doctorName} — Estética Avançada` : 'Clínica de Estética Avançada';
  const lastUpdated = '09 de março de 2026';

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 font-sans selection:bg-rose-500 selection:text-white">
      <Navbar cms={cms} onOpenAdmin={onOpenAdmin} />

      {/* HEADER HERO */}
      <header className="relative overflow-hidden pt-28 pb-12 sm:pt-36 sm:pb-16 border-b border-slate-800/80">
        <div className="pointer-events-none absolute left-1/2 top-10 h-96 w-96 -translate-x-1/2 rounded-full bg-rose-600/10 blur-[120px]" />

        <div className="relative z-10 mx-auto max-w-5xl px-4 sm:px-6 lg:px-8">
          {/* Breadcrumb & switcher */}
          <div className="flex flex-wrap items-center justify-between gap-4 mb-6">
            <Link
              to="/"
              className="inline-flex items-center gap-1.5 text-xs font-medium text-slate-400 hover:text-rose-400 transition-colors"
            >
              <ArrowLeft className="w-3.5 h-3.5" /> Voltar ao Início
            </Link>

            <div className="inline-flex rounded-full border border-slate-800 bg-slate-900/80 p-1 text-xs font-medium">
              <span className="rounded-full bg-rose-500 px-3.5 py-1 text-white shadow-sm font-semibold">
                Política de Privacidade
              </span>
              <Link
                to="/termos"
                className="rounded-full px-3.5 py-1 text-slate-400 hover:text-rose-300 transition-colors"
              >
                Termos de Serviço
              </Link>
            </div>
          </div>

          <div className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full bg-rose-500/10 border border-rose-500/20 text-rose-400 text-xs font-semibold uppercase tracking-wider mb-4">
            <ShieldCheck className="w-4 h-4" /> Privacidade & Proteção de Dados
          </div>

          <h1 className="font-display text-3xl sm:text-4xl lg:text-5xl font-bold text-white tracking-tight">
            Política de Privacidade
          </h1>

          <p className="mt-4 text-base sm:text-lg text-slate-300 max-w-3xl leading-relaxed">
            Transparência e segurança no tratamento de seus dados pessoais. Este documento explica detalhadamente como a{' '}
            <strong className="text-white font-semibold">{clinicName}</strong> e a plataforma{' '}
            <strong className="text-white font-semibold">PainelEstetica</strong> tratam, protegem e respeitam suas informações
            em conformidade com a <strong className="text-rose-300 font-semibold">LGPD (Lei Geral de Proteção de Dados nº 13.709/2018)</strong> e
            com a <strong className="text-rose-300 font-semibold">Google API Services User Data Policy</strong>.
          </p>

          <div className="mt-6 flex flex-wrap items-center gap-4 text-xs text-slate-400">
            <span className="inline-flex items-center gap-1.5">
              <Calendar className="w-3.5 h-3.5 text-rose-400" />
              Última atualização: {lastUpdated}
            </span>
            <span>•</span>
            <span className="inline-flex items-center gap-1.5">
              <CheckCircle2 className="w-3.5 h-3.5 text-emerald-400" />
              Versão 1.0 Oficial
            </span>
            <span>•</span>
            <span>Documento Público</span>
          </div>
        </div>
      </header>

      {/* CONTEÚDO PRINCIPAL */}
      <main className="mx-auto max-w-5xl px-4 sm:px-6 lg:px-8 py-12 sm:py-16">
        <div className="space-y-12 text-slate-300 leading-relaxed text-sm sm:text-base">

          {/* BOX DESTAQUE GOOGLE USER DATA POLICY */}
          <div
            id="google-data-policy"
            className="rounded-3xl border-2 border-rose-500/40 bg-gradient-to-br from-rose-950/40 via-slate-900/90 to-slate-900/90 p-6 sm:p-8 shadow-2xl backdrop-blur-md"
          >
            <div className="flex items-start gap-4">
              <div className="p-3 rounded-2xl bg-rose-500/20 text-rose-400 border border-rose-500/30 shrink-0">
                <Calendar className="w-7 h-7" />
              </div>
              <div className="space-y-3">
                <span className="inline-block text-rose-400 text-xs font-bold uppercase tracking-widest">
                  Google API Services User Data Policy & Limited Use
                </span>
                <h2 className="font-display text-xl sm:text-2xl font-bold text-white">
                  Declaração de Uso Limitado das APIs do Google
                </h2>
                <p className="text-sm sm:text-base text-slate-200 leading-relaxed">
                  O uso e a transferência para qualquer outro aplicativo de informações recebidas de APIs do Google pela
                  plataforma <strong className="text-white">PainelEstetica / {clinicName}</strong> cumprirão estritamente a{' '}
                  <a
                    href="https://developers.google.com/terms/api-services-user-data-policy"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-rose-400 underline decoration-rose-500/50 underline-offset-4 hover:text-rose-300 font-semibold inline-flex items-center gap-1"
                  >
                    Google API Services User Data Policy
                    <ExternalLink className="w-3.5 h-3.5 inline" />
                  </a>
                  , incluindo os requisitos de <strong className="text-white">Uso Limitado (Limited Use requirements)</strong>.
                </p>
                <div className="grid sm:grid-cols-2 gap-3 pt-2 text-xs sm:text-sm">
                  <div className="rounded-xl border border-rose-500/20 bg-rose-950/30 p-3 text-slate-300">
                    <strong className="text-rose-200 block mb-1">✓ Sem Finalidade Publicitária</strong>
                    Os dados obtidos do Google Calendar nunca são utilizados para anúncios ou perfil publicitário.
                  </div>
                  <div className="rounded-xl border border-rose-500/20 bg-rose-950/30 p-3 text-slate-300">
                    <strong className="text-rose-200 block mb-1">✓ Sem Treinamento de IA Geral</strong>
                    Dados do Google não são compartilhados nem empregados no treinamento de modelos de inteligência artificial ou ML generalistas.
                  </div>
                  <div className="rounded-xl border border-rose-500/20 bg-rose-950/30 p-3 text-slate-300">
                    <strong className="text-rose-200 block mb-1">✓ Sem Comercialização</strong>
                    Nenhuma informação da sua conta Google ou da sua agenda é comercializada, alugada ou cedida a terceiros.
                  </div>
                  <div className="rounded-xl border border-rose-500/20 bg-rose-950/30 p-3 text-slate-300">
                    <strong className="text-rose-200 block mb-1">✓ Revogação a Qualquer Momento</strong>
                    Você pode desconectar a integração no painel administrativo ou diretamente nas permissões da sua Conta Google.
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* SUMÁRIO RÁPIDO */}
          <nav aria-label="Sumário da Política" className="rounded-2xl border border-slate-800 bg-slate-900/40 p-5">
            <h3 className="text-xs font-bold uppercase tracking-wider text-slate-400 mb-3">Navegação Rápida</h3>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2 text-xs">
              <a href="#controlador" className="text-slate-300 hover:text-rose-400 transition-colors flex items-center gap-1.5">
                <ChevronRight className="w-3.5 h-3.5 text-rose-500" /> 1. Controlador de Dados
              </a>
              <a href="#dados-coletados" className="text-slate-300 hover:text-rose-400 transition-colors flex items-center gap-1.5">
                <ChevronRight className="w-3.5 h-3.5 text-rose-500" /> 2. Dados Coletados
              </a>
              <a href="#finalidades" className="text-slate-300 hover:text-rose-400 transition-colors flex items-center gap-1.5">
                <ChevronRight className="w-3.5 h-3.5 text-rose-500" /> 3. Finalidades do Tratamento
              </a>
              <a href="#google-calendar" className="text-slate-300 hover:text-rose-400 transition-colors flex items-center gap-1.5">
                <ChevronRight className="w-3.5 h-3.5 text-rose-500" /> 4. Integração Google Calendar
              </a>
              <a href="#seguranca" className="text-slate-300 hover:text-rose-400 transition-colors flex items-center gap-1.5">
                <ChevronRight className="w-3.5 h-3.5 text-rose-500" /> 5. Segurança & Armazenamento
              </a>
              <a href="#direitos-lgpd" className="text-slate-300 hover:text-rose-400 transition-colors flex items-center gap-1.5">
                <ChevronRight className="w-3.5 h-3.5 text-rose-500" /> 6. Direitos do Titular (LGPD)
              </a>
              <a href="#revogacao" className="text-slate-300 hover:text-rose-400 transition-colors flex items-center gap-1.5">
                <ChevronRight className="w-3.5 h-3.5 text-rose-500" /> 7. Revogação de Acesso
              </a>
              <a href="#contato-dpo" className="text-slate-300 hover:text-rose-400 transition-colors flex items-center gap-1.5">
                <ChevronRight className="w-3.5 h-3.5 text-rose-500" /> 8. Canal de Contato & DPO
              </a>
              <a href="#atualizacoes" className="text-slate-300 hover:text-rose-400 transition-colors flex items-center gap-1.5">
                <ChevronRight className="w-3.5 h-3.5 text-rose-500" /> 9. Atualizações desta Política
              </a>
            </div>
          </nav>

          {/* 1. CONTROLADOR DE DADOS */}
          <section id="controlador" className="space-y-4 pt-4">
            <h2 className="font-display text-2xl font-bold text-white flex items-center gap-3">
              <span className="p-2 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20 text-sm">01</span>
              Identificação do Controlador de Dados
            </h2>
            <p>
              O controlador dos dados pessoais tratados por este serviço é a clínica estritamente identificada como{' '}
              <strong className="text-white">{clinicName}</strong>, com endereço comercial em{' '}
              <span className="text-slate-200">{cms.addressText || 'São Paulo/SP'}</span> e atendimento pelo WhatsApp{' '}
              <span className="text-slate-200">{cms.whatsappNumber}</span>.
            </p>
            <p>
              A aplicação <strong className="text-white">PainelEstetica</strong> compreende a landing page pública de apresentação de tratamentos e
              avaliações, bem como o software administrativo utilizado exclusivamente pelos profissionais e colaboradores autorizados da clínica
              para organização de consultas, prontuários estéticos e sincronização de calendários operacionais.
            </p>
          </section>

          {/* 2. QUAIS DADOS COLETAMOS */}
          <section id="dados-coletados" className="space-y-4 pt-4">
            <h2 className="font-display text-2xl font-bold text-white flex items-center gap-3">
              <span className="p-2 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20 text-sm">02</span>
              Quais Dados Coletamos
            </h2>
            <p>
              Coletamos apenas os dados estritamente necessários para viabilizar o atendimento estético, o agendamento de consultas e o correto
              gerenciamento operacional:
            </p>

            <div className="grid md:grid-cols-3 gap-4 pt-2">
              <div className="rounded-2xl border border-slate-800 bg-slate-900/40 p-5 space-y-2">
                <div className="flex items-center gap-2 text-rose-400 font-semibold text-sm">
                  <UserCheck className="w-4 h-4" />
                  Pacientes & Interessados
                </div>
                <ul className="text-xs text-slate-300 space-y-1.5 list-disc list-inside">
                  <li>Nome completo;</li>
                  <li>Telefone / WhatsApp;</li>
                  <li>Endereço de e-mail;</li>
                  <li>Tratamento de interesse e mensagens de avaliação;</li>
                  <li>Histórico clínico, fichas de anamnese e termos assinados (TCLE);</li>
                  <li>Registros fotográficos clínicos pré e pós-procedimento devidamente consentidos.</li>
                </ul>
              </div>

              <div className="rounded-2xl border border-slate-800 bg-slate-900/40 p-5 space-y-2">
                <div className="flex items-center gap-2 text-rose-400 font-semibold text-sm">
                  <Lock className="w-4 h-4" />
                  Profissionais da Clínica
                </div>
                <ul className="text-xs text-slate-300 space-y-1.5 list-disc list-inside">
                  <li>Nome de usuário e e-mail corporativo;</li>
                  <li>Credenciais criptografadas (hashes seguros);</li>
                  <li>Chaves de autenticação de dois fatores (MFA/TOTP);</li>
                  <li>Papéis, permissões de acesso e registros de log de ações operacionais.</li>
                </ul>
              </div>

              <div className="rounded-2xl border border-slate-800 bg-slate-900/40 p-5 space-y-2">
                <div className="flex items-center gap-2 text-rose-400 font-semibold text-sm">
                  <Calendar className="w-4 h-4" />
                  Integração Google
                </div>
                <ul className="text-xs text-slate-300 space-y-1.5 list-disc list-inside">
                  <li>Identificador de conta Google e e-mail vinculado;</li>
                  <li>Identificadores de calendários selecionados pelo profissional;</li>
                  <li>Horários, títulos e status de eventos da agenda Google para verificação de conflitos e criação de agendamentos de procedimentos.</li>
                </ul>
              </div>
            </div>
          </section>

          {/* 3. FINALIDADES DO TRATAMENTO */}
          <section id="finalidades" className="space-y-4 pt-4">
            <h2 className="font-display text-2xl font-bold text-white flex items-center gap-3">
              <span className="p-2 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20 text-sm">03</span>
              Finalidades do Tratamento de Dados
            </h2>
            <p>Tratamos as informações pessoais com fundamentos jurídicos legítimos previstos no Art. 7º e Art. 11 da LGPD:</p>
            <ul className="grid sm:grid-cols-2 gap-3 text-xs sm:text-sm">
              <li className="rounded-xl border border-slate-800/80 bg-slate-900/40 p-3.5">
                <strong className="text-white block mb-1">1. Execução de Procedimentos e Contrato</strong>
                Realização de consultas de avaliação, aplicação de tratamentos estéticos e acompanhamento evolutivo individualizado.
              </li>
              <li className="rounded-xl border border-slate-800/80 bg-slate-900/40 p-3.5">
                <strong className="text-white block mb-1">2. Gestão de Agenda & Notificações</strong>
                Agendamento de horários, confirmações e avisos de comparecimento via WhatsApp ou e-mail de forma operacional.
              </li>
              <li className="rounded-xl border border-slate-800/80 bg-slate-900/40 p-3.5">
                <strong className="text-white block mb-1">3. Tutela da Saúde & Biossegurança</strong>
                Preenchimento de anamnese para verificação de contraindicações médicas, alergias e cuidados pré e pós-procedimento.
              </li>
              <li className="rounded-xl border border-slate-800/80 bg-slate-900/40 p-3.5">
                <strong className="text-white block mb-1">4. Cumprimento de Obrigações Legais</strong>
                Guarda de registros médicos/estéticos e emissão de documentações fiscais e de consentimento obrigatórias.
              </li>
            </ul>
          </section>

          {/* 4. INTEGRAÇÃO COM GOOGLE CALENDAR */}
          <section id="google-calendar" className="space-y-4 pt-4">
            <h2 className="font-display text-2xl font-bold text-white flex items-center gap-3">
              <span className="p-2 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20 text-sm">04</span>
              Integração com o Google Calendar & Política de Dados de Usuários Google
            </h2>
            <p>
              A aplicação oferece aos profissionais da clínica a funcionalidade de vincular sua agenda do Google Calendar para
              sincronização bidirecional ou leitura de horários ocupados, assegurando que não ocorram marcações conflitantes.
            </p>

            <div className="space-y-3 bg-slate-900/60 border border-slate-800 rounded-2xl p-5">
              <h3 className="font-semibold text-white text-base">Escopos Solicitados e Como São Usados:</h3>
              <p className="text-xs sm:text-sm text-slate-300">
                Utilizamos o protocolo padrão <strong className="text-white">OAuth 2.0</strong> do Google. Quando autorizado pelo profissional,
                solicitamos escopos para acesso à agenda (<code className="text-rose-300 text-xs">calendar.events</code> e/ou <code className="text-rose-300 text-xs">calendar</code>).
              </p>
              <ul className="text-xs sm:text-sm text-slate-300 space-y-2 list-disc list-inside">
                <li>
                  <strong className="text-slate-100">Leitura de eventos:</strong> Apenas para identificar períodos de indisponibilidade na agenda
                  do profissional e exibir no painel interno.
                </li>
                <li>
                  <strong className="text-slate-100">Criação/Atualização de eventos:</strong> Quando um novo agendamento de procedimento estético é
                  criado no PainelEstetica, o evento correspondente é registrado na agenda Google selecionada com data, horário e tipo de procedimento.
                </li>
              </ul>
            </div>

            <div className="rounded-2xl border border-rose-500/30 bg-rose-950/20 p-5 space-y-2">
              <h3 className="font-semibold text-rose-200 text-base">Compromisso Específico de Uso Limitado (Limited Use):</h3>
              <p className="text-xs sm:text-sm text-slate-200">
                Reiteramos que a utilização dos dados obtidos por meio das APIs do Google limita-se exclusivamente a fornecer e aprimorar a
                funcionalidade visível ao usuário de sincronização de agendamentos. A clínica e o PainelEstetica:
              </p>
              <ul className="text-xs sm:text-sm text-slate-200 space-y-1 list-disc list-inside pl-1">
                <li>NÃO compartilham nem transferem dados da API do Google para terceiros, exceto quando estritamente necessário para prestar ou melhorar o serviço sob as regras do Google;</li>
                <li>NÃO utilizam dados da API do Google para anúncios, publicidade direcionada ou criação de perfis;</li>
                <li>NÃO permitem que humanos leiam esses dados, exceto mediante consentimento expresso para fins de suporte técnico, por exigência legal ou para investigações de segurança interna com dados agregados;</li>
                <li>NÃO usam esses dados para treinar modelos de inteligência artificial ou machine learning generalistas.</li>
              </ul>
            </div>
          </section>

          {/* 5. SEGURANÇA E ARMAZENAMENTO */}
          <section id="seguranca" className="space-y-4 pt-4">
            <h2 className="font-display text-2xl font-bold text-white flex items-center gap-3">
              <span className="p-2 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20 text-sm">05</span>
              Segurança e Armazenamento da Informação
            </h2>
            <p>
              Adotamos medidas técnicas, administrativas e organizacionais de segurança da informação compatíveis com as melhores práticas
              de proteção de dados:
            </p>
            <div className="grid sm:grid-cols-2 gap-4 text-xs sm:text-sm">
              <div className="p-4 rounded-xl border border-slate-800 bg-slate-900/40">
                <div className="flex items-center gap-2 text-rose-400 font-semibold mb-1">
                  <Lock className="w-4 h-4" /> Criptografia de Ponta a Ponta
                </div>
                Todo o tráfego é criptografado utilizando HTTPS/TLS 1.3 em trânsito. Tokens sensíveis de autenticação e credenciais de integração
                são criptografados em repouso por mecanismos protegidos (DPAPI / criptografia de chaves).
              </div>
              <div className="p-4 rounded-xl border border-slate-800 bg-slate-900/40">
                <div className="flex items-center gap-2 text-rose-400 font-semibold mb-1">
                  <ShieldAlert className="w-4 h-4" /> Controle Rigoroso de Acesso
                </div>
                Acesso baseado em funções (RBAC), exigência de senhas fortes, proteção contra ataques de força bruta, autenticação de dois fatores
                (MFA/TOTP) e registro contínuo de auditoria para visualização de prontuários.
              </div>
            </div>
          </section>

          {/* 6. DIREITOS DO TITULAR (LGPD) */}
          <section id="direitos-lgpd" className="space-y-4 pt-4">
            <h2 className="font-display text-2xl font-bold text-white flex items-center gap-3">
              <span className="p-2 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20 text-sm">06</span>
              Seus Direitos como Titular de Dados (LGPD - Art. 18)
            </h2>
            <p>
              Em conformidade com a Lei Geral de Proteção de Dados (Lei nº 13.709/2018), você possui direitos fundamentais em relação aos
              seus dados pessoais:
            </p>
            <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-3 text-xs">
              <div className="p-3.5 rounded-xl border border-slate-800 bg-slate-900/30">
                <strong className="text-white block mb-1">Confirmação & Acesso</strong>
                Direito de saber se realizamos o tratamento e obter cópia dos seus dados cadastrais e prontuário.
              </div>
              <div className="p-3.5 rounded-xl border border-slate-800 bg-slate-900/30">
                <strong className="text-white block mb-1">Correção</strong>
                Direito de solicitar a atualização de informações incompletas, inexatas ou desatualizadas.
              </div>
              <div className="p-3.5 rounded-xl border border-slate-800 bg-slate-900/30">
                <strong className="text-white block mb-1">Eliminação & Revogação</strong>
                Direito de revogar seu consentimento e solicitar a exclusão de dados não obrigatórios por lei clínica/fiscal.
              </div>
              <div className="p-3.5 rounded-xl border border-slate-800 bg-slate-900/30">
                <strong className="text-white block mb-1">Anonimização & Bloqueio</strong>
                Direito de solicitar a anonimização de dados desnecessários ou excessivos.
              </div>
              <div className="p-3.5 rounded-xl border border-slate-800 bg-slate-900/30">
                <strong className="text-white block mb-1">Portabilidade</strong>
                Direito de solicitar a transferência de seus dados clínicos ou cadastrais a outro prestador.
              </div>
              <div className="p-3.5 rounded-xl border border-slate-800 bg-slate-900/30">
                <strong className="text-white block mb-1">Informação sobre Compartilhamento</strong>
                Saber com quais entidades públicas ou privadas compartilhamos dados para fins regulatórios.
              </div>
            </div>
          </section>

          {/* 7. REVOGAÇÃO DE ACESSO */}
          <section id="revogacao" className="space-y-4 pt-4">
            <h2 className="font-display text-2xl font-bold text-white flex items-center gap-3">
              <span className="p-2 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20 text-sm">07</span>
              Como Revogar o Acesso e Desconectar Integrações
            </h2>
            <p>
              Você mantém o controle total sobre suas contas e autorizações. Para revogar permissões concedidas:
            </p>
            <div className="space-y-3 text-xs sm:text-sm">
              <div className="p-4 rounded-xl border border-slate-800 bg-slate-900/40">
                <strong className="text-white block mb-1">1. Desconexão do Google Calendar pelo Painel:</strong>
                O usuário autorizado pode acessar <strong className="text-slate-200">Configurações &gt; Integrações</strong> dentro do PainelEstetica e
                clicar em <span className="text-rose-400 font-semibold">"Desconectar Google Calendar"</span>. Os tokens locais são imediatamente invalidados e apagados.
              </div>
              <div className="p-4 rounded-xl border border-slate-800 bg-slate-900/40">
                <strong className="text-white block mb-1">2. Revogação Direta na sua Conta Google:</strong>
                Você pode revogar a permissão do aplicativo a qualquer momento através do painel de segurança do Google pelo link oficial:{' '}
                <a
                  href="https://myaccount.google.com/permissions"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-rose-400 underline hover:text-rose-300 font-medium inline-flex items-center gap-1"
                >
                  https://myaccount.google.com/permissions
                  <ExternalLink className="w-3 h-3 inline" />
                </a>.
              </div>
              <div className="p-4 rounded-xl border border-slate-800 bg-slate-900/40">
                <strong className="text-white block mb-1">3. Cancelamento de Notificações de WhatsApp:</strong>
                O paciente pode cancelar a qualquer momento o recebimento de mensagens e lembretes respondendo "SAIR" ou solicitando via WhatsApp à nossa equipe.
              </div>
            </div>
          </section>

          {/* 8. CONTATO & DPO */}
          <section id="contato-dpo" className="space-y-4 pt-4">
            <h2 className="font-display text-2xl font-bold text-white flex items-center gap-3">
              <span className="p-2 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20 text-sm">08</span>
              Canal de Atendimento e Encarregado de Proteção de Dados (DPO)
            </h2>
            <p>
              Para exercer quaisquer dos seus direitos de titular, tirar dúvidas sobre o tratamento de seus dados ou solicitar esclarecimentos adicionais,
              entre em contato conosco:
            </p>
            <div className="rounded-2xl border border-slate-800 bg-slate-900/60 p-5 sm:p-6 space-y-3 text-xs sm:text-sm">
              <div className="flex items-center gap-2 text-slate-200">
                <UserCheck className="w-4 h-4 text-rose-400" />
                <span><strong>Encarregado / Responsável:</strong> {cms.doctorName || 'Dra. Leilaine Arakaki'}</span>
              </div>
              <div className="flex items-center gap-2 text-slate-200">
                <MessageCircle className="w-4 h-4 text-rose-400" />
                <span>
                  <strong>WhatsApp Oficial:</strong>{' '}
                  <a
                    href={`https://wa.me/${cms.whatsappNumber}`}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-rose-400 underline hover:text-rose-300"
                  >
                    {cms.whatsappNumber}
                  </a>
                </span>
              </div>
              <div className="flex items-start gap-2 text-slate-200">
                <Server className="w-4 h-4 text-rose-400 shrink-0 mt-0.5" />
                <span><strong>Endereço da Clínica:</strong> {cms.addressText}</span>
              </div>
            </div>
          </section>

          {/* 9. ATUALIZAÇÕES DESTA POLÍTICA */}
          <section id="atualizacoes" className="space-y-4 pt-4">
            <h2 className="font-display text-2xl font-bold text-white flex items-center gap-3">
              <span className="p-2 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20 text-sm">09</span>
              Atualizações desta Política
            </h2>
            <p>
              Esta Política de Privacidade pode ser revisada periodicamente para refletir melhorias no sistema, mudanças legislativas ou atualizações
              nas políticas de plataformas parceiras (como o Google). Toda alteração será publicada nesta página com a respectiva data de atualização.
            </p>
          </section>

          {/* BOTTOM CTA BAR */}
          <div className="pt-8 border-t border-slate-800 flex flex-col sm:flex-row items-center justify-between gap-4">
            <Link
              to="/termos"
              className="inline-flex items-center gap-2 text-sm text-rose-400 hover:text-rose-300 transition-colors font-medium"
            >
              Conheça também nossos Termos de Serviço <ChevronRight className="w-4 h-4" />
            </Link>

            <Link
              to="/"
              className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-slate-900 border border-slate-700 text-sm text-white hover:border-rose-500/60 hover:text-rose-300 transition-colors"
            >
              <ArrowLeft className="w-4 h-4" /> Voltar para a Página Inicial
            </Link>
          </div>

        </div>
      </main>

      <Footer cms={cms} />
    </div>
  );
};
