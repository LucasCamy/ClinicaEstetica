import React, { createContext, useContext, useEffect, useMemo, useState } from 'react';

export type ThemeMode = 'light' | 'dark' | 'system';
export type AccentColor = 'neutral' | 'stone' | 'zinc' | 'gray' | 'slate' | 'red' | 'orange' | 'blue' | 'green' | 'violet' | 'rose';

export interface AccentOption {
  id: AccentColor;
  label: string;
  swatch: string;
}

interface ThemeContextValue {
  mode: ThemeMode;
  resolvedTheme: 'light' | 'dark';
  accent: AccentColor;
  setMode: (mode: ThemeMode) => void;
  setAccent: (accent: AccentColor) => void;
}

const STORAGE_KEY = 'painel-estetica.appearance.v1';

export const accentOptions: AccentOption[] = [
  { id: 'neutral', label: 'Neutro', swatch: '#737373' },
  { id: 'stone', label: 'Pedra', swatch: '#78716c' },
  { id: 'zinc', label: 'Zinco', swatch: '#71717a' },
  { id: 'gray', label: 'Cinza', swatch: '#6b7280' },
  { id: 'slate', label: 'Ardósia', swatch: '#475569' },
  { id: 'red', label: 'Vermelho', swatch: '#ef4444' },
  { id: 'orange', label: 'Laranja', swatch: '#f97316' },
  { id: 'blue', label: 'Azul', swatch: '#2563eb' },
  { id: 'green', label: 'Verde', swatch: '#16a34a' },
  { id: 'violet', label: 'Violeta', swatch: '#7c3aed' },
  { id: 'rose', label: 'Rosa', swatch: '#e11d48' },
];

type ColorScale = Record<'50' | '100' | '200' | '300' | '400' | '500' | '600' | '700' | '800' | '900' | '950', string>;

interface SurfacePalette {
  slate: ColorScale;
  panel: string;
  card: string;
  border: string;
  shadow: string;
  themeColor: string;
}

interface AppearancePalette {
  dark: SurfacePalette;
  light: SurfacePalette;
}

const accentScales: Record<AccentColor, ColorScale> = {
  neutral: { 50: '250 250 250', 100: '245 245 245', 200: '229 229 229', 300: '212 212 212', 400: '163 163 163', 500: '115 115 115', 600: '82 82 82', 700: '64 64 64', 800: '38 38 38', 900: '23 23 23', 950: '10 10 10' },
  stone: { 50: '250 250 249', 100: '245 245 244', 200: '231 229 228', 300: '214 211 209', 400: '168 162 158', 500: '120 113 108', 600: '87 83 78', 700: '68 64 60', 800: '41 37 36', 900: '28 25 23', 950: '12 10 9' },
  zinc: { 50: '250 250 250', 100: '244 244 245', 200: '228 228 231', 300: '212 212 216', 400: '161 161 170', 500: '113 113 122', 600: '82 82 91', 700: '63 63 70', 800: '39 39 42', 900: '24 24 27', 950: '9 9 11' },
  gray: { 50: '249 250 251', 100: '243 244 246', 200: '229 231 235', 300: '209 213 219', 400: '156 163 175', 500: '107 114 128', 600: '75 85 99', 700: '55 65 81', 800: '31 41 55', 900: '17 24 39', 950: '3 7 18' },
  slate: { 50: '248 250 252', 100: '241 245 249', 200: '226 232 240', 300: '203 213 225', 400: '148 163 184', 500: '100 116 139', 600: '71 85 105', 700: '51 65 85', 800: '30 41 59', 900: '15 23 42', 950: '2 6 23' },
  red: { 50: '254 242 242', 100: '254 226 226', 200: '254 202 202', 300: '252 165 165', 400: '248 113 113', 500: '239 68 68', 600: '220 38 38', 700: '185 28 28', 800: '153 27 27', 900: '127 29 29', 950: '69 10 10' },
  orange: { 50: '255 247 237', 100: '255 237 213', 200: '254 215 170', 300: '253 186 116', 400: '251 146 60', 500: '249 115 22', 600: '234 88 12', 700: '194 65 12', 800: '154 52 18', 900: '124 45 18', 950: '67 20 7' },
  blue: { 50: '239 246 255', 100: '219 234 254', 200: '191 219 254', 300: '147 197 253', 400: '96 165 250', 500: '59 130 246', 600: '37 99 235', 700: '29 78 216', 800: '30 64 175', 900: '30 58 138', 950: '23 37 84' },
  green: { 50: '240 253 244', 100: '220 252 231', 200: '187 247 208', 300: '134 239 172', 400: '74 222 128', 500: '34 197 94', 600: '22 163 74', 700: '21 128 61', 800: '22 101 52', 900: '20 83 45', 950: '5 46 22' },
  violet: { 50: '245 243 255', 100: '237 233 254', 200: '221 214 254', 300: '196 181 253', 400: '167 139 250', 500: '139 92 246', 600: '124 58 237', 700: '109 40 217', 800: '91 33 182', 900: '76 29 149', 950: '46 16 101' },
  rose: { 50: '255 241 242', 100: '255 228 230', 200: '254 205 211', 300: '253 164 175', 400: '251 113 133', 500: '244 63 94', 600: '225 29 72', 700: '190 18 60', 800: '159 18 57', 900: '136 19 55', 950: '76 5 25' },
};

// Amarelo tem significado funcional (atenção/pendência), por isso é independente
// da paleta escolhida. Os tons abaixo preservam contraste tanto em telas claras
// quanto escuras e evitam que um aviso concorra com a identidade visual do tema.
const warningScales: Record<'light' | 'dark', ColorScale> = {
  dark: { 50: '255 250 235', 100: '254 243 199', 200: '253 230 138', 300: '246 204 113', 400: '224 166 75', 500: '190 126 45', 600: '149 91 25', 700: '112 64 16', 800: '78 42 11', 900: '50 27 8', 950: '30 16 4' },
  light: { 50: '69 43 7', 100: '91 58 11', 200: '120 76 16', 300: '148 96 25', 400: '180 120 42', 500: '208 158 74', 600: '230 191 119', 700: '244 220 165', 800: '252 238 203', 900: '255 248 232', 950: '255 253 247' },
};

const surfaceScale = (...tones: string[]): ColorScale => {
  const keys: Array<keyof ColorScale> = ['50', '100', '200', '300', '400', '500', '600', '700', '800', '900', '950'];
  return Object.fromEntries(keys.map((key, index) => [key, tones[index]])) as ColorScale;
};

const appearancePalettes: Record<AccentColor, AppearancePalette> = {
  neutral: {
    dark: { slate: surfaceScale('250 250 250', '245 245 245', '229 229 229', '212 212 212', '163 163 163', '115 115 115', '82 82 82', '64 64 64', '38 38 38', '23 23 23', '10 10 10'), panel: '23 23 23', card: '38 38 38', border: '255 255 255', shadow: '10 10 10', themeColor: '#0a0a0a' },
    light: { slate: surfaceScale('24 24 27', '39 39 42', '63 63 70', '82 82 91', '113 113 122', '161 161 170', '212 212 216', '228 228 231', '244 244 245', '250 250 250', '255 255 255'), panel: '255 255 255', card: '244 244 245', border: '39 39 42', shadow: '228 228 231', themeColor: '#ffffff' },
  },
  stone: {
    dark: { slate: surfaceScale('250 250 249', '245 245 244', '231 229 228', '214 211 209', '168 162 158', '120 113 108', '87 83 78', '68 64 60', '41 37 36', '28 25 23', '12 10 9'), panel: '28 25 23', card: '41 37 36', border: '250 250 249', shadow: '12 10 9', themeColor: '#0c0a09' },
    light: { slate: surfaceScale('41 37 36', '68 64 60', '87 83 78', '120 113 108', '168 162 158', '214 211 209', '231 229 228', '245 245 244', '250 250 249', '253 252 251', '255 255 254'), panel: '255 255 254', card: '250 250 249', border: '68 64 60', shadow: '231 229 228', themeColor: '#fffffe' },
  },
  zinc: {
    dark: { slate: surfaceScale('250 250 250', '244 244 245', '228 228 231', '212 212 216', '161 161 170', '113 113 122', '82 82 91', '63 63 70', '39 39 42', '24 24 27', '9 9 11'), panel: '24 24 27', card: '39 39 42', border: '250 250 250', shadow: '9 9 11', themeColor: '#09090b' },
    light: { slate: surfaceScale('24 24 27', '39 39 42', '63 63 70', '82 82 91', '113 113 122', '161 161 170', '212 212 216', '228 228 231', '244 244 245', '250 250 250', '255 255 255'), panel: '255 255 255', card: '244 244 245', border: '39 39 42', shadow: '228 228 231', themeColor: '#ffffff' },
  },
  gray: {
    dark: { slate: surfaceScale('249 250 251', '243 244 246', '229 231 235', '209 213 219', '156 163 175', '107 114 128', '75 85 99', '55 65 81', '31 41 55', '17 24 39', '3 7 18'), panel: '17 24 39', card: '31 41 55', border: '249 250 251', shadow: '3 7 18', themeColor: '#030712' },
    light: { slate: surfaceScale('17 24 39', '31 41 55', '55 65 81', '75 85 99', '107 114 128', '156 163 175', '209 213 219', '229 231 235', '243 244 246', '249 250 251', '255 255 255'), panel: '255 255 255', card: '243 244 246', border: '31 41 55', shadow: '229 231 235', themeColor: '#ffffff' },
  },
  slate: {
    dark: { slate: surfaceScale('248 250 252', '241 245 249', '226 232 240', '203 213 225', '148 163 184', '100 116 139', '71 85 105', '51 65 85', '30 41 59', '15 23 42', '2 6 23'), panel: '15 23 42', card: '30 41 59', border: '255 255 255', shadow: '2 6 23', themeColor: '#020617' },
    light: { slate: surfaceScale('15 23 42', '30 41 59', '51 65 85', '71 85 105', '100 116 139', '148 163 184', '203 213 225', '226 232 240', '241 245 249', '248 250 252', '255 255 255'), panel: '255 255 255', card: '241 245 249', border: '30 41 59', shadow: '226 232 240', themeColor: '#ffffff' },
  },
  red: {
    dark: { slate: surfaceScale('255 248 248', '250 240 240', '234 218 219', '204 183 185', '166 143 146', '126 103 107', '92 72 76', '65 50 53', '46 34 37', '30 22 24', '16 10 11'), panel: '30 22 24', card: '46 34 37', border: '250 240 240', shadow: '16 10 11', themeColor: '#100a0b' },
    light: { slate: surfaceScale('69 10 10', '127 29 29', '153 27 27', '185 28 28', '220 38 38', '248 113 113', '252 165 165', '254 202 202', '254 226 226', '254 242 242', '255 251 251'), panel: '255 251 251', card: '254 242 242', border: '127 29 29', shadow: '254 226 226', themeColor: '#fffafb' },
  },
  orange: {
    dark: { slate: surfaceScale('255 249 241', '255 239 221', '255 217 176', '251 181 112', '239 140 70', '204 100 37', '162 70 22', '116 46 17', '75 29 12', '45 17 8', '27 9 3'), panel: '45 17 8', card: '75 29 12', border: '255 239 221', shadow: '27 9 3', themeColor: '#1b0903' },
    light: { slate: surfaceScale('67 20 7', '124 45 18', '154 52 18', '194 65 12', '234 88 12', '251 146 60', '253 186 116', '254 215 170', '255 237 213', '255 247 237', '255 252 248'), panel: '255 252 248', card: '255 247 237', border: '124 45 18', shadow: '255 237 213', themeColor: '#fffaf6' },
  },
  blue: {
    dark: { slate: surfaceScale('239 246 255', '219 234 254', '191 219 254', '147 197 253', '104 160 229', '70 122 203', '44 91 165', '29 63 122', '18 42 82', '10 27 55', '4 14 31'), panel: '10 27 55', card: '18 42 82', border: '219 234 254', shadow: '4 14 31', themeColor: '#040e1f' },
    light: { slate: surfaceScale('23 37 84', '30 58 138', '30 64 175', '29 78 216', '37 99 235', '96 165 250', '147 197 253', '191 219 254', '219 234 254', '239 246 255', '249 252 255'), panel: '249 252 255', card: '239 246 255', border: '30 58 138', shadow: '219 234 254', themeColor: '#f9fcff' },
  },
  green: {
    dark: { slate: surfaceScale('247 253 249', '235 246 238', '214 231 220', '181 204 190', '143 169 153', '105 134 117', '75 101 86', '52 73 61', '37 51 43', '24 34 29', '12 20 16'), panel: '24 34 29', card: '37 51 43', border: '235 246 238', shadow: '12 20 16', themeColor: '#0c1410' },
    light: { slate: surfaceScale('5 46 22', '20 83 45', '22 101 52', '21 128 61', '22 163 74', '74 222 128', '134 239 172', '187 247 208', '220 252 231', '240 253 244', '250 255 252'), panel: '250 255 252', card: '240 253 244', border: '20 83 45', shadow: '220 252 231', themeColor: '#fafffc' },
  },
  violet: {
    dark: { slate: surfaceScale('248 246 255', '237 231 255', '220 210 253', '190 171 240', '151 124 217', '116 86 180', '84 57 142', '58 38 103', '39 24 70', '25 14 47', '14 7 29'), panel: '25 14 47', card: '39 24 70', border: '237 231 255', shadow: '14 7 29', themeColor: '#0e071d' },
    light: { slate: surfaceScale('46 16 101', '76 29 149', '91 33 182', '109 40 217', '124 58 237', '167 139 250', '196 181 253', '221 214 254', '237 233 254', '245 243 255', '252 250 255'), panel: '252 250 255', card: '245 243 255', border: '76 29 149', shadow: '237 233 254', themeColor: '#fcfaff' },
  },
  rose: {
    dark: { slate: surfaceScale('255 248 250', '249 240 243', '232 217 222', '202 182 189', '164 142 150', '124 102 111', '91 72 80', '65 50 57', '46 35 40', '30 22 26', '16 10 13'), panel: '30 22 26', card: '46 35 40', border: '249 240 243', shadow: '16 10 13', themeColor: '#100a0d' },
    light: { slate: surfaceScale('76 5 25', '136 19 55', '159 18 57', '190 18 60', '225 29 72', '251 113 133', '253 164 175', '254 205 211', '255 228 230', '255 241 242', '255 251 252'), panel: '255 251 252', card: '255 241 242', border: '136 19 55', shadow: '255 228 230', themeColor: '#fffbfc' },
  },
};

const ThemeContext = createContext<ThemeContextValue | null>(null);

const isThemeMode = (value: unknown): value is ThemeMode => value === 'light' || value === 'dark' || value === 'system';
const isAccentColor = (value: unknown): value is AccentColor => accentOptions.some(option => option.id === value);

const updateFavicon = (accent: AccentColor) => {
  const color = accentOptions.find(option => option.id === accent)?.swatch ?? '#e11d48';
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" fill="none"><rect width="64" height="64" fill="${color}"/><path d="M32 13l2.6 10.4L45 26l-10.4 2.6L32 39l-2.6-10.4L19 26l10.4-2.6L32 13zM17.5 37l1.4 5.6 5.6 1.4-5.6 1.4-1.4 5.6-1.4-5.6-5.6-1.4 5.6-1.4 1.4-5.6zM47.5 39l1.3 5.2 5.2 1.3-5.2 1.3-1.3 5.2-1.3-5.2-5.2-1.3 5.2-1.3 1.3-5.2z" fill="white"/></svg>`;
  let icon = document.querySelector<HTMLLinkElement>('link[rel="icon"]');
  if (!icon) {
    icon = document.createElement('link');
    icon.rel = 'icon';
    document.head.appendChild(icon);
  }
  icon.type = 'image/svg+xml';
  icon.href = `data:image/svg+xml,${encodeURIComponent(svg)}`;
};

const readPreference = (): Pick<ThemeContextValue, 'mode' | 'accent'> => {
  try {
    const parsed = JSON.parse(window.localStorage.getItem(STORAGE_KEY) ?? '{}') as { mode?: unknown; accent?: unknown };
    return {
      mode: isThemeMode(parsed.mode) ? parsed.mode : 'system',
      accent: isAccentColor(parsed.accent) ? parsed.accent : 'rose',
    };
  } catch {
    return { mode: 'system', accent: 'rose' };
  }
};

export const ThemeProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const preference = readPreference();
  const [mode, setMode] = useState<ThemeMode>(preference.mode);
  const [accent, setAccent] = useState<AccentColor>(preference.accent);
  const [systemTheme, setSystemTheme] = useState<'light' | 'dark'>(() =>
    window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark',
  );
  const resolvedTheme = mode === 'system' ? systemTheme : mode;

  useEffect(() => {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: light)');
    const updateSystemTheme = (event: MediaQueryListEvent | MediaQueryList) => setSystemTheme(event.matches ? 'light' : 'dark');
    mediaQuery.addEventListener('change', updateSystemTheme);
    return () => mediaQuery.removeEventListener('change', updateSystemTheme);
  }, []);

  useEffect(() => {
    const root = document.documentElement;
    const palette = appearancePalettes[accent][resolvedTheme];
    root.dataset.theme = resolvedTheme;
    root.dataset.accent = accent;
    root.style.colorScheme = resolvedTheme;
    Object.entries(accentScales[accent]).forEach(([tone, value]) => root.style.setProperty(`--accent-${tone}`, value));
    Object.entries(warningScales[resolvedTheme]).forEach(([tone, value]) => root.style.setProperty(`--warning-${tone}`, value));
    Object.entries(palette.slate).forEach(([tone, value]) => root.style.setProperty(`--slate-${tone}`, value));
    root.style.setProperty('--panel-rgb', palette.panel);
    root.style.setProperty('--card-rgb', palette.card);
    root.style.setProperty('--glass-border-rgb', palette.border);
    root.style.setProperty('--page-shadow-rgb', palette.shadow);
    root.style.setProperty('--content-primary', palette.slate['50']);
    document.querySelector('meta[name="theme-color"]')?.setAttribute('content', palette.themeColor);
    updateFavicon(accent);
  }, [accent, resolvedTheme]);

  useEffect(() => {
    try {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify({ mode, accent }));
    } catch {
      // A aparência continua funcional mesmo se o navegador bloquear o armazenamento local.
    }
  }, [mode, accent]);

  const value = useMemo(() => ({ mode, resolvedTheme, accent, setMode, setAccent }), [mode, resolvedTheme, accent]);
  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
};

export const useTheme = (): ThemeContextValue => {
  const context = useContext(ThemeContext);
  if (!context) throw new Error('useTheme deve ser usado dentro de ThemeProvider.');
  return context;
};
