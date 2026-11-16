// npm run smoke: opens every screen in English at 390 px and in Hindi at 360 and 390 px.
// Fails on: sideways scrolling, browser errors, text that is cut off without "...".
// Admin screens are also opened at laptop width (1280 px). Demo (mock) data. Exit code 1 on any problem.
import { launch, login, pageErrors, startApp } from './lib.mjs'

const MATRIX = process.env.SMOKE_MATRIX ? process.env.SMOKE_MATRIX.split(',').map((x) => [x.split(':')[0], Number(x.split(':')[1])]) : [
  ['en', 390],
  ['hi', 360],
  ['hi', 390],
]
// SMOKE_ONLY=shop,seller,admin,public,new,pending limits the run (handy when fixing one area).
const ONLY = process.env.SMOKE_ONLY ? process.env.SMOKE_ONLY.split(',') : null
const want = (k) => !ONLY || ONLY.includes(k)

const app = await startApp()
const browser = await launch()
const problems = []
let screens = 0
let last = ''

/** Elements whose text is clipped (overflow hidden) but not shown with an ellipsis. */
const cutOff = () =>
  [...document.querySelectorAll('h1,h2,h3,p,span,a,button,label,li,dd,dt')]
    .filter((el) => {
      if (!(el instanceof HTMLElement) || el.closest('[aria-hidden="true"], .sr-only, .skeleton')) return false
      const cs = getComputedStyle(el)
      if (cs.display === 'none' || cs.visibility === 'hidden' || !el.textContent?.trim()) return false
      if (!['hidden', 'clip'].includes(cs.overflowX) || cs.textOverflow === 'ellipsis') return false
      if (cs.webkitLineClamp && cs.webkitLineClamp !== 'none') return false
      return el.scrollWidth > el.clientWidth + 2 && el.clientWidth > 0
    })
    .slice(0, 5)
    .map((el) => `${el.tagName.toLowerCase()} "${(el.textContent ?? '').trim().slice(0, 40)}"`)

async function check(page, label, sink) {
  last = label
  // Wait until skeletons are gone, then a moment for fonts and images.
  await page.waitForFunction(() => !document.querySelector('[role=status][aria-label]'), null, { timeout: 15_000 }).catch(() => undefined)
  await page.waitForTimeout(500)
  const width = await page.evaluate(() => [document.documentElement.scrollWidth, document.documentElement.clientWidth])
  if (width[0] > width[1] + 1) problems.push(`${label}: sideways scroll ${width[0]} > ${width[1]}`)
  const clipped = await page.evaluate(cutOff)
  if (clipped.length) problems.push(`${label}: text cut off: ${clipped.join('; ')}`)
  if (sink.length) {
    problems.push(`${label}: browser errors: ${[...new Set(sink)].join(' | ')}`)
    sink.length = 0
  }
  screens++
}

async function role(lang, width, phone, home, visits) {
  const ctx = await browser.newContext({ viewport: { width, height: 844 } })
  await ctx.addInitScript((l) => {
    try {
      localStorage.setItem('tc_lang', l)
    } catch {
      /* storage blocked */
    }
  }, lang)
  const page = await ctx.newPage()
  page.setDefaultTimeout(25_000)
  const sink = []
  await pageErrors(page, sink)
  const tag = `${lang}@${width} ${phone}`
  try {
    await login(page, app.url, phone, home)
    await check(page, `${tag} home`, sink)
    await visits(page, async (name) => check(page, `${tag} ${name}`, sink))
  } catch (e) {
    problems.push(`${tag}: after "${last}": ${String(e.message).split('\n')[0]}`)
  }
  await ctx.close()
}

const go = (page, path) => page.goto(app.url + path)
const first = (page, sel) => page.locator(`${sel}:visible`).first()

for (const [lang, width] of MATRIX) {
  // public screens
  if (want('public')) {
    const ctx = await browser.newContext({ viewport: { width, height: 844 } })
    await ctx.addInitScript((l) => localStorage.setItem('tc_lang', l), lang)
    const page = await ctx.newPage()
    const sink = []
    await pageErrors(page, sink)
    await go(page, '/login')
    await page.locator('input[type=tel]').waitFor()
    await check(page, `${lang}@${width} login`, sink)
    await page.locator('input[type=tel]').fill('9000000001')
    await page.locator('form button[type=submit]').click()
    await page.locator('input[autocomplete="one-time-code"]').waitFor()
    await check(page, `${lang}@${width} otp`, sink)
    await go(page, '/session-expired')
    await check(page, `${lang}@${width} session-expired`, sink)
    await ctx.close()
  }

  // new shopkeeper: register steps
  if (want('new')) await role(lang, width, '9000000004', '**/register', async (page, check) => {
    await page.locator('input[autocomplete=postal-code]').fill('190001')
    await page.locator('input[autocomplete=organization]').fill('Smoke Traders')
    await page.locator('input[autocomplete=address-line1]').fill('Main road')
    await page.waitForTimeout(600)
    await page.locator('form button[type=submit]').click()
    await page.locator('input[type=file]').first().waitFor()
    await check('register documents')
  })
  // waiting for approval
  if (want('pending')) await role(lang, width, '9000000005', '**/pending', async (page, check) => {
    await go(page, '/shop')
    await check('browse while waiting')
    await go(page, '/shop/account')
    await check('account while waiting')
  })

  // shopkeeper
  if (want('shop')) await role(lang, width, '9000000001', '**/shop', async (page, check) => {
    await go(page, '/shop/products?q=wool')
    await first(page, 'a[href^="/shop/p/"]').waitFor()
    await check('search')
    await page.locator('button').filter({ hasText: /Filters|फ़िल्टर/ }).first().click()
    await page.getByRole('dialog').waitFor()
    await check('filter sheet')
    await page.keyboard.press('Escape')
    await first(page, 'a[href^="/shop/p/"]').click()
    await page.waitForURL('**/shop/p/**')
    await page.locator('h1').first().waitFor()
    await check('product')
    await go(page, '/shop')
    await page.locator('button').filter({ hasText: /Order again|फिर से मंगाएं/ }).first().click()
    await page.waitForURL('**/shop/cart')
    await page.locator('li').first().waitFor()
    await check('cart')
    await go(page, '/shop/checkout')
    await page.getByRole('radio').first().waitFor()
    await check('checkout')
    await go(page, '/shop/orders')
    await first(page, 'a[href^="/shop/orders/"]:not([href$="/report"])').waitFor()
    await check('orders')
    await first(page, 'a[href^="/shop/orders/"]:not([href$="/report"])').click()
    await page.waitForURL('**/shop/orders/*')
    await check('track order')
    await go(page, '/shop/notifications')
    await check('notifications')
    await go(page, '/shop/account')
    await check('account')
  })

  // seller
  if (want('seller')) await role(lang, width, '9000000002', '**/seller', async (page, check) => {
    await go(page, '/seller/orders')
    await page.locator('article').first().waitFor()
    await check('orders new')
    for (const re of [/To pack|पैक करने हैं/, /Shipped|भेजे/, /Done|पूरे/]) {
      await page.getByRole('button', { name: re }).first().click()
      await page.waitForTimeout(300)
      await check(`orders tab ${re.source.slice(0, 8)}`)
    }
    await page.getByRole('button', { name: /^(New|नए)/ }).first().click()
    await page.locator('article a').first().click()
    await page.waitForURL('**/seller/orders/*')
    await check('order detail')
    await go(page, '/seller/products')
    await page.locator('[role=switch]').first().waitFor()
    await check('products')
    await go(page, '/seller/products/new')
    await page.waitForTimeout(800)
    await check('add product')
    await go(page, '/seller/products')
    await first(page, 'a[href^="/seller/products/"]').click()
    await page.waitForURL('**/seller/products/*')
    await page.waitForTimeout(800)
    await check('edit product')
    await go(page, '/seller/payouts')
    await check('payouts')
    await go(page, '/seller/notifications')
    await check('notifications')
    await go(page, '/seller/account')
    await check('account')
  })

  // admin (phone width of this pass)
  if (want('admin')) await role(lang, width, '9000000003', '**/admin', async (page, check) => {
    await go(page, '/admin/businesses')
    await first(page, 'a[href^="/admin/businesses/"]').waitFor()
    await check('approvals')
    await first(page, 'a[href^="/admin/businesses/"]').click()
    await page.waitForURL('**/admin/businesses/*')
    await page.locator('h1').first().waitFor()
    await check('review')
    await go(page, '/admin/settings')
    await check('settings')
    await go(page, '/admin/shipping')
    await check('freight')
  })
}

// admin on a laptop
if (want('admin')) {
  const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } })
  await ctx.addInitScript(() => localStorage.setItem('tc_lang', 'en'))
  const page = await ctx.newPage()
  const sink = []
  await pageErrors(page, sink)
  await login(page, app.url, '9000000003', '**/admin')
  await check(page, 'en@1280 admin home', sink)
  await go(page, '/admin/businesses')
  await first(page, 'a[href^="/admin/businesses/"]').waitFor()
  await check(page, 'en@1280 approvals table', sink)
  await first(page, 'a[href^="/admin/businesses/"]').click()
  await page.waitForURL('**/admin/businesses/*')
  await check(page, 'en@1280 review', sink)
  await ctx.close()
}

await browser.close()
await app.close()

console.log(`Screens checked: ${screens}`)
console.log(problems.length ? 'PROBLEMS:\n' + problems.join('\n') : 'No sideways scroll, no cut-off text, no browser errors.')
console.log(problems.length ? 'SMOKE FAILED' : 'SMOKE PASSED')
process.exit(problems.length ? 1 : 0)
