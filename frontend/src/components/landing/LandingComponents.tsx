import { ArrowRight, Calendar, CheckCircle, ChevronLeft, ChevronRight, FileText, LockKeyhole, MapPin, MessageCircle, Moon, Phone, ShieldCheck, Sparkles, Sun } from 'lucide-react';
import React, { useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { useTheme } from '../../contexts/ThemeContext';
import { api } from '../../services/api';
import { CmsContent, LandingCarouselItem, ProcedureType } from '../../types';
import { Button, Input, Select } from '../ui/Components';

export const defaultCmsContent: CmsContent = {
  heroTitle: 'Realce Sua Beleza Natural com Ciência e Elegância',
  heroSubtitle: 'Tratamentos estéticos faciais e corporais avançados com atendimento exclusivo e personalizado para renovar sua autoestima.',
  doctorName: 'Dra. Leilaine Arakaki',
  doctorTitle: 'Estética Avançada & Cosmiatria',
  doctorBio: 'Especialista em estética avançada com atendimento exclusivo e personalizado, unindo técnicas modernas, biossegurança e cuidado individualizado.',
  doctorPhotoUrl: '/brand/dra-leilaine-portrait.jpg',
  logoOnDarkUrl: '/brand/leilaine-arakaki-logo-light.png',
  logoOnLightUrl: '/brand/leilaine-arakaki-logo-dark.png',
  whatsappNumber: '5511999998888',
  addressText: 'Atendimento exclusivo mediante agendamento prévio',
  publicHoursWeekdays: 'Segunda a Sexta: 08:00 às 19:00',
  publicHoursSaturday: 'Sábado: 08:00 às 13:00',
  publicHoursNote: 'Atendimento com horário agendado.',
  aboutTitle: 'A Profissional',
  aboutText: '',
  aboutImageUrl: '',
  clinicTitle: 'A Clínica',
  clinicText: '',
  clinicImageUrl: '',
  carouselItems: []
};

const DEFAULT_DOCTOR_PHOTO_URL = '/brand/dra-leilaine-portrait.jpg';
const DEFAULT_LOGO_ON_DARK_URL = '/brand/leilaine-arakaki-logo-light.png';
const DEFAULT_LOGO_ON_LIGHT_URL = '/brand/leilaine-arakaki-logo-dark.png';
const legacySamplePhotoPatterns = [
  /images\.unsplash\.com\/photo-1594824813566-8207198e3b1c/i,
  /maisbemestar\.com\.br\/wp-content\/uploads\/2018\/01\/estetica-facil-saude-rosto\.jpg/i,
];

const getDoctorPhotoUrl = (photoUrl: string) => {
  const normalizedUrl = photoUrl.trim();
  return !normalizedUrl || legacySamplePhotoPatterns.some(pattern => pattern.test(normalizedUrl)) ? DEFAULT_DOCTOR_PHOTO_URL : normalizedUrl;
};

export const LandingBrand: React.FC<{ cms: CmsContent; className?: string }> = ({ cms, className = '' }) => (
  <span className={`landing-brand ${className}`}>
    <img
      className="landing-brand-on-dark"
      src={cms.logoOnDarkUrl?.trim() || DEFAULT_LOGO_ON_DARK_URL}
      alt={`${cms.doctorName} — Estética Avançada`}
    />
    <img
      className="landing-brand-on-light"
      src={cms.logoOnLightUrl?.trim() || DEFAULT_LOGO_ON_LIGHT_URL}
      alt=""
      aria-hidden="true"
    />
  </span>
);

// NAVBAR
export const Navbar: React.FC<{ cms: CmsContent; onOpenAdmin: () => void }> = ({ cms, onOpenAdmin }) => {
  const { resolvedTheme, setMode } = useTheme();
  const location = useLocation();
  const isHomePage = location.pathname === '/';
  const nextTheme = resolvedTheme === 'dark' ? 'light' : 'dark';
  const ThemeIcon = resolvedTheme === 'dark' ? Sun : Moon;
  const hasAbout = Boolean(cms.aboutTitle?.trim() && cms.aboutText?.trim());
  const hasClinic = Boolean(cms.clinicTitle?.trim() && cms.clinicText?.trim());
  const hasGallery = (cms.carouselItems ?? []).some(item => item.imageUrl?.trim());

  return (
    <nav className="fixed top-0 left-0 right-0 z-40 border-b border-slate-800/80 glass-panel [padding-top:env(safe-area-inset-top,0px)] [padding-inline-end:env(safe-area-inset-right,0px)] [padding-inline-start:env(safe-area-inset-left,0px)]">
      <div className="mx-auto flex h-16 max-w-7xl items-center justify-between gap-3 px-4 sm:h-20 sm:px-6 lg:px-8">
        <Link to="/" className="flex min-w-0 items-center" aria-label="Dra. Leilaine Arakaki — voltar ao início">
          <LandingBrand cms={cms} className="h-12 w-24 sm:h-16 sm:w-36 xl:h-[76px] xl:w-44" />
        </Link>

        <div className="hidden items-center gap-7 text-sm font-medium text-slate-300 xl:flex">
          {!isHomePage && (
            <Link to="/" className="hover:text-rose-400 transition-colors">
              Início
            </Link>
          )}
          <a href={isHomePage ? '#procedimentos' : '/#procedimentos'} className="hover:text-rose-400 transition-colors">
            Procedimentos
          </a>
          {hasAbout && (
            <a href={isHomePage ? '#sobre' : '/#sobre'} className="hover:text-rose-400 transition-colors">
              A profissional
            </a>
          )}
          {hasClinic && (
            <a href={isHomePage ? '#clinica' : '/#clinica'} className="hover:text-rose-400 transition-colors">
              A clínica
            </a>
          )}
          {hasGallery && (
            <a href={isHomePage ? '#galeria' : '/#galeria'} className="hover:text-rose-400 transition-colors">
              Galeria
            </a>
          )}
          <a href={isHomePage ? '#contato' : '/#contato'} className="hover:text-rose-400 transition-colors">
            Contato
          </a>
        </div>

        <div className="flex shrink-0 items-center gap-2 sm:gap-3">
          <button
            type="button"
            onClick={() => setMode(nextTheme)}
            aria-label={`Ativar modo ${nextTheme === 'light' ? 'claro' : 'escuro'}`}
            title={`Ativar modo ${nextTheme === 'light' ? 'claro' : 'escuro'}`}
            className="flex h-9 w-9 items-center justify-center rounded-lg border border-slate-700 bg-slate-900/60 text-slate-300 transition-colors hover:border-rose-500/50 hover:bg-slate-800 hover:text-rose-400 focus:outline-none focus-visible:ring-2 focus-visible:ring-rose-500/60 sm:h-10 sm:w-10"
          >
            <ThemeIcon className="h-4 w-4 sm:h-5 sm:w-5" />
          </button>
          <Button variant="outline" size="sm" className="px-2.5 sm:px-3" onClick={onOpenAdmin}>
            <LockKeyhole className="h-4 w-4" />
            <span className="hidden sm:inline">Área restrita</span>
            <span className="sr-only sm:hidden">Área restrita</span>
          </Button>
          <a
            href={`https://wa.me/${cms.whatsappNumber}`}
            target="_blank"
            rel="noopener noreferrer"
            className="hidden lg:inline-flex"
          >
            <Button variant="primary" size="sm">
              <MessageCircle className="w-4 h-4" /> Agendar no WhatsApp
            </Button>
          </a>
        </div>
      </div>
    </nav>
  );
};

// HERO SECTION
export const HeroSection: React.FC<{ cms: CmsContent }> = ({ cms }) => (
  <section className="relative overflow-hidden pb-16 pt-[calc(6rem+env(safe-area-inset-top,0px))] sm:pb-24 sm:pt-[calc(7rem+env(safe-area-inset-top,0px))] lg:pb-28 lg:pt-[calc(9rem+env(safe-area-inset-top,0px))] xl:pb-32 xl:pt-[calc(10rem+env(safe-area-inset-top,0px))]">
    <div className="pointer-events-none absolute left-1/2 top-1/4 h-[min(75vw,600px)] w-[min(75vw,600px)] -translate-x-1/2 -translate-y-1/2 rounded-full bg-rose-600/10 blur-[140px]"></div>

    <div className="relative z-10 mx-auto grid max-w-7xl items-center gap-10 px-4 sm:px-6 lg:px-8 xl:grid-cols-2 xl:gap-12">
      <div className="space-y-6">
        <div className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full bg-rose-500/10 border border-rose-500/20 text-rose-400 text-xs font-semibold uppercase tracking-wider">
          <Sparkles className="w-3.5 h-3.5" /> Atendimento Exclusivo & Personalizado
        </div>

        <h1 className="font-display text-4xl font-extrabold leading-tight text-white sm:text-5xl xl:text-6xl">
          {cms.heroTitle}
        </h1>

        <p className="text-base font-light leading-relaxed text-slate-300 sm:text-lg">
          {cms.heroSubtitle}
        </p>

        <div className="flex flex-col sm:flex-row gap-4 pt-4">
          <a href="#contato">
            <Button variant="primary" size="lg" className="w-full sm:w-auto">
              <Calendar className="w-5 h-5" /> Agendar Avaliação <ArrowRight className="w-4 h-4" />
            </Button>
          </a>
          <a href="#procedimentos">
            <Button variant="outline" size="lg" className="w-full sm:w-auto">
              Conhecer Tratamentos
            </Button>
          </a>
        </div>

        <div className="grid grid-cols-1 gap-4 border-t border-slate-800/80 pt-8 sm:grid-cols-3">
          <div>
            <span className="block font-display text-2xl font-bold text-white">3+ Anos</span>
            <span className="text-xs text-slate-400">De Experiência</span>
          </div>
          <div>
            <span className="block font-display text-2xl font-bold text-rose-400">2.500+</span>
            <span className="text-xs text-slate-400">Atendimentos</span>
          </div>
          <div>
            <span className="block font-display text-2xl font-bold text-white">Cuidado</span>
            <span className="text-xs text-slate-400">Atendimento responsável</span>
          </div>
        </div>
      </div>

      <div className="relative">
        <div className="relative z-10 rounded-3xl overflow-hidden shadow-2xl border border-slate-700/60 glow-rose">
          <img
            src={getDoctorPhotoUrl(cms.doctorPhotoUrl)}
            alt={cms.doctorName}
            className="h-[340px] w-full object-cover object-center transition-transform duration-700 hover:scale-105 sm:h-[420px] lg:h-[460px] xl:h-[500px]"
          />
          <div className="absolute bottom-0 left-0 right-0 p-6 bg-gradient-to-t from-slate-950 via-slate-950/80 to-transparent">
            <h3 className="font-display text-xl font-bold text-white">{cms.doctorName}</h3>
            <p className="text-rose-400 text-sm font-medium">{cms.doctorTitle}</p>
          </div>
        </div>
      </div>
    </div>
  </section>
);

export const AboutProfessionalSection: React.FC<{ cms: CmsContent }> = ({ cms }) => {
  const title = cms.aboutTitle?.trim();
  const text = cms.aboutText?.trim();
  if (!title || !text) return null;

  const imageUrl = cms.aboutImageUrl?.trim() || getDoctorPhotoUrl(cms.doctorPhotoUrl);
  return (
    <section id="sobre" className="relative overflow-hidden bg-slate-900/50 py-16 sm:py-20 xl:py-24">
      <div className="mx-auto grid max-w-7xl items-center gap-8 px-4 sm:px-6 lg:grid-cols-2 lg:gap-14 lg:px-8">
        <div className="overflow-hidden rounded-3xl border border-slate-700/70 bg-slate-900 shadow-2xl">
          <img src={imageUrl} alt={cms.doctorName} className="h-[340px] w-full object-cover object-center sm:h-[440px]" />
        </div>
        <div className="max-w-xl">
          <span className="text-xs font-bold uppercase tracking-[0.2em] text-rose-400">Conheça a profissional</span>
          <h2 className="mt-3 font-display text-3xl font-extrabold leading-tight text-white sm:text-4xl">{title}</h2>
          <p className="mt-5 whitespace-pre-line text-sm font-light leading-7 text-slate-300 sm:text-base">{text}</p>
          <a href="#contato" className="mt-7 inline-flex">
            <Button variant="outline">Conversar sobre sua avaliação <ArrowRight className="h-4 w-4" /></Button>
          </a>
        </div>
      </div>
    </section>
  );
};

export const ClinicSection: React.FC<{ cms: CmsContent }> = ({ cms }) => {
  const title = cms.clinicTitle?.trim();
  const text = cms.clinicText?.trim();
  const imageUrl = cms.clinicImageUrl?.trim();
  if (!title || !text) return null;

  return (
    <section id="clinica" className="relative overflow-hidden py-16 sm:py-20 xl:py-24">
      <div className={`mx-auto grid max-w-7xl items-center gap-8 px-4 sm:px-6 lg:gap-14 lg:px-8 ${imageUrl ? 'lg:grid-cols-2' : ''}`}>
        <div className="max-w-xl">
          <span className="text-xs font-bold uppercase tracking-[0.2em] text-rose-400">A clínica</span>
          <h2 className="mt-3 font-display text-3xl font-extrabold leading-tight text-white sm:text-4xl">{title}</h2>
          <p className="mt-5 whitespace-pre-line text-sm font-light leading-7 text-slate-300 sm:text-base">{text}</p>
        </div>
        {imageUrl && <div className="overflow-hidden rounded-3xl border border-slate-700/70 bg-slate-900 shadow-2xl">
          <img src={imageUrl} alt={title} className="h-[300px] w-full object-cover object-center sm:h-[390px]" />
        </div>}
      </div>
    </section>
  );
};

export const LandingImageCarousel: React.FC<{ items: LandingCarouselItem[] }> = ({ items }) => {
  const publishedItems = items.filter(item => item.imageUrl?.trim());
  const [activeIndex, setActiveIndex] = useState(0);
  if (!publishedItems.length) return null;

  const item = publishedItems[activeIndex % publishedItems.length];
  const move = (direction: number) => setActiveIndex(current => (current + direction + publishedItems.length) % publishedItems.length);

  return (
    <section id="galeria" className="relative overflow-hidden bg-slate-900/50 py-16 sm:py-20 xl:py-24">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mx-auto mb-8 max-w-2xl text-center sm:mb-10">
          <span className="text-xs font-bold uppercase tracking-[0.2em] text-rose-400">Galeria</span>
          <h2 className="mt-3 font-display text-3xl font-extrabold text-white sm:text-4xl">Detalhes que inspiram cuidado</h2>
        </div>
        <div className="relative overflow-hidden rounded-3xl border border-slate-700/70 bg-slate-950 shadow-2xl">
          <img src={item.imageUrl} alt={item.title || 'Imagem da clínica'} className="h-[320px] w-full object-cover object-center sm:h-[460px] xl:h-[560px]" />
          {(item.title || item.description) && <div className="absolute inset-x-0 bottom-0 bg-gradient-to-t from-slate-950 via-slate-950/75 to-transparent px-5 pb-6 pt-20 sm:px-8 sm:pb-8">
            {item.title && <h3 className="font-display text-xl font-bold text-white sm:text-2xl">{item.title}</h3>}
            {item.description && <p className="mt-1 max-w-2xl text-sm text-slate-200">{item.description}</p>}
          </div>}
          {publishedItems.length > 1 && <>
            <button type="button" onClick={() => move(-1)} aria-label="Imagem anterior" className="absolute left-3 top-1/2 flex h-10 w-10 -translate-y-1/2 items-center justify-center rounded-full border border-white/20 bg-slate-950/60 text-white backdrop-blur transition hover:bg-slate-900 sm:left-5 sm:h-11 sm:w-11"><ChevronLeft className="h-5 w-5" /></button>
            <button type="button" onClick={() => move(1)} aria-label="Próxima imagem" className="absolute right-3 top-1/2 flex h-10 w-10 -translate-y-1/2 items-center justify-center rounded-full border border-white/20 bg-slate-950/60 text-white backdrop-blur transition hover:bg-slate-900 sm:right-5 sm:h-11 sm:w-11"><ChevronRight className="h-5 w-5" /></button>
            <div className="absolute bottom-3 left-1/2 flex -translate-x-1/2 gap-2 sm:bottom-4">
              {publishedItems.map((galleryItem, index) => <button key={galleryItem.id || galleryItem.imageUrl} type="button" onClick={() => setActiveIndex(index)} aria-label={`Exibir imagem ${index + 1}`} className={`h-2 rounded-full transition-all ${index === activeIndex % publishedItems.length ? 'w-6 bg-rose-400' : 'w-2 bg-white/50 hover:bg-white/80'}`} />)}
            </div>
          </>}
        </div>
      </div>
    </section>
  );
};

// PROCEDURE CAROUSEL
export const ProcedureCarousel: React.FC<{ procedures: ProcedureType[] }> = ({ procedures }) => {
  const [activeIdx, setActiveIdx] = useState(0);

  if (procedures.length === 0) return null;

  return (
    <section id="procedimentos" className="relative bg-slate-900/50 py-16 sm:py-20 xl:py-24">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mx-auto mb-10 max-w-2xl text-center sm:mb-12 xl:mb-16">
          <h2 className="font-display text-3xl sm:text-4xl font-extrabold text-white">
            Nossos Tratamentos em <span className="text-rose-500">Destaque</span>
          </h2>
          <p className="text-slate-400 text-sm mt-3">
            Protocolos desenvolvidos com biotecnologia de ponta para realçar sua pele com naturalidade.
          </p>
        </div>

        <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3 xl:gap-8">
          {procedures.map((proc, idx) => (
            <div
              key={proc.id}
              className={`glass-card rounded-2xl overflow-hidden border transition-all duration-300 ${
                idx === activeIdx ? 'border-rose-500 shadow-xl glow-rose scale-[1.02]' : 'border-slate-800 hover:border-slate-700'
              }`}
              onClick={() => setActiveIdx(idx)}
            >
              <div className="h-56 overflow-hidden relative">
                <img src={proc.imageUrl} alt={proc.name} className="w-full h-full object-cover" />
                <div className="absolute top-3 right-3 bg-slate-950/80 backdrop-blur-md px-3 py-1 rounded-full text-xs font-semibold text-rose-400 border border-rose-500/30">
                  {proc.durationMinutes} min
                </div>
              </div>
              <div className="p-6 space-y-4">
                <h3 className="font-display text-xl font-bold text-white">{proc.name}</h3>
                <p className="text-slate-300 text-sm font-light leading-relaxed">{proc.description}</p>
                <div className="flex items-center justify-between pt-4 border-t border-slate-800">
                  <div>
                    {proc.isPriceHiddenOnWebsite ? (
                      <>
                        <span className="text-[10px] uppercase text-slate-400 block">Condições</span>
                        <span className="font-display text-base font-bold text-rose-400">Consulte-nos</span>
                      </>
                    ) : (
                      <>
                        <span className="text-[10px] uppercase text-slate-400 block">A partir de</span>
                        <span className="font-display text-xl font-bold text-rose-400">
                          R$ {proc.price.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                        </span>
                      </>
                    )}
                  </div>
                  <a href="#contato">
                    <Button variant="primary" size="sm">Agendar</Button>
                  </a>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
};

// LEAD FORM SECTION
export const LeadFormSection: React.FC<{ procedures: ProcedureType[] }> = ({ procedures }) => {
  const [form, setForm] = useState({ name: '', phone: '', email: '', interestedProcedure: '', message: '', website: '' });
  const [sent, setSent] = useState(false);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSending(true);
    setError('');
    try {
      await api.createLead({ ...form, source: 'Website' });
      setSent(true);
      setForm({ name: '', phone: '', email: '', interestedProcedure: '', message: '', website: '' });
    } catch (submitError) {
      setError(submitError instanceof Error ? submitError.message : 'Não foi possível enviar seu contato.');
    } finally {
      setSending(false);
    }
  };

  return (
    <section id="contato" className="relative overflow-hidden py-16 sm:py-20 xl:py-24">
      <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8">
        <div className="relative rounded-3xl border border-rose-500/30 p-5 shadow-2xl glass-panel sm:p-8 xl:p-12">
          <div className="text-center max-w-xl mx-auto mb-8">
            <span className="text-rose-400 text-xs font-bold uppercase tracking-widest">Atendimento Exclusivo</span>
            <h2 className="font-display text-3xl font-bold text-white mt-1">Agende Sua Avaliação Estética</h2>
            <p className="text-slate-400 text-xs mt-2">
              Preencha os dados abaixo e nossa equipe entrará em contato para agendar o melhor horário.
            </p>
          </div>

          {sent ? (
            <div className="text-center py-12 space-y-4">
              <CheckCircle className="w-16 h-16 text-emerald-400 mx-auto" />
              <h3 className="font-display text-2xl font-bold text-white">Solicitação Enviada com Sucesso!</h3>
              <p className="text-slate-300 text-sm">Entraremos em contato via WhatsApp muito em breve.</p>
              <Button variant="outline" onClick={() => setSent(false)}>Enviar Outra Mensagem</Button>
            </div>
          ) : (
            <form onSubmit={handleSubmit} className="space-y-4">
              {error && (
                <div role="alert" className="p-3 rounded-lg border border-rose-500/30 bg-rose-500/10 text-sm text-rose-300">
                  {error}
                </div>
              )}

              {/* Honeypot field (hidden from real users, populated by automated spambots) */}
              <div className="absolute -left-[9999px] opacity-0 pointer-events-none" aria-hidden="true">
                <label htmlFor="website-hp">Website</label>
                <input
                  id="website-hp"
                  type="text"
                  tabIndex={-1}
                  autoComplete="off"
                  value={form.website}
                  onChange={e => setForm({ ...form, website: e.target.value })}
                />
              </div>

              <div className="grid md:grid-cols-2 gap-4">
                <Input
                  label="Seu Nome Completo"
                  placeholder="Ex: Ana Maria Silva"
                  required
                  value={form.name}
                  onChange={e => setForm({...form, name: e.target.value})}
                />
                <Input
                  label="WhatsApp / Telefone"
                  placeholder="(11) 99999-9999"
                  required
                  value={form.phone}
                  onChange={e => setForm({...form, phone: e.target.value})}
                />
              </div>

              <div className="grid md:grid-cols-2 gap-4">
                <Input
                  label="Seu E-mail"
                  type="email"
                  placeholder="ana@email.com"
                  value={form.email}
                  onChange={e => setForm({...form, email: e.target.value})}
                />
                <Select
                  label="Procedimento de Interesse"
                  value={form.interestedProcedure}
                  onChange={e => setForm({...form, interestedProcedure: e.target.value})}
                >
                  <option value="">Selecione o tratamento...</option>
                  {procedures.map(p => (
                    <option key={p.id} value={p.name}>{p.name}</option>
                  ))}
                  <option value="Avaliação Geral">Avaliação Estética Geral</option>
                </Select>
              </div>

              <div className="space-y-1.5">
                <label className="block text-xs font-medium text-slate-300">Mensagem ou Dúvida (Opcional)</label>
                <textarea
                  className="w-full px-3.5 py-2 rounded-lg bg-slate-900/90 border border-slate-800 text-slate-100 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500/50"
                  rows={3}
                  placeholder="Conte um pouco do que você busca melhorar..."
                  value={form.message}
                  onChange={e => setForm({...form, message: e.target.value})}
                />
              </div>

              <Button type="submit" variant="primary" size="lg" className="w-full mt-4" isLoading={sending}>
                {sending ? 'Enviando...' : 'Enviar Solicitação & Agendar'}
              </Button>
            </form>
          )}
        </div>
      </div>
    </section>
  );
};

// FOOTER
export const Footer: React.FC<{ cms: CmsContent }> = ({ cms }) => {
  const publicHours = [cms.publicHoursWeekdays, cms.publicHoursSaturday, cms.publicHoursNote].filter(Boolean);
  return (
    <footer className="bg-slate-950 border-t border-slate-800/80 py-12 text-slate-400 text-sm">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className={`grid gap-8 ${publicHours.length ? 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-4' : 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3'}`}>
          <div>
            <LandingBrand cms={cms} className="mb-3 h-20 w-40 sm:h-24 sm:w-48" />
            <p className="text-xs text-slate-500">
              Atendimento em estética avançada, com técnica, cuidado e identidade própria.
            </p>
            <p className="text-xs text-slate-500 mt-4">
              Conformidade total LGPD © 2026. Todos os direitos reservados.
            </p>
          </div>

          <div>
            <h5 className="font-semibold text-slate-200 mb-3 flex items-center gap-2">
              <ShieldCheck className="w-4 h-4 text-rose-400" />
              Legal & Privacidade
            </h5>
            <ul className="space-y-2.5 text-xs">
              <li>
                <Link
                  to="/privacidade"
                  className="text-slate-400 hover:text-rose-400 transition-colors inline-flex items-center gap-1.5"
                >
                  <FileText className="w-3.5 h-3.5 text-slate-500" />
                  Política de Privacidade
                </Link>
              </li>
              <li>
                <Link
                  to="/termos"
                  className="text-slate-400 hover:text-rose-400 transition-colors inline-flex items-center gap-1.5"
                >
                  <FileText className="w-3.5 h-3.5 text-slate-500" />
                  Termos de Serviço
                </Link>
              </li>
              <li>
                <Link
                  to="/privacidade#google-data-policy"
                  className="text-slate-500 hover:text-rose-400 transition-colors inline-flex items-center gap-1.5"
                >
                  Uso de Dados Google (Limited Use)
                </Link>
              </li>
              <li>
                <Link
                  to="/privacidade#direitos-lgpd"
                  className="text-slate-500 hover:text-rose-400 transition-colors inline-flex items-center gap-1.5"
                >
                  Direitos do Titular (LGPD)
                </Link>
              </li>
            </ul>
          </div>

          <div>
            <h5 className="font-semibold text-slate-200 mb-3">Endereço & Atendimento</h5>
            <div className="flex items-start gap-2 text-xs text-slate-400 mb-2">
              <MapPin className="w-4 h-4 text-rose-400 shrink-0 mt-0.5" />
              <span>{cms.addressText}</span>
            </div>
            <div className="flex items-center gap-2 text-xs text-slate-400">
              <Phone className="w-4 h-4 text-rose-400 shrink-0" />
              <a
                href={`https://wa.me/${cms.whatsappNumber}`}
                target="_blank"
                rel="noopener noreferrer"
                className="hover:text-rose-400 transition-colors"
              >
                WhatsApp: {cms.whatsappNumber}
              </a>
            </div>
          </div>

          {publicHours.length > 0 && (
            <div>
              <h5 className="font-semibold text-slate-200 mb-3">Horário de Funcionamento</h5>
              {publicHours.map((hours, index) => (
                <p key={`${hours}-${index}`} className={`text-xs ${index ? 'mt-1' : ''}`}>
                  {hours}
                </p>
              ))}
            </div>
          )}
        </div>

        <div className="mt-8 pt-6 border-t border-slate-900/80 flex flex-col sm:flex-row items-center justify-between gap-4 text-xs text-slate-500">
          <div>
            {cms.doctorName} • Clínica Estética Avançada
          </div>
          <div className="flex flex-wrap items-center gap-3 sm:gap-4">
            <Link to="/privacidade" className="hover:text-rose-400 transition-colors">
              Privacidade
            </Link>
            <span>•</span>
            <Link to="/termos" className="hover:text-rose-400 transition-colors">
              Termos de Serviço
            </Link>
            <span>•</span>
            <button
              type="button"
              onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })}
              className="hover:text-rose-400 transition-colors cursor-pointer"
            >
              Voltar ao topo ↑
            </button>
          </div>
        </div>
      </div>
    </footer>
  );
};
