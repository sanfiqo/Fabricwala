// Shared helpers for `npm run e2e` and `npm run smoke`.
// They start the app themselves (Vite dev server in demo mode) unless E2E_URL points at one that is already running.
import { chromium } from 'playwright-core'
import { createServer } from 'vite'

export async function startApp() {
  const external = process.env.E2E_URL
  if (external) return { url: external.replace(/\/$/, ''), close: async () => {} }
  // Demo mode on, whatever .env.local says, so the journey is repeatable.
  const server = await createServer({ root: process.cwd(), logLevel: 'error', server: { port: 5188, strictPort: false }, define: { 'import.meta.env.VITE_USE_MOCKS': '"true"' } })
  await server.listen()
  const url = (server.resolvedUrls?.local?.[0] ?? 'http://localhost:5188/').replace(/\/$/, '')
  return { url, close: () => server.close() }
}

export async function launch() {
  // PW_EXECUTABLE can point at any Chrome or Edge. By default the installed Edge is used.
  const opts = process.env.PW_EXECUTABLE ? { executablePath: process.env.PW_EXECUTABLE } : { channel: 'msedge' }
  return chromium.launch({ headless: true, ...opts })
}

/** Log in through the real screens (number, then the demo code). */
export async function login(page, base, phone, expectUrl) {
  await page.goto(base + '/login')
  await page.locator('input[type=tel]').fill(phone)
  await page.locator('form button[type=submit]').click()
  await page.locator('input[autocomplete="one-time-code"]').fill('123456')
  if (expectUrl) await page.waitForURL(expectUrl)
}

export async function pageErrors(page, sink) {
  page.on('pageerror', (e) => sink.push('PAGEERROR ' + e.message))
  page.on('console', (m) => {
    // Failed requests while a test goes offline on purpose are not bugs.
    if (m.type() === 'error' && !/ERR_INTERNET_DISCONNECTED/.test(m.text())) sink.push('CONSOLE ' + m.text())
  })
}
