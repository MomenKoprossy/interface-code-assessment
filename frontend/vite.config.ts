import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

// Aspire injects PORT and services__api__http__0; plain `npm run dev` falls back to the API's launchSettings port.
const port = Number(process.env.PORT) || 5173;
const apiTarget = process.env.services__api__http__0 || process.env.API_URL || 'http://localhost:5080';
const proxy = { '/api': { target: apiTarget, changeOrigin: true } };

export default defineConfig({
  plugins: [react()],
  server: { port, proxy },
  preview: { port, proxy },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    restoreMocks: true,
  },
});
