import React from 'react';

export const AdminPageHeader: React.FC<{
  title: string;
  description: string;
  eyebrow?: string;
  actions?: React.ReactNode;
  className?: string;
}> = ({ title, description, eyebrow, actions, className = '' }) => (
  <header className={`flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between ${className}`}>
    <div className="min-w-0">
      {eyebrow && <p className="mb-1 text-xs font-semibold uppercase tracking-[0.16em] text-rose-400">{eyebrow}</p>}
      <h1 className="break-words font-display text-2xl font-bold text-white sm:text-3xl">{title}</h1>
      <p className="mt-1 text-xs text-slate-400 sm:text-sm">{description}</p>
    </div>
    {actions && <div className="flex w-full flex-col gap-2 sm:flex-row sm:flex-wrap sm:items-center lg:w-auto [&_button]:w-full [&_button]:whitespace-nowrap sm:[&_button]:w-auto">{actions}</div>}
  </header>
);
