/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    tailwindcss()
  ],
  server: {
    port: 5173,
    strictPort: true,
    // Mismo origen en desarrollo: el navegador solo habla con Vite y Vite reenvía /api al backend.
    // Igual que hará Caddy en producción. El certificado de desarrollo de .NET es autofirmado.
    proxy: {
      '/api': {
        target: process.env.VITE_PROXY_TARGET ?? 'https://localhost:7216',
        secure: false,
        changeOrigin: false,
      },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'html']
    }
  }
})
