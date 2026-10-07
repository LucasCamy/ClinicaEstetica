/** @type {import('tailwindcss').Config} */
const withAlpha = (variable) => `rgb(var(${variable}) / <alpha-value>)`;

export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        rose: {
          50: withAlpha('--accent-50'),
          100: withAlpha('--accent-100'),
          200: withAlpha('--accent-200'),
          300: withAlpha('--accent-300'),
          400: withAlpha('--accent-400'),
          500: withAlpha('--accent-500'),
          600: withAlpha('--accent-600'),
          700: withAlpha('--accent-700'),
          800: withAlpha('--accent-800'),
          900: withAlpha('--accent-900'),
          950: withAlpha('--accent-950'),
        },
        slate: {
          50: withAlpha('--slate-50'),
          100: withAlpha('--slate-100'),
          200: withAlpha('--slate-200'),
          300: withAlpha('--slate-300'),
          400: withAlpha('--slate-400'),
          500: withAlpha('--slate-500'),
          600: withAlpha('--slate-600'),
          700: withAlpha('--slate-700'),
          800: withAlpha('--slate-800'),
          900: withAlpha('--slate-900'),
          950: withAlpha('--slate-950'),
        },
        amber: {
          50: withAlpha('--warning-50'),
          100: withAlpha('--warning-100'),
          200: withAlpha('--warning-200'),
          300: withAlpha('--warning-300'),
          400: withAlpha('--warning-400'),
          500: withAlpha('--warning-500'),
          600: withAlpha('--warning-600'),
          700: withAlpha('--warning-700'),
          800: withAlpha('--warning-800'),
          900: withAlpha('--warning-900'),
          950: withAlpha('--warning-950'),
        },
        gold: {
          400: '#e0a96d',
          500: '#d4af37',
          600: '#c59b27',
        },
        cream: {
          50: '#fbf8f5',
          100: '#f5efe6',
          200: '#e6d7c3',
        }
      },
      fontFamily: {
        sans: ['Inter', 'sans-serif'],
        display: ['Outfit', 'sans-serif'],
      }
    },
  },
  plugins: [],
}
