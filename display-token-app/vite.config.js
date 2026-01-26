import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({ mode }) => ({
  plugins: [react()],

  // No static base - the appDomain in /{appDomain}/tokens/ is dynamic
  // The app extracts appDomain from the URL at runtime
  base: '/',

  resolve: {
    dedupe: ['react', 'react-dom']
  },

  server: {
    port: 5313,
    strictPort: true,
    allowedHosts: ['localhost', 'longmanrd.net'],
    proxy: {
      '/config': {
        target: 'https://longmanrd.net',  // Just the server, no path
        changeOrigin: true,
        secure: true,
      } 
    }
  },

  preview: {
    port: 5314,
    strictPort: true,
  },
}))