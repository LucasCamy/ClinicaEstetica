import {
  AlertTriangle,
  ArrowLeft,
  Calendar,
  CheckCircle2,
  ChevronRight,
  ExternalLink,
  FileCheck,
  FileText,
  Gavel,
  HelpCircle,
  Lock,
  MessageCircle,
  Scale,
  ShieldCheck,
  Sparkles,
  UserCheck
} from 'lucide-react';
import React, { useEffect, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { Footer, Navbar, defaultCmsContent } from '../../components/landing/LandingComponents';
import { api } from '../../services/api';
import { CmsContent } from '../../types';

export const TermsOfServicePage: React.FC<{ onOpenAdmin: () => void }> = ({ onOpenAdmin }) => {
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
              <Link
                to="/privacidade"
                className="rounded-full px-3.5 py-1 text-slate-400 hover:text-rose-300 transition-colors"
              >
                Política de Privacidade
              </Link>
              <span className="rounded-full bg-rose-500 px-3.5 py-1 text-white shadow-sm font-semibold">
                Termos de Serviço
              </span>
            </div>
          </div>

          <div className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full bg-rose-500/10 border border-rose-500/20 text-rose-400 text-xs font-semibold uppercase tracking-wider mb-4">
            <Scale className="w-4 h-4" /> Termos de Uso & Condições Gerais
          </div>

          <h1 className="font-display text-3xl sm:text-4xl lg:text-5xl font-bold text-white tracking-tight">
            Termos de Serviço
          </h1>

          <p className="mt-4 text-base sm:text-lg text-slate-300 max-w-3xl leading-relaxed">
            Bem-vindo(a) à plataforma da <strong className="text-white font-semibold">{clinicName}</strong> e ao sistema{' '}
            <strong className="text-white font-semibold">PainelEstetica</strong>. Este documento estabelece as condições gerais, direitos e
            responsabilidades que regem a navegação em nosso website, a solicitação de agendamentos estéticos e o uso de nossos serviços.
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

          {/* BOX DESTAQUE ÉTICO & CLÍNICO */}
          <div className="rounded-3xl border border-rose-500/30 bg-gradient-to-br from-rose-950/30 via-slate-900/80 to-slate-900/80 p-6 sm:p-8 shadow-2xl backdrop-blur-md">
            <div className="flex items-start gap-4">
              <div className="p-3 rounded-2xl bg-rose-500/20 text-rose-400 border border-rose-500/30 shrink-0">
                <Sparkles className="w-7 h-7" />
              </div>
              <div className="space-y-2">
                <span className="inline-block text-rose-400 text-xs font-bold uppercase tracking-widest">
                  Atendimento Responsável & Prática Estética Ética
                </span>
                <h2 className="font-display text-xl sm:text-2xl font-bold text-white">
                  Compromisso com a Biossegurança e Saúde do Paciente
                </h2>
                <p className="text-xs sm:text-sm text-slate-200">
                  Os procedimentos estéticos realizados em nossa clínica são executados por profissional especializada e devidamente habilitada.
                  Nenhum tratamento invasivo ou estético avançado é realizado sem avaliação prévia detalhada, confirmação de ausência de
                  contraindicações e consentimento informado por escrito.
                </p>
              </div>
            </div>
          </div>

          {/* SUMÁRIO RÁPIDO */}
          <nav aria-label="Sumário dos Termos" className="rounded-2xl border border-slate-800 bg-slate-900/40 p-5">
            <h3 className="text-xs font-bold uppercase tracking-wider text-slate-400 mb-3">Índice dos Termos</h3>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2 text-xs">
              <a href="#objeto" className="text-slate-300 hover:text-rose-400 transition-colors flex items-center gap-1.5">
                <ChevronRight className="w-3.5 h-3.5 text-rose-500" /> 1. Objeto & Aceitação
              </a>
              <a href="#servicos" className="text-slate-300 hover:text-rose-400 transition-colors flex items-center gap-1.5">
                <ChevronRight className="w-3.5 h-3.5 text-rose-500" /> 2. Descrição dos Serviços
              </a>
              <a href="#avaliacao-tcle" className="text-slate-300 hover:text-rose-400 transition-colors flex items-center gap-1.5">
                <ChevronRight className="w-3.5 h-3.5 text-rose-500" /> 3. Avaliação Prévia & TCLE
              </a>
              <a href="#anamnese" className="text-slate-300 hover:text-rose-400 transition-colors flex items-center gap-1.5">
                <ChevronRight className="w-3.5 h-3.5 text-rose-500" /> 4. Dever de Veracidade na Saúde
              </a>
              <a href="#agendamentos" className="text-slate-300 hover:text-rose-400 transition-colors flex items-center gap-1.5">
                <ChevronRight className="w-3.5 h-3.5 text-rose-500" /> 5. Agendamentos & Cancelamentos
              </a>
              <a href="#integracoes-google" className="text-slate-300 hover:text-rose-400 transition-colors flex items-center gap-1.5">
                <ChevronRight className="w-3.5 h-3.5 text-rose-500" /> 6. Google Calendar & Notificações
              </a>
              <a href="#propriedade-intelectual" className="text-slate-300 hover:text-rose-400 transition-colors flex items-center gap-1.5">
                <ChevronRight className="w-3.5 h-3.5 text-rose-500" /> 7. Propriedade Intelectual
              </a>
              <a href="#responsabilidade" className="text-slate-300 hover:text-rose-400 transition-colors flex items-center gap-1.5">
                <ChevronRight className="w-3.5 h-3.5 text-rose-500" /> 8. Limitação de Responsabilidade
              </a>
              <a href="#privacidade-termos" className="text-slate-300 hover:text-rose-400 transition-colors flex items-center gap-1.5">
                <ChevronRight className="w-3.5 h-3.5 text-rose-500" /> 9. Privacidade & LGPD
              </a>
              <a href="#legislacao-foro" className="text-slate-300 hover:text-rose-400 transition-colors flex items-center gap-1.5">
                <ChevronRight className="w-3.5 h-3.5 text-rose-500" /> 10. Legislação & Foro
              </a>
            </div>
          </nav>

          {/* 1. OBJETO & ACEITAÇÃO */}
          <section id="objeto" className="space-y-4 pt-4">
            <h2 className="font-display text-2xl font-bold text-white flex items-center gap-3">
              <span className="p-2 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20 text-sm">01</span>
              Objeto e Aceitação dos Termos
            </h2>
            <p>
              Estes Termos de Serviço regulam o acesso e a utilização dos serviços digitais da clínica{' '}
              <strong className="text-white">{clinicName}</strong> e da plataforma{' '}
              <strong className="text-white">PainelEstetica</strong>, abrangendo o website institucional, ferramentas de agendamento online,
              fichas cadastrais e o sistema administrativo de gestão clínica.
            </p>
            <p>
              Ao navegar pelo website, preencher formulários de contato, solicitar agendamento ou acessar o sistema administrativo, você declara
              ter lido, compreendido e concordado integralmente com estes Termos de Serviço e com nossa{' '}
              <Link to="/privacidade" className="text-rose-400 underline hover:text-rose-300 font-semibold">
                Política de Privacidade
              </Link>.
            </p>
          </section>

          {/* 2. DESCRIÇÃO DOS SERVIÇOS */}
          <section id="servicos" className="space-y-4 pt-4">
            <h2 className="font-display text-2xl font-bold text-white flex items-center gap-3">
              <span className="p-2 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20 text-sm">02</span>
              Descrição dos Serviços
            </h2>
            <p>
              A clínica disponibiliza através deste canal digital:
            </p>
            <div className="grid sm:grid-cols-2 gap-4 text-xs sm:text-sm">
              <div className="p-4 rounded-xl border border-slate-800 bg-slate-900/40 space-y-1">
                <strong className="text-white block">Catálogo de Procedimentos</strong>
                Apresentação detalhada de tratamentos faciais e corporais (ex.: Harmonização Facial, Bioestimuladores de Colágeno, Limpeza de Pele Fotônica, toxina botulínica, etc.).
              </div>
              <div className="p-4 rounded-xl border border-slate-800 bg-slate-900/40 space-y-1">
                <strong className="text-white block">Canal de Agendamento e Contato</strong>
                Formulário para envio de solicitações de avaliação estética individualizada e contato direto com a recepção da clínica via WhatsApp.
              </div>
              <div className="p-4 rounded-xl border border-slate-800 bg-slate-900/40 space-y-1">
                <strong className="text-white block">Prontuário Digital & Acompanhamento</strong>
                Ambiente restrito aos profissionais para manutenção segura de dados clínicos, evolução de tratamentos e fotos de acompanhamento.
              </div>
              <div className="p-4 rounded-xl border border-slate-800 bg-slate-900/40 space-y-1">
                <strong className="text-white block">Sincronização de Agenda</strong>
                Mecanismos de integração de calendário para gerenciamento eficiente de horários e prevenção de conflitos de atendimento.
              </div>
            </div>
          </section>

          {/* 3. AVALIAÇÃO PRÉVIA & TCLE */}
          <section id="avaliacao-tcle" className="space-y-4 pt-4">
            <h2 className="font-display text-2xl font-bold text-white flex items-center gap-3">
              <span className="p-2 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20 text-sm">03</span>
              Avaliação Prévia Obrigatória e Termo de Consentimento (TCLE)
            </h2>
            <p>
              A indicação, viabilidade e segurança de qualquer procedimento estético dependem obrigatoriamente de:
            </p>
            <ul className="text-xs sm:text-sm text-slate-300 space-y-2 list-disc list-inside bg-slate-900/40 border border-slate-800 rounded-2xl p-5">
              <li>
                <strong className="text-slate-100">Avaliação Clínica Individualizada:</strong> Realizada presencialmente pela profissional
                responsável para examinar o biótipo cutâneo, condições anatômicas e expectativas do cliente;
              </li>
              <li>
                <strong className="text-slate-100">Assinatura de Termo de Consentimento Livre e Esclarecido (TCLE):</strong> Documento formal
                em que o paciente toma ciência expressa das etapas do procedimento, cuidados necessários, eventuais efeitos temporários
                (como edema ou eritema) e orientações pós-tratamento;
              </li>
              <li>
                <strong className="text-slate-100">Autonomia Profissional:</strong> A clínica reserva-se o direito técnico e ético de recusar
                ou contraindicar tratamentos caso identifique riscos à saúde ou contraindicações médicas.
              </li>
            </ul>
          </section>

          {/* 4. DEVER DE VERACIDADE NA SAÚDE */}
          <section id="anamnese" className="space-y-4 pt-4">
            <h2 className="font-display text-2xl font-bold text-white flex items-center gap-3">
              <span className="p-2 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20 text-sm">04</span>
              Dever de Veracidade nas Informações de Saúde
            </h2>
            <p>
              O paciente ou interessado compromete-se a fornecer informações verdadeiras, precisas e completas ao preencher formulários e
              fichas de anamnese.
            </p>
            <div className="p-4 rounded-xl border border-rose-500/30 bg-rose-950/20 text-xs sm:text-sm text-rose-200 flex items-start gap-3">
              <AlertTriangle className="w-5 h-5 text-rose-400 shrink-0 mt-0.5" />
              <div>
                É dever impreterível do paciente informar previamente sobre gravidez, período de amamentação, alergias conhecidas, doenças
                autoimunes, uso de medicamentos contínuos (como anticoagulantes), próteses e procedimentos estéticos pregressos. A omissão de
                dados de saúde exime a equipe de intercorrências decorrentes de condições preexistentes não declaradas.
              </div>
            </div>
          </section>

          {/* 5. AGENDAMENTOS & CANCELAMENTOS */}
          <section id="agendamentos" className="space-y-4 pt-4">
            <h2 className="font-display text-2xl font-bold text-white flex items-center gap-3">
              <span className="p-2 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20 text-sm">05</span>
              Política de Agendamentos, Cancelamentos e Comparecimento
            </h2>
            <div className="space-y-3 text-xs sm:text-sm">
              <p>
                As solicitações de horário realizadas pelo website constituem pedidos de pré-reserva e passam por confirmação pela nossa equipe
                de atendimento.
              </p>
              <div className="grid sm:grid-cols-2 gap-3">
                <div className="p-3.5 rounded-xl border border-slate-800 bg-slate-900/40">
                  <strong className="text-white block mb-1">Pontualidade:</strong>
                  Recomenda-se a chegada com 10 a 15 minutos de antecedência ao horário agendado para acolhimento e preparo.
                </div>
                <div className="p-3.5 rounded-xl border border-slate-800 bg-slate-900/40">
                  <strong className="text-white block mb-1">Remarcações & Cancelamentos:</strong>
                  Solicitamos a gentileza de avisar com pelo menos 24 (vinte e quatro) horas de antecedência em caso de impossibilidade de comparecimento.
                </div>
              </div>
            </div>
          </section>

          {/* 6. GOOGLE CALENDAR & NOTIFICAÇÕES */}
          <section id="integracoes-google" className="space-y-4 pt-4">
            <h2 className="font-display text-2xl font-bold text-white flex items-center gap-3">
              <span className="p-2 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20 text-sm">06</span>
              Integração com Google Calendar e Mensagens Operacionais
            </h2>
            <p>
              Para assegurar conveniência aos profissionais e pontualidade aos pacientes, nossa plataforma disponibiliza integrações tecnológicas:
            </p>

            <div className="rounded-2xl border border-rose-500/30 bg-rose-950/20 p-5 space-y-3">
              <h3 className="font-semibold text-white text-sm sm:text-base flex items-center gap-2">
                <Calendar className="w-4 h-4 text-rose-400" />
                Uso das APIs do Google Calendar & Requisitos de Uso Limitado
              </h3>
              <p className="text-xs sm:text-sm text-slate-200 leading-relaxed">
                A sincronização de compromissos com o Google Calendar pelos profissionais da clínica opera sob autorização expressa via
                Google OAuth 2.0. Em conformidade com a{' '}
                <strong className="text-rose-300">Google API Services User Data Policy</strong> e suas regras de{' '}
                <strong className="text-white">Uso Limitado (Limited Use requirements)</strong>:
              </p>
              <ul className="text-xs sm:text-sm text-slate-200 space-y-1 list-disc list-inside">
                <li>O acesso é restrito à leitura de conflitos de horários e inclusão de consultas estéticas na agenda escolhida;</li>
                <li>Nenhum dado é comercializado, cedido para publicidade ou utilizado no treinamento de modelos generalistas de IA;</li>
                <li>A conexão pode ser desfeita a qualquer momento pelo painel da clínica ou pelo gerenciador oficial da Conta Google.</li>
              </ul>
            </div>

            <div className="rounded-2xl border border-slate-800 bg-slate-900/40 p-5 space-y-2">
              <h3 className="font-semibold text-white text-sm sm:text-base flex items-center gap-2">
                <MessageCircle className="w-4 h-4 text-rose-400" />
                Notificações Operacionais via WhatsApp
              </h3>
              <p className="text-xs sm:text-sm text-slate-300">
                Ao solicitar agendamento e marcar a autorização correspondente, o usuário concorda em receber lembretes de consulta, confirmações
                e orientações pré e pós-atendimento através do WhatsApp informado, podendo revogar esse envio quando desejar.
              </p>
            </div>
          </section>

          {/* 7. PROPRIEDADE INTELECTUAL */}
          <section id="propriedade-intelectual" className="space-y-4 pt-4">
            <h2 className="font-display text-2xl font-bold text-white flex items-center gap-3">
              <span className="p-2 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20 text-sm">07</span>
              Propriedade Intelectual
            </h2>
            <p>
              Todos os elementos presentes neste website — incluindo a marca nominativa e figurativa da{' '}
              <strong className="text-white">{clinicName}</strong>, logotipos, fotografias, textos, layouts, identidade visual e os
              sistemas proprietários do <strong className="text-white">PainelEstetica</strong> — são protegidos pela legislação brasileira de
              propriedade intelectual e direitos autorais. É vedada qualquer reprodução, cópia, distribuição ou engenharia reversa sem prévia
              autorização por escrito.
            </p>
          </section>

          {/* 8. LIMITAÇÃO DE RESPONSABILIDADE */}
          <section id="responsabilidade" className="space-y-4 pt-4">
            <h2 className="font-display text-2xl font-bold text-white flex items-center gap-3">
              <span className="p-2 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20 text-sm">08</span>
              Limitação de Responsabilidade
            </h2>
            <p>
              Empenhamos contínuos esforços para manter o website e os sistemas online seguros, funcionais e atualizados. Todavia:
            </p>
            <ul className="text-xs sm:text-sm text-slate-300 space-y-2 list-disc list-inside">
              <li>
                Não respondemos por indisponibilidades temporárias resultantes de falhas de conexão de internet, ataques cibernéticos em massa ou
                interrupções técnicas em provedores externos de nuvem ou APIs terceiras;
              </li>
              <li>
                Os resultados de procedimentos estéticos possuem natureza personalizada e dependem de fatores biológicos singulares, resposta
                individual do organismo, estilo de vida e estrito cumprimento das orientações de cuidados domiciliares fornecidas pelo profissional.
              </li>
            </ul>
          </section>

          {/* 9. PRIVACIDADE & LGPD */}
          <section id="privacidade-termos" className="space-y-4 pt-4">
            <h2 className="font-display text-2xl font-bold text-white flex items-center gap-3">
              <span className="p-2 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20 text-sm">09</span>
              Privacidade e Proteção de Dados
            </h2>
            <p>
              O tratamento de quaisquer dados pessoais coletados por meio deste website obedece integralmente à legislação aplicável, em especial a
              Lei Geral de Proteção de Dados (Lei nº 13.709/2018 - LGPD).
            </p>
            <p>
              Para obter informações detalhadas sobre a coleta, finalidade, retenção, medidas de segurança e como exercer seus direitos de titular
              (acesso, correção e exclusão), consulte a nossa{' '}
              <Link to="/privacidade" className="text-rose-400 underline hover:text-rose-300 font-semibold">
                Política de Privacidade
              </Link>.
            </p>
          </section>

          {/* 10. LEGISLAÇÃO & FORO */}
          <section id="legislacao-foro" className="space-y-4 pt-4">
            <h2 className="font-display text-2xl font-bold text-white flex items-center gap-3">
              <span className="p-2 rounded-xl bg-rose-500/10 text-rose-400 border border-rose-500/20 text-sm">10</span>
              Legislação Aplicável e Foro de Eleição
            </h2>
            <p>
              Estes Termos de Serviço são regidos e interpretados segundo as leis vigentes na República Federativa do Brasil, incluindo o Código de
              Defesa do Consumidor (Lei nº 8.078/1990) e o Marco Civil da Internet (Lei nº 12.965/2014).
            </p>
            <p>
              Fica eleito o foro da comarca de domicílio da clínica para dirimir quaisquer eventuais litígios oriundos deste instrumento, com renúncia
              expressa a qualquer outro, por mais privilegiado que seja.
            </p>
          </section>

          {/* DÚVIDAS & ATENDIMENTO */}
          <div className="rounded-2xl border border-slate-800 bg-slate-900/60 p-5 sm:p-6 space-y-3 text-xs sm:text-sm">
            <h3 className="font-semibold text-white text-base flex items-center gap-2">
              <HelpCircle className="w-5 h-5 text-rose-400" />
              Canais Oficiais para Esclarecimentos
            </h3>
            <p className="text-slate-300">
              Se tiver dúvidas sobre nossos termos ou sobre os protocolos da clínica, entre em contato diretamente com nossa recepção:
            </p>
            <div className="flex flex-wrap items-center gap-4 pt-1">
              <a
                href={`https://wa.me/${cms.whatsappNumber}`}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center gap-2 px-4 py-2 rounded-xl bg-rose-600 text-white hover:bg-rose-500 font-semibold text-xs transition-colors"
              >
                <MessageCircle className="w-4 h-4" />
                Falar com a Clínica no WhatsApp ({cms.whatsappNumber})
              </a>
              <span className="text-slate-400 text-xs">
                {cms.addressText}
              </span>
            </div>
          </div>

          {/* BOTTOM CTA BAR */}
          <div className="pt-8 border-t border-slate-800 flex flex-col sm:flex-row items-center justify-between gap-4">
            <Link
              to="/privacidade"
              className="inline-flex items-center gap-2 text-sm text-rose-400 hover:text-rose-300 transition-colors font-medium"
            >
              Consultar nossa Política de Privacidade <ChevronRight className="w-4 h-4" />
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
