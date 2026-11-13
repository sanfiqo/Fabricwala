// After the first screen is drawn, fetch the other shopkeeper screens in the background.
// Then a tap never shows a loading flash, even on slow 4G.
export function preloadShopRoutes() {
  const run = () => {
    void import('@/pages/shop/ProductPage')
    void import('@/pages/shop/CartPage')
    void import('@/pages/shop/OrdersPage')
    void import('@/pages/shop/ProductListPage')
    void import('@/pages/shop/AccountPage')
    void import('@/components/ui/sheet-impl')
  }
  const idle = (window as Window & { requestIdleCallback?: (cb: () => void, o?: { timeout: number }) => number }).requestIdleCallback
  if (idle) idle(run, { timeout: 4000 })
  else window.setTimeout(run, 2000)
}
