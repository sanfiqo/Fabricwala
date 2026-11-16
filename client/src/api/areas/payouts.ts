import { api, get } from '../client'
import type { PayoutRow, Payouts } from '../types'

interface RealPayouts {
  nextPayoutAmount: number
  nextPayoutDate: string | null
  onHoldAmount: number
  paidThisMonth: number
  payouts: { items: Array<Record<string, unknown>> }
}

const str = (v: unknown) => (v === undefined || v === null ? '' : String(v))
const toRow = (r: Record<string, unknown>, i: number): PayoutRow => {
  const raw = str(r.status).toLowerCase()
  return {
    id: Number(r.sellerOrderId ?? r.id ?? i),
    orderNumber: str(r.orderNumber ?? r.subOrderNumber ?? r.sellerOrderNumber),
    buyer: str(r.retailerName ?? r.buyer ?? r.retailer),
    placedAt: str(r.placedAt ?? r.createdAt ?? r.orderedAt ?? r.releaseDate ?? r.paidAt),
    deliveredAt: (r.deliveredAt as string | null | undefined) ?? null,
    amount: Number(r.amount ?? r.netAmount ?? r.payoutAmount ?? 0),
    status: r.paidAt || raw === 'paid' || raw.includes('settled') ? 'Paid' : raw.includes('hold') ? 'OnHold' : 'Scheduled',
    date: (r.paidAt ?? r.releaseDate ?? r.settledAt ?? r.date ?? null) as string | null,
  }
}

export const payoutApi = {
  list: async (): Promise<Payouts> => {
    const d = await get<RealPayouts | Payouts>('seller/payouts')
    if (!('payouts' in d)) return d
    return { next: { amount: d.nextPayoutAmount, date: d.nextPayoutDate }, onHold: d.onHoldAmount, paidThisMonth: d.paidThisMonth, rows: d.payouts.items.map(toRow) }
  },
  csv: () => api<string>('GET', 'seller/payouts', { query: { csv: true } }),
}
