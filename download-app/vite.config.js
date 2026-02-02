import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({ mode }) => ({
  plugins: [react()],

  base: '/infoforum/downloads/',

  resolve: {
    dedupe: ['react', 'react-dom']
  },

  server: {
    port: 5315,
    strictPort: true,
    allowedHosts: ['localhost', 'longmanrd.net'],
    proxy: {
      '/config': {
        target: 'https://longmanrd.net',
        changeOrigin: true,
        secure: true,
      },
      '/api/downloads': {
        target: 'http://localhost:5004',
        changeOrigin: true,
      }
    }
  },

  preview: {
    port: 5316,
    strictPort: true,
  },
}))
