import { get, post } from '../client'
import type { Order, PaymentIntent } from '../types'

export const orderApi = {
  checkout: (deliveryAddressId: number) => post<Order>('checkout', { deliveryAddressId }),
  list: () => get<Order[]>('orders'),
  get: (id: number) => get<Order>(`orders/${id}`),
  confirmDelivery: (id: number) => post<Order>(`orders/${id}/confirm-delivery`),
  pay: (orderId: number) => post<PaymentIntent>('payments', { orderId }),
}
