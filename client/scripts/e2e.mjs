// npm run e2e: the whole journey in a real browser, against the demo (mock) data.
// Shopkeeper signs up -> admin approves -> shopkeeper searches, orders and pays -> seller accepts and ships
// -> seller marks it reached -> shopkeeper confirms delivery -> seller payout shows.
// Screenshots of every step go to docs/e2e/. Exit code 1 if any step fails.
import fs from 'node:fs'
import path from 'node:path'
import { launch, login, pageErrors, startApp } from './lib.mjs'

const OUT = path.resolve('docs/e2e')
fs.mkdirSync(OUT, { recursive: true })
for (const f of fs.readdirSync(OUT)) if (f.endsWith('.png')) fs.rmSync(path.join(OUT, f)) // start clean: only this run's screenshots stay
const NAME = 'E2E Traders'

const app = await startApp()
const browser = await launch()
const ctx = await browser.newContext({ viewport: { width: 390, height: 844 } })
await ctx.addInitScript(() => {
  try {
    localStorage.setItem('tc_lang', 'en')
  } catch {
    /* storage blocked */
  }
})
const page = await ctx.newPage()
page.setDefaultTimeout(30_000)
const errors = []
await pageErrors(page, errors)

const results = []
let n = 0
async function step(title, fn) {
  n++
  const id = String(n).padStart(2, '0')
  try {
    await fn()
    await page.waitForTimeout(400)
    await page.screenshot({ path: path.join(OUT, `${id}-${title.toLowerCase().replace(/[^a-z0-9]+/g, '-')}.png`) })
    results.push(`ok   ${id} ${title}`)
  } catch (e) {
    results.push(`FAIL ${id} ${title} :: ${String(e.message).split('\n')[0]}`)
    try {
      await page.screenshot({ path: path.join(OUT, `${id}-FAILED.png`) })
    } catch {
      /* page gone */
    }
  }
}
const logout = async (accountPath) => {
  if (accountPath) await page.goto(app.url + accountPath)
  await page.getByRole('button', { name: /Log out/ }).first().click()
  await page.waitForURL('**/login')
  await page.locator('input[type=tel]').waitFor()
}

// 1. Shopkeeper signs up
await step('Shopkeeper logs in as a new user', async () => {
  await login(page, app.url, '9000000004', '**/register')
})
await step('Shopkeeper fills business details', async () => {
  await page.locator('input[autocomplete=organization]').fill(NAME)
  await page.locator('input[autocomplete=postal-code]').fill('190001')
  await page.getByText('Srinagar, Jammu & Kashmir').waitFor()
  await page.locator('input[autocomplete=address-line1]').fill('Main road, Lal Chowk')
  await page.locator('form button[type=submit]').click()
  await page.locator('input[type=file]').first().waitFor()
})
await step('Shopkeeper sends for approval', async () => {
  await page.getByRole('button', { name: /Send for approval/ }).click()
  await page.waitForURL('**/pending')
  await page.getByRole('heading', { level: 1 }).first().waitFor()
})
await step('Shopkeeper logs out', async () => {
  await logout('/shop/account')
})

// 2. Admin approves
await step('Admin logs in and sees approvals', async () => {
  await login(page, app.url, '9000000003', '**/admin')
  await page.goto(app.url + '/admin/businesses')
  await page.locator(`a:visible:has-text("${NAME}")`).first().waitFor()
})
await step('Admin approves the business', async () => {
  await page.locator(`a:visible:has-text("${NAME}")`).first().click()
  await page.waitForURL('**/admin/businesses/*')
  await page.getByRole('button', { name: /^Approve$/ }).click()
  await page.waitForURL('**/admin/businesses')
})
await step('Admin logs out', async () => {
  await page.goto(app.url + '/admin')
  await logout()
})

// 3. Shopkeeper searches, orders, pays
await step('Shopkeeper logs in (approved)', async () => {
  await login(page, app.url, '9000000004', '**/shop')
  await page.locator('a[href^="/shop/p/"]').first().waitFor()
})
await step('Shopkeeper searches for wool', async () => {
  await page.getByRole('searchbox').first().fill('wool')
  await page.getByRole('searchbox').first().press('Enter')
  await page.waitForURL('**/shop/products?q=wool')
  await page.locator('a[href^="/shop/p/"]').first().waitFor()
})
await step('Shopkeeper opens a product and picks 100 pieces', async () => {
  await page.locator('a[href^="/shop/p/"]').first().click()
  await page.waitForURL('**/shop/p/**')
  await page.getByRole('button', { name: '+50' }).click()
  await page.getByRole('button', { name: '+50' }).click()
  await page.getByText('You pay per piece').waitFor()
})
await step('Shopkeeper adds to cart', async () => {
  await page.locator('button:has-text("Add to cart")').last().click()
  await page.getByText('Added to cart').first().waitFor()
})
await step('Shopkeeper opens the cart', async () => {
  await page.getByRole('link', { name: /Cart/ }).first().click()
  await page.waitForURL('**/shop/cart')
  await page.locator('li').first().waitFor()
})
await step('Shopkeeper checks out and pays', async () => {
  await page.locator('a[href="/shop/checkout"]').click()
  await page.waitForURL('**/shop/checkout')
  await page.getByRole('radio').first().waitFor()
  await page.locator('button').filter({ hasText: /Place order/ }).click()
  await page.getByRole('heading', { level: 1 }).filter({ hasText: 'Order placed' }).waitFor()
})
await step('Shopkeeper logs out', async () => {
  await logout('/shop/account')
})

// 4. Seller accepts and ships
await step('Seller sees the new order', async () => {
  await login(page, app.url, '9000000002', '**/seller')
  await page.goto(app.url + '/seller/orders')
  await page.locator(`article:has-text("${NAME}")`).first().waitFor()
})
await step('Seller accepts the order', async () => {
  await page.locator(`article:has-text("${NAME}")`).first().getByRole('button', { name: 'Accept order' }).click()
  await page.getByRole('button', { name: /^To pack/ }).click()
  await page.locator(`article:has-text("${NAME}")`).first().waitFor()
})
await step('Seller ships with an LR number', async () => {
  await page.locator(`article:has-text("${NAME}")`).first().getByRole('button', { name: 'Ship with LR number' }).click()
  const dialog = page.getByRole('dialog')
  await dialog.waitFor()
  await dialog.locator('input').first().fill('LR-E2E-1')
  await dialog.getByRole('button', { name: /^Dispatch$/ }).click()
  await dialog.waitFor({ state: 'detached' })
})
await step('Seller marks it reached the shop city', async () => {
  await page.getByRole('button', { name: /^Shipped/ }).click()
  await page.locator(`article:has-text("${NAME}") a`).first().click()
  await page.waitForURL('**/seller/orders/*')
  await page.getByRole('button', { name: 'Reached the shop city' }).click()
  await page.getByText('Reached your city').first().waitFor()
})
await step('Seller logs out', async () => {
  await logout('/seller/account')
})

// 5. Shopkeeper confirms delivery
await step('Shopkeeper sees the confirm card', async () => {
  await login(page, app.url, '9000000004', '**/shop')
  await page.goto(app.url + '/shop/orders')
  await page.getByText('Needs your action').waitFor()
})
await step('Shopkeeper confirms delivery', async () => {
  await page.getByRole('button', { name: 'All good, confirm' }).click()
  await page.getByText(/Thank you/).first().waitFor()
})
await step('Shopkeeper logs out', async () => {
  await logout('/shop/account')
})

// 6. Seller payout shows
await step('Seller payout shows the order as paid', async () => {
  await login(page, app.url, '9000000002', '**/seller')
  await page.goto(app.url + '/seller/payouts')
  const row = page.locator(`li:has-text("${NAME}")`).first()
  await row.waitFor()
  await row.getByText('Paid', { exact: true }).waitFor()
})

await browser.close()
await app.close()

console.log(results.join('\n'))
console.log('--- browser errors ---')
console.log([...new Set(errors)].join('\n') || 'none')
const failed = results.some((r) => r.startsWith('FAIL')) || errors.length > 0
console.log(failed ? 'E2E FAILED' : `E2E PASSED (${results.length} steps). Screenshots: docs/e2e/`)
process.exit(failed ? 1 : 0)
