import React from 'react';
import { Check, Monitor, Moon, Palette, Sun } from 'lucide-react';
import { accentOptions, ThemeMode, useTheme } from '../../contexts/ThemeContext';
import { Dialog } from '../ui/Components';

const modeOptions: Array<{ id: ThemeMode; label: string; icon: React.ElementType; description: string }> = [
  { id: 'light', label: 'Claro', icon: Sun, description: 'Fundo claro' },
  { id: 'dark', label: 'Escuro', icon: Moon, description: 'Padrão recomendado' },
  { id: 'system', label: 'Sistema', icon: Monitor, description: 'Segue o dispositivo' },
];

export const AppearanceDialog: React.FC<{ isOpen: boolean; onClose: () => void }> = ({ isOpen, onClose }) => {
  const { mode, resolvedTheme, accent, setMode, setAccent } = useTheme();

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title="Personalizar aparência" maxWidth="max-w-lg">
      <div className="space-y-6">
        <div className="flex items-start gap-3 text-sm text-slate-400">
          <Palette className="mt-0.5 h-4 w-4 shrink-0 text-rose-400" />
          <p>Escolha o modo e a cor principal. A preferência fica salva apenas neste dispositivo.</p>
        </div>

        <fieldset>
          <legend className="mb-3 text-sm font-semibold text-slate-200">Modo de tema</legend>
          <div className="grid grid-cols-3 gap-2 sm:gap-3">
            {modeOptions.map(option => {
              const Icon = option.icon;
              const selected = mode === option.id;
              return (
                <button
                  key={option.id}
                  type="button"
                  aria-pressed={selected}
                  onClick={() => setMode(option.id)}
                  className={`relative flex min-h-24 flex-col items-center justify-center rounded-xl border p-2 text-center transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-rose-500/60 ${
                    selected
                      ? 'border-slate-100 bg-slate-900/60 text-slate-100 shadow-sm ring-1 ring-slate-100/70'
                      : 'border-slate-800 bg-slate-950/40 text-slate-300 hover:border-slate-700 hover:bg-slate-900/50'
                  }`}
                >
                  <Icon className="mb-2 h-5 w-5" />
                  <span className="text-xs font-semibold sm:text-sm">{option.label}</span>
                  <span className="mt-1 hidden text-[10px] text-slate-500 sm:block">{option.description}</span>
                </button>
              );
            })}
          </div>
          {mode === 'system' && <p className="mt-2 text-xs text-slate-500">O sistema está usando o modo {resolvedTheme === 'dark' ? 'escuro' : 'claro'} neste momento.</p>}
        </fieldset>

        <fieldset>
          <legend className="mb-3 text-sm font-semibold text-slate-200">Cor do tema</legend>
          <div className="grid grid-cols-3 gap-2 min-[390px]:grid-cols-4 sm:gap-3">
            {accentOptions.map(option => {
              const selected = accent === option.id;
              return (
                <button
                  key={option.id}
                  type="button"
                  aria-pressed={selected}
                  onClick={() => setAccent(option.id)}
                  className={`relative flex min-h-20 flex-col items-center justify-center rounded-xl border p-2 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-rose-500/60 ${
                    selected
                      ? 'border-slate-100 bg-slate-900/60 ring-1 ring-slate-100/70'
                      : 'border-slate-800 bg-slate-950/40 hover:border-slate-700 hover:bg-slate-900/50'
                  }`}
                >
                  <span className="mb-1.5 h-5 w-5 rounded-full border border-white/15 shadow-sm" style={{ backgroundColor: option.swatch }} />
                  <span className="text-xs font-semibold text-slate-200">{option.label}</span>
                  {selected && <span className="absolute right-1.5 top-1.5 flex h-4 w-4 items-center justify-center rounded-full bg-slate-100 text-slate-950"><Check className="h-3 w-3" /></span>}
                </button>
              );
            })}
          </div>
        </fieldset>
      </div>
    </Dialog>
  );
};
