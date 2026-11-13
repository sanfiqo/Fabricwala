import { del, get, post, put, upload } from '../client'
import type { SellerDashboard, PriceSlab, ProductDetail, ProductInput, ProductMedia, ProductSummary, SellerOrder } from '../types'

export const sellerProductApi = {
  list: () => get<ProductSummary[]>('seller/products'),
  get: (id: number) => get<ProductDetail>(`seller/products/${id}`),
  create: (input: ProductInput) => post<ProductDetail>('seller/products', input),
  update: (id: number, input: ProductInput) => put<ProductDetail>(`seller/products/${id}`, input),
  remove: (id: number) => del<void>(`seller/products/${id}`),
  saveSlabs: (id: number, slabs: PriceSlab[]) => put<PriceSlab[]>(`seller/products/${id}/slabs`, { slabs }),
  addMedia: (id: number, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return upload<ProductMedia>(`seller/products/${id}/media`, form)
  },
}

export const sellerOrderApi = {
  list: () => get<SellerOrder[]>('seller/orders'),
  get: (id: number) => get<SellerOrder>(`seller/orders/${id}`),
  accept: (id: number) => post<SellerOrder>(`seller/orders/${id}/accept`),
  reject: (id: number, reason: string) => post<SellerOrder>(`seller/orders/${id}/reject`, { reason }),
  dispatch: (id: number, lrNumber: string, carrierName: string) =>
    post<SellerOrder>(`seller/orders/${id}/dispatch`, { lrNumber, carrierName }),
  setStatus: (id: number, status: 'ReachedKashmir' | 'Delivered') => post<SellerOrder>(`seller/orders/${id}/status`, { status }),
}

export const sellerHomeApi = {
  dashboard: () => get<SellerDashboard>('seller/dashboard'),
}
