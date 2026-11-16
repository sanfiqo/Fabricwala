// Query keys, grouped by area. The first element is the area name used for stale times and for the offline cache (see queryClient.ts).
// Keep the first element stable: the saved offline copy is matched on it.
import type { ProductQuery } from './types'

export const qk = {
  me: ['me'] as const,
  businessMe: ['business-me'] as const,
  categories: ['categories'] as const,
  products: (q?: ProductQuery) => (q ? (['products', q] as const) : (['products'] as const)),
  product: (slug: string) => ['product', slug] as const,
  landed: (productId: number, qty: number, cityId?: number) => ['landed', productId, qty, cityId] as const,
  cart: ['cart'] as const,
  orders: ['orders'] as const,
  order: (id: number) => ['orders', id] as const,
  sellerOrders: ['seller-orders'] as const,
  sellerOrder: (id: number) => ['seller-orders', id] as const,
  sellerProducts: ['seller-products'] as const,
  sellerProduct: (id: number) => ['seller-products', id] as const,
  sellerDashboard: ['seller-dashboard'] as const,
  payouts: ['payouts'] as const,
  notifications: ['notifications'] as const,
  unread: ['notifications', 'unread'] as const,
  adminBusinesses: (status?: string) => (status ? (['admin-businesses', status] as const) : (['admin-businesses'] as const)),
  adminDashboard: ['admin-dashboard'] as const,
}

/** How long data counts as fresh, per area. Money and order state are always re-read. */
export const STALE = {
  master: 10 * 60_000, // categories, states, cities
  catalog: 60_000,
  live: 0, // cart, orders, seller orders, payouts, notifications
} as const
