import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig(({ command, mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  return {
    plugins: [react(), tailwindcss()],

    build: { sourcemap: mode === "dev" },

    base: command === 'serve' ? '/' : '/logs/',

    resolve: {
      dedupe: ['react', 'react-dom']
    },

    server: {
      port: parseInt(env.VITE_SERVER_PORT),
      proxy: {
        '/config': {
          target: env.VITE_CONFIG_SERVICE_URL,
          changeOrigin: true,
          rewrite: (path) => path.replace(/^\/config/, '')
        }
      }
    }
  }
})
