// "Order again" remembers what each item cost last time, so the cart can say "Price updated: now X (was Y)".
// Kept in sessionStorage: it only matters for the cart you see right after tapping the button.
const KEY = 'tc_reorder_prices'

export function saveReorderPrices(prices: Record<number, number>) {
  try {
    sessionStorage.setItem(KEY, JSON.stringify(prices))
  } catch {
    /* storage blocked: the cart just will not show the notice */
  }
}

export function readReorderPrices(): Record<number, number> {
  try {
    return JSON.parse(sessionStorage.getItem(KEY) ?? '{}') as Record<number, number>
  } catch {
    return {}
  }
}

export function clearReorderPrices() {
  try {
    sessionStorage.removeItem(KEY)
  } catch {
    /* nothing to clear */
  }
}
