import React, { useState, useEffect } from 'react';
import { CmsContent, ProcedureType } from '../../types';
import { api } from '../../services/api';
import { AboutProfessionalSection, ClinicSection, Footer, HeroSection, LandingImageCarousel, LeadFormSection, Navbar, ProcedureCarousel } from '../../components/landing/LandingComponents';

export const LandingPage: React.FC<{ onOpenAdmin: () => void }> = ({ onOpenAdmin }) => {
  const [cms, setCms] = useState<CmsContent | null>(null);
  const [procedures, setProcedures] = useState<ProcedureType[]>([]);
  const [error, setError] = useState('');

  useEffect(() => {
    Promise.all([api.getCms(), api.getProcedures(true)])
      .then(([loadedCms, loadedProcedures]) => {
        setCms(loadedCms);
        setProcedures(loadedProcedures);
      })
      .catch(loadError => {
        setError(loadError instanceof Error ? loadError.message : 'Não foi possível carregar o site.');
      });
  }, []);

  if (error) return (
    <div className="min-h-screen bg-slate-950 flex flex-col gap-4 items-center justify-center text-center px-6">
      <p className="text-rose-400 font-display font-semibold">{error}</p>
      <button className="text-sm text-white underline" onClick={() => window.location.reload()}>Tentar novamente</button>
    </div>
  );

  if (!cms) return (
    <div className="min-h-screen bg-slate-950 text-slate-100 p-6 space-y-12 animate-pulse">
      {/* Navbar Skeleton */}
      <div className="mx-auto max-w-7xl flex items-center justify-between py-4">
        <div className="h-8 w-40 rounded-lg bg-slate-800/80" />
        <div className="hidden md:flex gap-6">
          <div className="h-4 w-20 rounded bg-slate-800/60" />
          <div className="h-4 w-24 rounded bg-slate-800/60" />
          <div className="h-4 w-20 rounded bg-slate-800/60" />
        </div>
        <div className="h-9 w-28 rounded-lg bg-slate-800/80" />
      </div>
      {/* Hero Skeleton */}
      <div className="mx-auto max-w-7xl grid md:grid-cols-2 gap-12 items-center pt-8">
        <div className="space-y-6">
          <div className="h-4 w-32 rounded bg-rose-500/20" />
          <div className="space-y-3">
            <div className="h-12 w-4/5 rounded-lg bg-slate-800/90" />
            <div className="h-12 w-3/5 rounded-lg bg-slate-800/90" />
          </div>
          <div className="h-16 w-full rounded-lg bg-slate-800/60" />
          <div className="flex gap-4 pt-4">
            <div className="h-12 w-40 rounded-lg bg-rose-600/40" />
            <div className="h-12 w-36 rounded-lg bg-slate-800/80" />
          </div>
        </div>
        <div className="h-96 rounded-3xl bg-slate-800/60 border border-slate-800" />
      </div>
    </div>
  );

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 font-sans selection:bg-rose-500 selection:text-white">
      <Navbar cms={cms} onOpenAdmin={onOpenAdmin} />
      <HeroSection cms={cms} />
      <AboutProfessionalSection cms={cms} />
      <ProcedureCarousel procedures={procedures} />
      <ClinicSection cms={cms} />
      <LandingImageCarousel items={cms.carouselItems ?? []} />
      <LeadFormSection procedures={procedures} />
      <Footer cms={cms} />
    </div>
  );
};
