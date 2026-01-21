import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({ mode }) => ({
  plugins: [react()],

  base: mode === 'production' ? '/tokens/' : '/',

  resolve: {
    dedupe: ['react', 'react-dom']
  },

  server: {
    port: 5313,
    strictPort: true,
    allowedHosts: ['localhost', 'sfddevelopment.com'],
    proxy: {
      '/config': {
        target: 'https://sfddevelopment.com',
        changeOrigin: true,
        secure: true,
        rewrite: (path) => path
      }
    }
  },

  preview: {
    port: 5314,
    strictPort: true,
  },
}))