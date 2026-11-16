import { del, get, isReal, post, put } from '../client'
import type { AdminBusiness, AdminDashboard, PlatformSettings, ShippingRate, ShippingRateInput, VerificationStatus } from '../types'
import { fromE164 } from './auth'

const unwrap = <T,>(v: T[] | { items: T[] }): T[] => (Array.isArray(v) ? v : v.items)

type Real = Record<string, unknown>
const toBusiness = (b: Real): AdminBusiness => ({
  id: b.id as number,
  type: (b.businessType ?? b.type) as AdminBusiness['type'],
  legalName: (b.legalName as string) ?? '',
  displayName: (b.displayName as string) ?? '',
  gstin: (b.gstin as string | null) ?? null,
  phone: fromE164(String(b.phone ?? '')),
  cityName: String(b.city ?? b.cityName ?? ''),
  verificationStatus: b.verificationStatus as VerificationStatus,
  rejectionNote: (b.rejectionNote as string | null) ?? null,
  createdAt: String(b.createdAtUtc ?? b.createdAt ?? ''),
  documents: ((b.documents as AdminBusiness['documents'] | undefined) ?? []),
})

interface Window { gmv: number; orders: number; commission: number; newBusinesses: number }
interface RealDash { today: Window; last7Days: Window; last30Days: Window; needsDecision: { pendingApprovals: number; openClaims: number } }

export const adminApi = {
  dashboard: async (days: number): Promise<AdminDashboard> => {
    const d = await get<RealDash | AdminDashboard>('admin/dashboard', { days })
    if (!('today' in d)) return d
    const w = days <= 1 ? d.today : days <= 7 ? d.last7Days : d.last30Days
    const pending = d.needsDecision.pendingApprovals
    const list = pending > 0 ? await get<{ items: Real[] }>('admin/businesses', { status: 'Pending' }).then((r) => r.items.map(toBusiness)) : []
    return {
      days, gmv: w.gmv, orders: w.orders, commission: w.commission, newBusinesses: w.newBusinesses, pendingApprovals: pending,
      decisions: list.slice(0, 5).map((b) => ({ kind: 'business' as const, title: b.displayName, sub: b.type, link: `/admin/businesses/${b.id}` })),
    }
  },
  businesses: (status?: VerificationStatus) => get<Real[] | { items: Real[] }>('admin/businesses', { status }).then((v) => unwrap(v).map(toBusiness)),
  /** One business with its documents. The list does not carry them. */
  business: async (id: number): Promise<AdminBusiness> => {
    if (!isReal('admin/businesses')) return (await adminApi.businesses()).find((b) => b.id === id) ?? Promise.reject(new Error('not found'))
    return get<Real>(`admin/businesses/${id}`).then(toBusiness)
  },
  approve: (id: number) => post<AdminBusiness>(`admin/businesses/${id}/approve`),
  reject: (id: number, note: string) => post<AdminBusiness>(`admin/businesses/${id}/reject`, { note }),
  settings: async (): Promise<PlatformSettings> => {
    const s = (await get<unknown>('admin/settings')) as Record<string, string>
    if ('commissionDefaultRate' in s) return s as unknown as PlatformSettings
    // The server keeps commission as a fraction (0.02 = 2%). Screens show percent.
    return { commissionDefaultRate: Math.round(Number(s['Commission.DefaultRate'] ?? 0) * 10000) / 100, settlementAutoReleaseDays: Number(s['Settlement.AutoReleaseDays'] ?? 7) }
  },
  saveSettings: async (s: PlatformSettings): Promise<PlatformSettings> => {
    if (!isReal('admin/settings')) return put<PlatformSettings>('admin/settings', s)
    await put<unknown>('admin/settings', { commissionDefaultRate: String(s.commissionDefaultRate / 100), settlementAutoReleaseDays: String(s.settlementAutoReleaseDays) })
    return s
  },
  /** Real rates have no id: they are keyed by from city, to city and rate type. The screens use a made-up id, built from the three. */
  rates: () =>
    get<ShippingRate[]>('admin/shipping-rates').then((l) => l.map((r) => ({ ...r, id: r.id ?? idOf(r.fromCityId, r.toCityId, r.rateType) }))),
  createRate: (r: ShippingRateInput) => post<ShippingRate>('admin/shipping-rates', r),
  updateRate: (id: number, r: ShippingRateInput) => (isReal('admin/shipping-rates') ? put<ShippingRate>(`admin/shipping-rates/${r.fromCityId}/${r.toCityId}/${r.rateType}`, r) : put<ShippingRate>(`admin/shipping-rates/${id}`, r)),
  deleteRate: (id: number, r?: ShippingRateInput) => (isReal('admin/shipping-rates') && r ? del<void>(`admin/shipping-rates/${r.fromCityId}/${r.toCityId}/${r.rateType}`) : del<void>(`admin/shipping-rates/${id}`)),
}
const idOf = (a: number, b: number, t: string) => a * 100_000 + b * 10 + (t === 'PerKg' ? 1 : 2)
