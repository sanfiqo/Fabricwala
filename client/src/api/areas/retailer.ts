import { ApiError, del, get, post, put } from '../client'
import type { OrderRating, Page, SavedProduct, Storefront } from '../types'

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

/** The shop's star rating of one order (1 to 5 and an optional comment). Allowed once an order is delivered; sending again changes it. */
export const ratingApi = {
  /** `null` when the order has not been rated yet (the server answers 404). */
  get: async (orderId: number): Promise<OrderRating | null> => {
    try {
      return await get<OrderRating>(`orders/${orderId}/rating`)
    } catch (e) {
      if (e instanceof ApiError && e.status === 404) return null
      throw e
    }
  },
  set: (orderId: number, stars: number, comment?: string) => post<OrderRating>(`orders/${orderId}/rating`, { stars, comment: comment || undefined }),
}
