// npm run e2e:real: the whole journey against the REAL backend (http://127.0.0.1:5075, Development, fake payment gateway).
// Three browser windows (shopkeeper, admin, seller) stay signed in, so the OTP limits of the API are not hit.
// Shopkeeper signs up -> admin approves -> shopkeeper searches, orders, pays -> seller accepts and ships
// -> shopkeeper confirms delivery -> seller payout shows. Screenshots: docs/e2e-real/. Exit code 1 on any failure.
import fs from 'node:fs'
import path from 'node:path'
import { launch, pageErrors, startApp } from './lib.mjs'

process.env.E2E_REAL = '1'
const OUT = path.resolve('docs/e2e-real')
fs.mkdirSync(OUT, { recursive: true })
for (const f of fs.readdirSync(OUT)) if (f.endsWith('.png')) fs.rmSync(path.join(OUT, f))

// A fresh mobile number every run (the API rejects a second business for the same user).
const NEW_PHONE = '99999' + String(Date.now()).slice(-5)
const NAME = 'E2E Traders ' + NEW_PHONE.slice(-4)
const PHONES = { shop: NEW_PHONE, admin: '9999900000', seller: '9999900002' }

const app = await startApp()
const browser = await launch()
const errors = []
const win = {}
for (const role of Object.keys(PHONES)) {
  const ctx = await browser.newContext({ viewport: { width: role === 'admin' ? 1280 : 390, height: role === 'admin' ? 800 : 844 } })
  await ctx.addInitScript(() => {
    try {
      localStorage.setItem('tc_lang', 'en')
    } catch {
      /* storage blocked */
    }
  })
  const page = await ctx.newPage()
  page.setDefaultTimeout(30_000)
  await pageErrors(page, errors)
  win[role] = { page }
}

const results = []
let n = 0
async function step(role, title, fn) {
  n++
  const id = String(n).padStart(2, '0')
  const { page } = win[role]
  try {
    await fn(page)
    await page.waitForTimeout(400)
    await page.screenshot({ path: path.join(OUT, `${id}-${role}-${title.toLowerCase().replace(/[^a-z0-9]+/g, '-')}.png`) })
    results.push(`ok   ${id} [${role}] ${title}`)
  } catch (e) {
    results.push(`FAIL ${id} [${role}] ${title} :: ${String(e.message).split('\n')[0]}`)
    try {
      await page.screenshot({ path: path.join(OUT, `${id}-${role}-FAILED.png`) })
    } catch {
      /* page gone */
    }
  }
}
const logIn = async (page, phone, url) => {
  await page.goto(app.url + '/login')
  await page.locator('input[type=tel]').fill(phone)
  await page.locator('form button[type=submit]').click()
  await page.locator('input[autocomplete="one-time-code"]').fill('123456')
  await page.waitForURL(url)
}

// 1. Shopkeeper signs up
await step('shop', 'logs in as a new user', (p) => logIn(p, PHONES.shop, '**/register'))
await step('shop', 'fills business details (pincode 452001)', async (p) => {
  await p.locator('input[autocomplete=organization]').fill(NAME)
  await p.locator('input[autocomplete=postal-code]').fill('452001')
  await p.getByText('Indore, Madhya Pradesh').waitFor()
  await p.locator('input[autocomplete=address-line1]').fill('12 Market Road')
  await p.locator('form button[type=submit]').click()
  await p.locator('input[type=file]').first().waitFor()
})
await step('shop', 'sends for approval', async (p) => {
  await p.getByRole('button', { name: /Send for approval/ }).click()
  await p.waitForURL('**/pending')
  await p.getByRole('heading', { level: 1 }).first().waitFor()
})

// 2. Admin approves
await step('admin', 'logs in and sees approvals', async (p) => {
  await logIn(p, PHONES.admin, '**/admin')
  await p.goto(app.url + '/admin/businesses')
  await p.locator(`a:visible:has-text("${NAME}")`).first().waitFor()
})
await step('admin', 'opens the review page', async (p) => {
  await p.locator(`a:visible:has-text("${NAME}")`).first().click()
  await p.waitForURL('**/admin/businesses/*')
  await p.locator('h1').first().waitFor()
})
await step('admin', 'approves the business', async (p) => {
  await p.getByRole('button', { name: /^Approve$/ }).click()
  await p.waitForURL('**/admin/businesses')
})

// 3. Shopkeeper checks again, searches, orders, pays
await step('shop', 'checks again and gets in', async (p) => {
  await p.getByRole('button', { name: /Check again/ }).click()
  await p.waitForURL('**/shop')
  await p.locator('a[href^="/shop/p/"]').first().waitFor()
})
await step('shop', 'searches for puffer', async (p) => {
  await p.getByRole('searchbox').first().fill('puffer')
  await p.getByRole('searchbox').first().press('Enter')
  await p.waitForURL('**/shop/products?q=puffer')
  await p.locator('a[href^="/shop/p/"]').first().waitFor()
})
await step('shop', 'opens the product and picks 50 pieces', async (p) => {
  await p.locator('a[href^="/shop/p/"]').first().click()
  await p.waitForURL('**/shop/p/**')
  await p.getByRole('button', { name: '+50' }).click()
  await p.getByText('You pay per piece').waitFor()
})
await step('shop', 'adds to cart', async (p) => {
  await p.locator('button:has-text("Add to cart")').last().click()
  await p.getByText('Added to cart').first().waitFor()
})
await step('shop', 'opens the cart', async (p) => {
  await p.getByRole('link', { name: /Cart/ }).first().click()
  await p.waitForURL('**/shop/cart')
  await p.locator('li').first().waitFor()
})
await step('shop', 'checks out and pays (fake gateway)', async (p) => {
  await p.locator('a[href="/shop/checkout"]').click()
  await p.waitForURL('**/shop/checkout')
  await p.getByRole('radio').first().waitFor()
  await p.locator('button').filter({ hasText: /Place order/ }).click()
  await p.getByRole('heading', { level: 1 }).filter({ hasText: 'Order placed' }).waitFor()
})

// 4. Seller accepts and ships
await step('seller', 'sees the new order', async (p) => {
  await logIn(p, PHONES.seller, '**/seller')
  await p.goto(app.url + '/seller/orders')
  await p.locator(`article:has-text("${NAME}")`).first().waitFor()
})
await step('seller', 'accepts the order', async (p) => {
  await p.locator(`article:has-text("${NAME}")`).first().getByRole('button', { name: 'Accept order' }).click()
  await p.getByRole('button', { name: /^To pack/ }).click()
  await p.locator(`article:has-text("${NAME}")`).first().waitFor()
})
await step('seller', 'ships with an LR number', async (p) => {
  await p.locator(`article:has-text("${NAME}")`).first().getByRole('button', { name: 'Ship with LR number' }).click()
  const dialog = p.getByRole('dialog')
  await dialog.waitFor()
  await dialog.locator('input').first().fill('LR-E2E-1')
  await dialog.getByRole('button', { name: /^Dispatch$/ }).click()
  await dialog.waitFor({ state: 'detached' })
})

// 5. Shopkeeper confirms delivery
await step('shop', 'sees the confirm card', async (p) => {
  await p.goto(app.url + '/shop/orders')
  await p.getByText('Needs your action').waitFor()
})
await step('shop', 'confirms delivery', async (p) => {
  await p.getByRole('button', { name: 'All good, confirm' }).click()
  await p.getByText(/Thank you/).first().waitFor()
})

// 6. Seller payout shows
await step('seller', 'payout shows the order', async (p) => {
  await p.goto(app.url + '/seller/payouts')
  await p.locator('li:has-text("TC-")').first().waitFor()
})

await browser.close()
await app.close()
console.log(results.join('\n'))
console.log('--- browser errors ---')
console.log([...new Set(errors)].join('\n') || 'none')
const failed = results.some((r) => r.startsWith('FAIL')) || errors.length > 0
console.log(failed ? 'E2E (REAL API) FAILED' : `E2E (REAL API) PASSED (${results.length} steps). Screenshots: docs/e2e-real/`)
process.exit(failed ? 1 : 0)
