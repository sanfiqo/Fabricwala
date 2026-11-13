import { api, get, post } from '../client'
import type { Order, PaymentIntent } from '../types'

export const orderApi = {
  /** `key` is an Idempotency-Key. Keep the same one for retries of the same order, so a double tap never places two orders. */
  checkout: (deliveryAddressId: number, key?: string) => api<Order>('POST', 'checkout', { body: { deliveryAddressId }, idempotencyKey: key }),
  list: () => get<Order[]>('orders'),
  get: (id: number) => get<Order>(`orders/${id}`),
  confirmDelivery: (id: number) => post<Order>(`orders/${id}/confirm-delivery`),
  /** Same rule for payments: one key per payment attempt, so a retry can never charge twice. */
  pay: (orderId: number, key?: string) => api<PaymentIntent>('POST', 'payments', { body: { orderId }, idempotencyKey: key }),
}
