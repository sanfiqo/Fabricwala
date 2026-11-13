import { del, get, post, put } from '../client'
import type { Cart } from '../types'

export const cartApi = {
  get: () => get<Cart>('cart'),
  add: (variantId: number, quantity: number) => post<Cart>('cart/items', { variantId, quantity }),
  update: (itemId: number, quantity: number) => put<Cart>(`cart/items/${itemId}`, { quantity }),
  remove: (itemId: number) => del<Cart>(`cart/items/${itemId}`),
}
