import { api, get, isReal, post } from '../client'
import type { Cart, Order, PaymentIntent, SellerOrder, SellerOrderStatus } from '../types'
import { accountAddresses } from './business'

type Real = Record<string, any> // eslint-disable-line @typescript-eslint/no-explicit-any

/** Real percentages are fractions (0.02 = 2%). The screens show percent. */
const pct = (r: number) => (r <= 1 ? Math.round(r * 10000) / 100 : r)
// The real history says "Pending" for the first two steps.
const hist = (s: string): SellerOrderStatus => (s === 'Pending' ? 'PendingAcceptance' : (s as SellerOrderStatus))

export function toSellerOrder(o: Real): SellerOrder {
  if (!isReal('orders')) return o as SellerOrder
  return {
    ...(o as SellerOrder),
    status: o.status,
    commissionRate: pct(Number(o.commissionRate ?? 0)),
    history: (o.history ?? []).map((h: Real) => ({ ...h, status: hist(h.status) })),
    shipment: o.shipment ?? null,
    manufacturerName: o.manufacturerName ?? '',
    retailerName: o.retailerName ?? '',
  }
}

export function toOrder(o: Real): Order {
  if (!isReal('orders')) return o as Order
  const a = o.deliveryAddress ?? {}
  return {
    ...(o as Order),
    deliveryAddress: { id: 0, addressType: 'Delivery', line1: a.line1 ?? '', line2: a.line2 ?? undefined, cityId: 0, pincode: a.pincode ?? '', isDefault: false, cityName: a.city ?? a.cityName ?? '', stateName: a.stateName ?? '' },
    sellerOrders: (o.sellerOrders ?? []).map(toSellerOrder),
  }
}

export const orderApi = {
  /** `key` is an Idempotency-Key. Keep the same one for retries of the same order, so a double tap never places two orders. */
  checkout: (deliveryAddressId: number, key?: string) => api<Real>('POST', 'checkout', { body: { deliveryAddressId }, idempotencyKey: key }).then(toOrder),
  /** The real list only has summaries, so each order is read in full (up to 100, newest first). */
  list: async (): Promise<Order[]> => {
    const l = await get<Real[] | { items: Real[] }>('orders', isReal('orders') ? { pageSize: 100 } : undefined)
    if (Array.isArray(l)) return l.map(toOrder)
    return Promise.all(l.items.map((s) => get<Real>(`orders/${s.id}`).then(toOrder)))
  },
  get: (id: number) => get<Real>(`orders/${id}`).then(toOrder),
  confirmDelivery: (id: number) => post<Real>(`orders/${id}/confirm-delivery`).then(toOrder),
  /** Same rule for payments: one key per payment attempt, so a retry can never charge twice. */
  pay: (orderId: number, key?: string) => api<PaymentIntent & { status?: string }>('POST', 'payments', { body: { orderId }, idempotencyKey: key }),
  /** Development only: the fake gateway has no screen, this completes the payment. The route does not exist in production. */
  devComplete: (gatewayOrderId: string, success = true) => post<{ status: string }>(`payments/${gatewayOrderId}/dev-complete${success ? '' : '?success=false'}`),
  /** Put the items of an old order in the cart. The server skips what is gone and says why. */
  reorder: (id: number) => post<{ cart: Cart; skipped: Array<{ title: string; reason: string | null }> }>(`orders/${id}/reorder`),
}

export { accountAddresses }
