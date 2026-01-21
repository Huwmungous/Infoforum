import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5029,
    proxy: {
      '/api': {
        target: 'http://localhost:5028',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:5028',
        changeOrigin: true,
        ws: true,
      },
    },
  },
  css: {
    preprocessorOptions: {
      scss: {
        api: 'modern-compiler',
      },
    },
  },
})
