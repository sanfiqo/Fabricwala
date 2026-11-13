import path from 'node:path'
import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { VitePWA } from 'vite-plugin-pwa'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const target = env.VITE_API_PROXY_TARGET || 'http://localhost:5000'
  return {
    resolve: { alias: { '@': path.resolve(import.meta.dirname, 'src') } },
    plugins: [
      react(),
      tailwindcss(),
      VitePWA({
        registerType: 'autoUpdate',
        includeAssets: ['icon.svg'],
        manifest: {
          name: 'Textile Connect',
          short_name: 'TextileConnect',
          description: 'Wholesale textiles for Kashmir shopkeepers',
          theme_color: '#faf9f6',
          background_color: '#faf9f6',
          display: 'standalone',
          start_url: '/',
          icons: [
            { src: 'icon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' },
            { src: 'icon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'maskable' },
          ],
        },
        workbox: { navigateFallbackDenylist: [/^\/api/] },
      }),
    ],
    server: { proxy: { '/api': { target, changeOrigin: true } } },
  }
})
