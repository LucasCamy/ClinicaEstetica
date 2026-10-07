import React, { useCallback, useEffect, useId, useRef, useState } from 'react';
import { createPortal } from 'react-dom';

// SKELETON LOADER COMPONENTS
export const SkeletonLoader: React.FC<{ className?: string }> = ({ className = 'h-6 w-full' }) => (
  <div className={`skeleton-shimmer rounded-lg ${className}`} aria-hidden="true" />
);

export const SkeletonMetric: React.FC<{ className?: string }> = ({ className = '' }) => (
  <div className={`glass-card rounded-xl p-6 border border-slate-800/80 space-y-3 ${className}`} aria-hidden="true">
    <div className="flex items-center justify-between">
      <SkeletonLoader className="h-4 w-28" />
      <SkeletonLoader className="h-8 w-8 rounded-lg" />
    </div>
    <SkeletonLoader className="h-8 w-36" />
    <SkeletonLoader className="h-3 w-20" />
  </div>
);

export const SkeletonCard: React.FC<{ className?: string; lines?: number }> = ({ className = '', lines = 3 }) => (
  <div className={`glass-card rounded-xl p-6 border border-slate-800/80 space-y-4 ${className}`} aria-hidden="true">
    <div className="flex items-center justify-between pb-3 border-b border-slate-800/60">
      <SkeletonLoader className="h-5 w-40" />
      <SkeletonLoader className="h-4 w-16" />
    </div>
    <div className="space-y-2.5">
      {Array.from({ length: lines }).map((_, i) => (
        <SkeletonLoader key={i} className={`h-4 ${i === lines - 1 ? 'w-2/3' : 'w-full'}`} />
      ))}
    </div>
  </div>
);

export const SkeletonTable: React.FC<{ rows?: number; columns?: number; className?: string }> = ({
  rows = 5,
  columns = 4,
  className = '',
}) => (
  <div className={`glass-panel rounded-xl overflow-hidden border border-slate-800/80 ${className}`} aria-hidden="true">
    <div className="flex items-center gap-4 bg-slate-900/80 px-6 py-4 border-b border-slate-800">
      {Array.from({ length: columns }).map((_, i) => (
        <SkeletonLoader key={i} className="h-4 flex-1" />
      ))}
    </div>
    <div className="divide-y divide-slate-800/50">
      {Array.from({ length: rows }).map((_, rowIndex) => (
        <div key={rowIndex} className="flex items-center gap-4 px-6 py-4">
          {Array.from({ length: columns }).map((_, colIndex) => (
            <SkeletonLoader
              key={colIndex}
              className={`h-4 flex-1 ${colIndex === 0 ? 'w-1/3' : ''}`}
            />
          ))}
        </div>
      ))}
    </div>
  </div>
);

export const SkeletonForm: React.FC<{ fields?: number; className?: string }> = ({
  fields = 4,
  className = '',
}) => (
  <div className={`glass-card rounded-xl p-6 space-y-5 border border-slate-800/80 ${className}`} aria-hidden="true">
    {Array.from({ length: fields }).map((_, i) => (
      <div key={i} className="space-y-2">
        <SkeletonLoader className="h-3 w-24" />
        <SkeletonLoader className="h-10 w-full rounded-lg" />
      </div>
    ))}
    <div className="pt-2 flex justify-end gap-3">
      <SkeletonLoader className="h-9 w-20 rounded-lg" />
      <SkeletonLoader className="h-9 w-28 rounded-lg" />
    </div>
  </div>
);

// PROGRESS BAR COMPONENT
export const ProgressBar: React.FC<{
  progress?: number; // 0 to 100
  label?: string;
  className?: string;
}> = ({ progress, label, className = '' }) => {
  const isIndeterminate = progress === undefined;

  return (
    <div className={`w-full space-y-1.5 ${className}`}>
      {label && (
        <div className="flex justify-between items-center text-xs text-slate-400">
          <span>{label}</span>
          {!isIndeterminate && <span>{Math.round(progress)}%</span>}
        </div>
      )}
      <div className="h-2 w-full overflow-hidden rounded-full bg-slate-800/80">
        {isIndeterminate ? (
          <div className="h-full w-1/3 rounded-full bg-gradient-to-r from-rose-500 to-amber-500 animate-progress-indeterminate" />
        ) : (
          <div
            className="h-full rounded-full bg-gradient-to-r from-rose-500 to-rose-400 transition-all duration-300 ease-out"
            style={{ width: `${Math.min(100, Math.max(0, progress))}%` }}
          />
        )}
      </div>
    </div>
  );
};

// BUTTON COMPONENT
export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary' | 'outline' | 'danger' | 'ghost';
  size?: 'sm' | 'md' | 'lg';
  isLoading?: boolean;
}

export const Button: React.FC<ButtonProps> = ({
  children, variant = 'primary', size = 'md', isLoading = false, disabled, className = '', ...props
}) => {
  const base = "inline-flex items-center justify-center font-medium transition-all duration-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-rose-500/50 disabled:opacity-50 disabled:cursor-not-allowed active:scale-[0.98]";

  const variants = {
    primary: "theme-on-accent bg-rose-600 hover:bg-rose-500 text-white shadow-md shadow-rose-600/20 hover:shadow-rose-500/40 border border-rose-500/30",
    secondary: "bg-amber-500/20 hover:bg-amber-500/30 text-amber-300 border border-amber-500/30",
    outline: "border border-slate-700 hover:border-slate-500 text-slate-200 bg-slate-900/60 hover:bg-slate-800",
    danger: "theme-on-accent bg-red-600 hover:bg-red-500 text-white shadow-md shadow-red-600/20",
    ghost: "text-slate-300 hover:text-white hover:bg-slate-800/60"
  };

  const sizes = {
    sm: "px-3 py-1.5 text-xs gap-1.5",
    md: "px-4 py-2 text-sm gap-2",
    lg: "px-6 py-3 text-base gap-2.5 font-semibold"
  };

  return (
    <button
      className={`${base} ${variants[variant]} ${sizes[size]} ${className}`}
      disabled={disabled || isLoading}
      {...props}
    >
      {isLoading && (
        <svg className="animate-spin -ml-1 mr-2 h-4 w-4 text-current" fill="none" viewBox="0 0 24 24">
          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
        </svg>
      )}
      {children}
    </button>
  );
};

// CARD COMPONENTS
export const Card: React.FC<{ children: React.ReactNode; className?: string }> = ({ children, className = '' }) => (
  <div className={`glass-card rounded-xl p-6 shadow-xl border border-slate-800/80 hover:border-slate-700/80 transition-all ${className}`}>
    {children}
  </div>
);

export const CardHeader: React.FC<{ title: string; subtitle?: string; action?: React.ReactNode }> = ({ title, subtitle, action }) => (
  <div className="flex items-center justify-between pb-4 mb-4 border-b border-slate-800/80">
    <div>
      <h3 className="font-display text-lg font-semibold text-slate-100">{title}</h3>
      {subtitle && <p className="text-xs text-slate-400 mt-0.5">{subtitle}</p>}
    </div>
    {action && <div>{action}</div>}
  </div>
);

// BADGE COMPONENT
export const Badge: React.FC<{ children: React.ReactNode; variant?: 'success' | 'warning' | 'info' | 'danger' | 'default' }> = ({ children, variant = 'default' }) => {
  const styles = {
    success: 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30',
    warning: 'bg-amber-500/10 text-amber-400 border-amber-500/30',
    info: 'bg-sky-500/10 text-sky-400 border-sky-500/30',
    danger: 'bg-rose-500/10 text-rose-400 border-rose-500/30',
    default: 'bg-slate-800 text-slate-300 border-slate-700'
  };

  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium border ${styles[variant]}`}>
      {children}
    </span>
  );
};

// ENHANCED DIALOG / MODAL COMPONENT (ESC KEY + CLICK OUTSIDE)
export const Dialog: React.FC<{
  isOpen: boolean;
  onClose: () => void;
  title: string;
  children: React.ReactNode;
  maxWidth?: string
}> = ({ isOpen, onClose, title, children, maxWidth = 'max-w-xl' }) => {
  const titleId = useId();
  const dialogRef = useRef<HTMLDivElement>(null);
  const onCloseRef = useRef(onClose);

  useEffect(() => {
    onCloseRef.current = onClose;
  }, [onClose]);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onCloseRef.current();
    };

    if (!isOpen) return;

    const previousOverflow = document.body.style.overflow;
    const previouslyFocusedElement = document.activeElement as HTMLElement | null;

    document.body.style.overflow = 'hidden';
    window.addEventListener('keydown', handleKeyDown);
    dialogRef.current?.focus({ preventScroll: true });

    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener('keydown', handleKeyDown);
      previouslyFocusedElement?.focus({ preventScroll: true });
    };
  }, [isOpen]);

  if (!isOpen) return null;

  return createPortal(
    <div
      className="fixed inset-0 z-[100] overflow-y-auto overscroll-contain"
    >
      <div className="fixed inset-0 bg-slate-950/80 backdrop-blur-sm" aria-hidden="true" />
      <div
        className="relative flex min-h-full items-start justify-center px-3 pb-[calc(0.75rem+env(safe-area-inset-bottom,0px))] pt-[calc(0.75rem+env(safe-area-inset-top,0px))] sm:items-center sm:px-6 sm:pb-[calc(1.5rem+env(safe-area-inset-bottom,0px))] sm:pt-[calc(1.5rem+env(safe-area-inset-top,0px))]"
        onMouseDown={(e) => {
          if (e.target === e.currentTarget) onClose();
        }}
      >
        <div
          ref={dialogRef}
          role="dialog"
          aria-modal="true"
          aria-labelledby={titleId}
          tabIndex={-1}
          className={`glass-panel relative flex max-h-[calc(100dvh-env(safe-area-inset-top,0px)-env(safe-area-inset-bottom,0px)-1.5rem)] w-full ${maxWidth} flex-col overflow-hidden rounded-2xl border border-slate-700/80 shadow-2xl outline-none sm:max-h-[calc(100dvh-env(safe-area-inset-top,0px)-env(safe-area-inset-bottom,0px)-3rem)] animate-modal`}
        >
          <div className="flex shrink-0 items-center justify-between border-b border-slate-800 bg-slate-900/90 px-4 py-3 sm:px-6 sm:py-4">
            <h3 id={titleId} className="font-display text-base font-semibold text-rose-400 sm:text-lg">{title}</h3>
            <button
              type="button"
              onClick={onClose}
              aria-label={`Fechar ${title}`}
              className="rounded px-2 py-0.5 text-xl font-bold text-slate-400 transition-colors hover:bg-slate-800 hover:text-white focus:outline-none focus-visible:ring-2 focus-visible:ring-rose-500/60"
            >
              &times;
            </button>
          </div>
          <div className="min-h-0 flex-1 overflow-y-auto p-4 sm:p-6">
            {children}
          </div>
        </div>
      </div>
    </div>,
    document.body
  );
};

export type ConfirmationDialogOptions = {
  title: string;
  description: React.ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: 'primary' | 'danger';
};

export const ConfirmationDialog: React.FC<ConfirmationDialogOptions & {
  isOpen: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}> = ({
  isOpen,
  onCancel,
  onConfirm,
  title,
  description,
  confirmLabel = 'Confirmar',
  cancelLabel = 'Cancelar',
  variant = 'danger',
}) => (
  <Dialog isOpen={isOpen} onClose={onCancel} title={title} maxWidth="max-w-md">
    <div className="space-y-5">
      <div className="text-sm leading-relaxed text-slate-300">{description}</div>
      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <Button type="button" variant="outline" onClick={onCancel}>{cancelLabel}</Button>
        <Button type="button" variant={variant} onClick={onConfirm}>{confirmLabel}</Button>
      </div>
    </div>
  </Dialog>
);

/**
 * Mantém confirmações críticas dentro da interface da aplicação, inclusive em
 * PWA e dispositivos móveis, sem recorrer ao alerta nativo do navegador.
 */
export const useConfirmationDialog = () => {
  const [pending, setPending] = useState<ConfirmationDialogOptions | null>(null);
  const resolverRef = useRef<((confirmed: boolean) => void) | null>(null);

  const resolve = useCallback((confirmed: boolean) => {
    const resolver = resolverRef.current;
    resolverRef.current = null;
    setPending(null);
    resolver?.(confirmed);
  }, []);

  const confirm = useCallback((options: ConfirmationDialogOptions) => new Promise<boolean>(resolvePromise => {
    resolverRef.current?.(false);
    resolverRef.current = resolvePromise;
    setPending(options);
  }), []);

  useEffect(() => () => resolverRef.current?.(false), []);

  const confirmationDialog = (
    <ConfirmationDialog
      isOpen={pending !== null}
      onCancel={() => resolve(false)}
      onConfirm={() => resolve(true)}
      title={pending?.title ?? ''}
      description={pending?.description ?? ''}
      confirmLabel={pending?.confirmLabel}
      cancelLabel={pending?.cancelLabel}
      variant={pending?.variant}
    />
  );

  return { confirm, confirmationDialog };
};

// INPUT & SELECT COMPONENTS
export const Input: React.FC<React.InputHTMLAttributes<HTMLInputElement> & { label?: string }> = ({ label, className = '', type, ...props }) => {
  const isNativeTemporalInput = type === 'date' || type === 'time' || type === 'month' || type === 'datetime-local';

  return (
    <div className="min-w-0 w-full space-y-1.5">
      {label && <label className="block text-xs font-medium text-slate-300">{label}</label>}
      {isNativeTemporalInput ? (
        <div className={`native-temporal-control flex h-10 min-w-0 w-full items-center overflow-hidden rounded-lg border border-slate-800 bg-slate-900/90 px-3.5 transition-all focus-within:border-rose-500 focus-within:ring-2 focus-within:ring-rose-500/50 ${className}`}>
          <input
            type={type}
            className="h-full min-w-0 max-w-full flex-1 appearance-auto overflow-hidden border-0 bg-transparent p-0 text-base leading-normal text-slate-100 outline-none sm:text-sm"
            {...props}
          />
        </div>
      ) : (
        <input
          type={type}
          className={`w-full px-3.5 py-2 rounded-lg bg-slate-900/90 border border-slate-800 text-slate-100 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500/50 focus:border-rose-500 transition-all ${className}`}
          {...props}
        />
      )}
    </div>
  );
};

export const Select: React.FC<React.SelectHTMLAttributes<HTMLSelectElement> & { label?: string }> = ({ label, children, className = '', ...props }) => (
  <div className="space-y-1.5 w-full">
    {label && <label className="block text-xs font-medium text-slate-300">{label}</label>}
    <select
      className={`w-full px-3.5 py-2 rounded-lg bg-slate-900/90 border border-slate-800 text-slate-100 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500/50 focus:border-rose-500 transition-all ${className}`}
      {...props}
    >
      {children}
    </select>
  </div>
);
