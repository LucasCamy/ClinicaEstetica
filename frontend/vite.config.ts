import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      injectRegister: 'script',
      manifest: {
        name: 'Leilaine Arakaki | Estética',
        short_name: 'Leilaine Arakaki',
        description: 'Agenda, clientes e gestão da clínica de estética.',
        lang: 'pt-BR',
        start_url: '/',
        scope: '/',
        display: 'standalone',
        display_override: ['standalone', 'minimal-ui'],
        background_color: '#020617',
        theme_color: '#020617',
        categories: ['health', 'beauty', 'medical'],
        icons: [
          { src: 'pwa-192x192.png?v=2', sizes: '192x192', type: 'image/png' },
          { src: 'pwa-512x512.png?v=2', sizes: '512x512', type: 'image/png' },
          { src: 'maskable-icon-512x512.png?v=2', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
      },
      workbox: {
        cleanupOutdatedCaches: true,
        navigateFallback: 'index.html',
        navigateFallbackDenylist: [/^\/api\//],
        globPatterns: ['**/*.{js,css,html,ico,png,svg,jpg,jpeg,webmanifest,mjs,woff2}'],
      },
    }),
  ],
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
      '/health': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  }
});
