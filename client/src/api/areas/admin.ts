import { del, get, post, put } from '../client'
import type { AdminBusiness, PlatformSettings, ShippingRate, ShippingRateInput, VerificationStatus } from '../types'

export const adminApi = {
  businesses: (status?: VerificationStatus) => get<AdminBusiness[]>('admin/businesses', { status }),
  approve: (id: number) => post<AdminBusiness>(`admin/businesses/${id}/approve`),
  reject: (id: number, note: string) => post<AdminBusiness>(`admin/businesses/${id}/reject`, { note }),
  settings: () => get<PlatformSettings>('admin/settings'),
  saveSettings: (s: PlatformSettings) => put<PlatformSettings>('admin/settings', s),
  rates: () => get<ShippingRate[]>('admin/shipping-rates'),
  createRate: (r: ShippingRateInput) => post<ShippingRate>('admin/shipping-rates', r),
  updateRate: (id: number, r: ShippingRateInput) => put<ShippingRate>(`admin/shipping-rates/${id}`, r),
  deleteRate: (id: number) => del<void>(`admin/shipping-rates/${id}`),
}
