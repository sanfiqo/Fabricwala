import { del, get, isReal, post, put, upload } from '../client'
import { toSellerOrder } from './orders'
import type { PriceSlab, ProductDetail, ProductInput, ProductMedia, ProductSummary, SellerDashboard, SellerOrder } from '../types'

const unwrap = <T,>(v: T[] | { items: T[] }): T[] => (Array.isArray(v) ? v : v.items)

interface RealSummary { id: number; title: string; slug: string; status: string; isActive?: boolean; moq: number; priceFrom: number; priceTo: number; imageUrl?: string | null }
const toSummary = (p: RealSummary): ProductSummary => ({
  id: p.id, slug: p.slug, title: p.title, categoryId: 0, sellerBusinessId: 0, sellerName: '', sellerCity: '', imageUrl: p.imageUrl ?? null, moq: p.moq, unit: 'Piece',
  priceFrom: p.priceFrom, priceTo: p.priceTo, status: (p.status === 'Active' && p.isActive === false ? 'Inactive' : p.status) as ProductSummary['status'],
})

type RealDetail = Omit<ProductDetail, 'slabs' | 'media'> & { tiers?: PriceSlab[]; slabs?: PriceSlab[]; media?: ProductMedia[]; isActive?: boolean }
const toDetail = (d: RealDetail): ProductDetail => ({
  ...d, slabs: d.slabs ?? d.tiers ?? [], media: d.media ?? [], sellerName: d.sellerName ?? '', sellerCity: d.sellerCity ?? '', imageUrl: d.imageUrl ?? null,
  priceFrom: d.priceFrom ?? 0, priceTo: d.priceTo ?? 0, status: (d.status === 'Active' && d.isActive === false ? 'Inactive' : d.status) as ProductDetail['status'],
})
const realBody = (i: ProductInput) => ({
  categoryId: i.categoryId, title: i.title, description: i.description, material: i.material, fabricSpec: i.fabricSpec, season: i.season, hsnCode: i.hsnCode, gstRate: i.gstRate,
  moq: i.moq, unit: i.unit, weightPerUnitGrams: i.weightPerUnitGrams, dispatchDays: i.dispatchDays, returnPolicy: i.returnPolicy, publish: i.status === 'Active',
  variants: i.variants.map((v) => ({ id: v.id, sku: v.sku || undefined, size: v.size, colour: v.colour, stockQty: v.stockQty, isActive: v.isActive })),
  tiers: (i.slabs ?? []).map((s) => ({ minQty: s.minQty, maxQty: s.maxQty, pricePerUnit: s.pricePerUnit })),
})

export const sellerProductApi = {
  list: () => get<ProductSummary[] | { items: RealSummary[] }>('seller/products').then((v) => (Array.isArray(v) ? v : unwrap(v).map(toSummary))),
  get: (id: number) => get<RealDetail>(`seller/products/${id}`).then(toDetail),
  create: async (input: ProductInput) => {
    if (isReal('seller/products')) return toDetail(await post<RealDetail>('seller/products', realBody(input)))
    const saved = await post<ProductDetail>('seller/products', input)
    if (input.slabs) await put<PriceSlab[]>(`seller/products/${saved.id}/slabs`, { slabs: input.slabs })
    return saved
  },
  update: async (id: number, input: ProductInput) => {
    if (isReal('seller/products')) return toDetail(await put<RealDetail>(`seller/products/${id}`, realBody(input)))
    const saved = await put<ProductDetail>(`seller/products/${id}`, input)
    if (input.slabs) await put<PriceSlab[]>(`seller/products/${id}/slabs`, { slabs: input.slabs })
    return saved
  },
  remove: (id: number) => del<void>(`seller/products/${id}`),
  /** Real API: PUT seller/products/{id}/active { active }. Sample data keeps its own status route. */
  setStatus: (id: number, status: 'Active' | 'Inactive') =>
    isReal('seller/products')
      ? put<unknown>(`seller/products/${id}/active`, { active: status === 'Active' }).then(() => ({ id, status }) as unknown as ProductSummary)
      : put<ProductSummary>(`seller/products/${id}/status`, { status }),
  /** TODO(BE): there is no photo upload route for one product (only bulk/images). */
  addMedia: (id: number, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return upload<ProductMedia>(`seller/products/${id}/media`, form)
  },
}

export const sellerOrderApi = {
  /** The real list has summaries only, so each order is read in full (up to 100). */
  list: async (): Promise<SellerOrder[]> => {
    const l = await get<SellerOrder[] | { items: Array<{ id: number }> }>('seller/orders', isReal('seller/orders') ? { status: 'all', pageSize: 100 } : undefined)
    if (Array.isArray(l)) return l
    return Promise.all(l.items.map((s) => get<SellerOrder>(`seller/orders/${s.id}`).then(toSellerOrder)))
  },
  get: (id: number) => get<SellerOrder>(`seller/orders/${id}`).then(toSellerOrder),
  accept: (id: number) => post<SellerOrder>(`seller/orders/${id}/accept`).then(toSellerOrder),
  reject: (id: number, reason: string) => post<SellerOrder>(`seller/orders/${id}/reject`, { reason }).then(toSellerOrder),
  dispatch: (id: number, lrNumber: string, carrierName: string) => post<SellerOrder>(`seller/orders/${id}/dispatch`, { lrNumber, carrierName }).then(toSellerOrder),
  /** Sample data only. The real seller routes cannot mark "reached" or "delivered": the shopkeeper confirms delivery. */
  setStatus: (id: number, status: 'ReachedKashmir' | 'Delivered') => post<SellerOrder>(`seller/orders/${id}/status`, { status }).then(toSellerOrder),
}
/** True when the seller can mark the parcel as reached or delivered (sample data only). */
export const sellerCanMarkDelivery = () => !isReal('seller/orders')

interface RealDash {
  newOrders: number | SellerDashboard['newOrders']
  toPack: number
  salesThisMonth: number
  nextPayoutAmount?: number
  nextPayoutDate?: string | null
  monthlySales?: Array<{ month: string; sales: number }>
  lowStock?: { items: Array<{ productId: number; title: string; colour: string; size: string; stockQty: number }> }
  bestSellers?: Array<Record<string, unknown>>
}
const monthName = (ym: string) => new Date(`${ym}-01T00:00:00`).toLocaleDateString('en-IN', { month: 'short' })

export const sellerHomeApi = {
  dashboard: async (): Promise<SellerDashboard> => {
    const d = await get<RealDash | SellerDashboard>('seller/dashboard')
    if (typeof (d as RealDash).newOrders !== 'number') return d as SellerDashboard
    const r = d as RealDash
    const months = (r.monthlySales ?? []).map((m) => ({ month: monthName(m.month), amount: m.sales }))
    const last = months.at(-1)?.amount ?? 0
    const prev = months.at(-2)?.amount ?? 0
    // Value, cities and the accept deadline come from the waiting orders themselves.
    let value = 0
    let cities: string[] = []
    let acceptBy: string | null = null
    if ((r.newOrders as number) > 0) {
      const waiting = (await sellerOrderApi.list().catch(() => [] as SellerOrder[])).filter((o) => o.status === 'PendingAcceptance')
      value = waiting.reduce((a, o) => a + o.subTotal, 0)
      cities = [...new Set(waiting.map((o) => o.deliveryCity).filter(Boolean))]
      const first = waiting.map((o) => new Date(o.placedAt).getTime()).sort()[0]
      acceptBy = first ? new Date(first + 24 * 3_600_000).toISOString() : null
    }
    return {
      newOrders: { count: r.newOrders as number, value, cities, acceptBy }, toPack: r.toPack, salesThisMonth: r.salesThisMonth, salesChangePct: prev > 0 ? Math.round(((last - prev) / prev) * 100) : null,
      payoutDue: r.nextPayoutAmount ?? 0, nextPayoutDate: r.nextPayoutDate ?? null, rating: null, ratingCount: 0, salesByMonth: months,
      lowStock: (r.lowStock?.items ?? []).map((l) => ({ productId: l.productId, title: l.title, variant: `${l.colour} ${l.size}`, qty: l.stockQty })),
      topProducts: (r.bestSellers ?? []).map((b) => ({ productId: Number(b.productId ?? 0), title: String(b.title ?? ''), pieces: Number(b.pieces ?? b.quantity ?? b.unitsSold ?? 0), amount: Number(b.amount ?? b.sales ?? b.revenue ?? 0) })),
    }
  },
}
