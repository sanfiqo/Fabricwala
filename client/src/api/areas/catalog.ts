import { get } from '../client'
import type { Category, City, LandedCost, Page, ProductDetail, ProductQuery, ProductSummary, State } from '../types'

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
