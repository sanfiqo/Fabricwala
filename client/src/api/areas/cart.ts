import { del, get, isReal, post, put } from '../client'
import type { Cart } from '../types'
import { accountAddresses } from './business'

/** The real cart has no addresses; checkout needs them, so they are added from the account. */
async function withAddresses(c: Cart): Promise<Cart> {
  if (!isReal('cart') || (c.addresses?.length ?? 0) > 0) return c
  const all = await accountAddresses().catch(() => [])
  return { ...c, addresses: all.filter((a) => a.addressType === 'Delivery') }
}

export const cartApi = {
  get: () => get<Cart>('cart').then(withAddresses),
  add: (variantId: number, quantity: number) => post<Cart>('cart/items', { variantId, quantity }).then(withAddresses),
  update: (itemId: number, quantity: number) => put<Cart>(`cart/items/${itemId}`, { quantity }).then(withAddresses),
  remove: (itemId: number) => del<Cart>(`cart/items/${itemId}`).then(withAddresses),
}
