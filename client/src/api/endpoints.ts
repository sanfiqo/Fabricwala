import { del, get, post, put, upload } from './client'
import type {
  AddressInput, AdminBusiness, BusinessAddress, BusinessDetail, BusinessDocument, Cart, Category, City, CreateBusinessInput,
  DocType, LandedCost, Me, Order, Page, PaymentIntent, PlatformSettings, PriceSlab, ProductDetail, ProductInput, ProductMedia,
  ProductQuery, ProductSummary, SellerOrder, ShippingRate, ShippingRateInput, State, VerificationStatus,
} from './types'

export const authApi = {
  csrf: () => get<{ token: string }>('auth/csrf'),
  requestOtp: (phone: string) => post<void>('auth/otp/request', { phoneNumber: phone }),
  verifyOtp: (phone: string, code: string) => post<Me>('auth/otp/verify', { phoneNumber: phone, code }),
  logout: () => post<void>('auth/logout'),
  me: () => get<Me>('auth/me'),
}

export const businessApi = {
  create: (input: CreateBusinessInput) => post<BusinessDetail>('businesses', input),
  me: () => get<BusinessDetail>('businesses/me'),
  addAddress: (input: AddressInput) => post<BusinessAddress>('businesses/me/addresses', input),
  addDocument: (docType: DocType, file: File) => {
    const form = new FormData()
    form.append('docType', docType)
    form.append('file', file)
    return upload<BusinessDocument>('businesses/me/documents', form)
  },
}

export const masterApi = {
  categories: () => get<Category[]>('categories'),
  states: () => get<State[]>('states'),
  cities: (zone?: 'delivery' | 'sourcing') => get<City[]>('cities', { zone }),
}

export const productApi = {
  list: (q: ProductQuery) => get<Page<ProductSummary>>('products', q),
  bySlug: (slug: string) => get<ProductDetail>(`products/${slug}`),
  landedCost: (productId: number, qty: number, cityId: number) => get<LandedCost>('landed-cost', { productId, qty, cityId }),
}

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

export const cartApi = {
  get: () => get<Cart>('cart'),
  add: (variantId: number, quantity: number) => post<Cart>('cart/items', { variantId, quantity }),
  update: (itemId: number, quantity: number) => put<Cart>(`cart/items/${itemId}`, { quantity }),
  remove: (itemId: number) => del<Cart>(`cart/items/${itemId}`),
}

export const orderApi = {
  checkout: (deliveryAddressId: number) => post<Order>('checkout', { deliveryAddressId }),
  list: () => get<Order[]>('orders'),
  get: (id: number) => get<Order>(`orders/${id}`),
  confirmDelivery: (id: number) => post<Order>(`orders/${id}/confirm-delivery`),
  pay: (orderId: number) => post<PaymentIntent>('payments', { orderId }),
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
