import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig(({ command, mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  return {
    plugins: [react(), tailwindcss()],

    build: { sourcemap: mode === "dev" },

    // Base path - updated by deploy script
    base: '/infoforum/logs/',

    resolve: {
      dedupe: ['react', 'react-dom']
    },

    server: {
      port: parseInt(env.VITE_SERVER_PORT) || 5004,
      proxy: {
        '/config': {
          target: 'https://longmanrd.net',
          changeOrigin: true,
          secure: true
        },
        '/logger': {
          target: 'https://longmanrd.net',
          changeOrigin: true,
          secure: true
        }
      }
    }


  }
})