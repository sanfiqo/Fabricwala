import path from 'node:path'
import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { VitePWA } from 'vite-plugin-pwa'
import type { Plugin } from 'vite'

/** Preloads the Latin font file for the first paint (its name has a hash, so it is found at build time). */
function fontPreload(): Plugin {
  return {
    name: 'font-preload',
    transformIndexHtml(_html, ctx) {
      const file = Object.keys(ctx.bundle ?? {}).find((f) => /inter-latin-wght-normal.*\.woff2$/.test(f))
      if (!file) return []
      return [{ tag: 'link', attrs: { rel: 'preload', as: 'font', type: 'font/woff2', crossorigin: '', href: '/' + file }, injectTo: 'head' }]
    },
  }
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const target = env.VITE_API_PROXY_TARGET || 'http://localhost:5000'
  return {
    resolve: { alias: { '@': path.resolve(import.meta.dirname, 'src') } },
    plugins: [
      react(),
      tailwindcss(),
      fontPreload(),
      VitePWA({
        registerType: 'prompt',
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
        workbox: {
          navigateFallbackDenylist: [/^\/api/],
          // Product photos are opened again and again on a slow phone: keep them.
          runtimeCaching: [
            { urlPattern: ({ request }) => request.destination === 'image', handler: 'CacheFirst', options: { cacheName: 'images', expiration: { maxEntries: 120, maxAgeSeconds: 30 * 24 * 3600 } } },
          ],
        },
      }),
    ],
    server: { proxy: { '/api': { target, changeOrigin: true } } },
  }
})
