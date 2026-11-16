import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { fileURLToPath, URL } from 'node:url';

const target =
  process.env['RISKCASE_API_URL'] ||
  process.env['services__webapi__https__0'] ||
  process.env['services__webapi__http__0'] ||
  'http://localhost:5250';

const proxyOptions = target
  ? { target, secure: false, changeOrigin: true }
  : undefined;
const port = Number(process.env.PORT ?? 5173);

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port,
    strictPort: true,
    proxy: proxyOptions
      ? {
          '/api': proxyOptions,
          '/openapi': proxyOptions,
          '/scalar': proxyOptions,
        }
      : undefined,
  },
  build: {
    outDir: 'dist',
  },
});
