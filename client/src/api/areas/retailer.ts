import { del, get, put } from '../client'
import type { Page, SavedProduct, Storefront } from '../types'

/** Saved products (the retailer's wishlist). Routes: `/saved-products`, see docs/API_REFERENCE.md. */
export const savedApi = {
  list: (page = 1) => get<Page<SavedProduct>>('saved-products', { page, pageSize: 50 }),
  save: (productId: number) => put<{ saved: boolean }>(`saved-products/${productId}`),
  remove: (productId: number) => del<void>(`saved-products/${productId}`),
}

/** Public seller page: business, rating and live products. */
export const storefrontApi = {
  get: (businessId: number) => get<Storefront>(`storefronts/${businessId}`),
}
