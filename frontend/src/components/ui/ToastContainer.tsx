import React from 'react';
import { createPortal } from 'react-dom';
import { CheckCircle2, AlertCircle, Info, X } from 'lucide-react';

export interface ToastMessage {
  id: string;
  message: string;
  type: 'success' | 'error' | 'info' | 'warning';
}

export const ToastContainer: React.FC<{
  toasts: ToastMessage[];
  onRemove: (id: string) => void
}> = ({ toasts, onRemove }) => {
  if (toasts.length === 0) return null;

  return createPortal(
    <div className="fixed top-5 right-5 z-[300] flex w-full max-w-sm flex-col gap-3 pointer-events-none">
      {toasts.map((toast) => {
        const styles = {
          success: 'bg-emerald-950/90 border-emerald-500/50 text-emerald-200',
          error: 'bg-rose-950/90 border-rose-500/50 text-rose-200',
          warning: 'bg-amber-950/90 border-amber-500/50 text-amber-200',
          info: 'bg-sky-950/90 border-sky-500/50 text-sky-200',
        };

        const icons = {
          success: <CheckCircle2 className="w-5 h-5 text-emerald-400 flex-shrink-0" />,
          error: <AlertCircle className="w-5 h-5 text-rose-400 flex-shrink-0" />,
          warning: <AlertCircle className="w-5 h-5 text-amber-400 flex-shrink-0" />,
          info: <Info className="w-5 h-5 text-sky-400 flex-shrink-0" />,
        };

        return (
          <div
            key={toast.id}
            className={`pointer-events-auto p-4 rounded-xl border backdrop-blur-md shadow-2xl flex items-center justify-between gap-3 animate-slide-in ${styles[toast.type]}`}
          >
            <div className="flex items-center gap-3 text-xs font-medium">
              {icons[toast.type]}
              <span>{toast.message}</span>
            </div>
            <button
              onClick={() => onRemove(toast.id)}
              className="text-slate-400 hover:text-white transition-colors"
            >
              <X className="w-4 h-4" />
            </button>
          </div>
        );
      })}
    </div>,
    document.body,
  );
};
