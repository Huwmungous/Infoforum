import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

export default defineConfig(({ mode }) => ({
  plugins: [react()],

  // The appDomain in /{appDomain}/ifollama/ is dynamic
  // The app extracts appDomain from the URL at runtime
  base: '/infoforum/ifollama/',

  resolve: {
    dedupe: ['react', 'react-dom'],
    alias: {
      '@if/styles': path.resolve(__dirname, '../../IF.Styles'),
    },
  },

  server: {
    port: 5029,
    strictPort: true,
    allowedHosts: ['localhost', 'longmanrd.net'],
    proxy: {
      '/config': {
        target: 'https://longmanrd.net',
        changeOrigin: true,
        secure: true,
      },
      '/logger': {
        target: 'http://localhost:5001',
        changeOrigin: true,
        rewrite: (path: string) => path.replace(/^\/logger/, ''),
      },
      '/api': {
        target: 'http://localhost:5028',
        changeOrigin: true,
      },
      '/chathub': {
        target: 'http://localhost:5028',
        changeOrigin: true,
        ws: true,
      },
    },
  },

  preview: {
    port: 5030,
    strictPort: true,
  },

  css: {
    preprocessorOptions: {
      scss: {
        api: 'modern-compiler',
      },
    },
  },
}))
