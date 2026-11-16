import { get, isReal } from '../client'
import type { Category, City, LandedCost, Page, ProductDetail, ProductQuery, ProductSummary, State } from '../types'

const unwrap = <T,>(v: T[] | { items: T[] }): T[] => (Array.isArray(v) ? v : v.items)

export const masterApi = {
  categories: () => get<Category[] | { items: Category[] }>('categories').then(unwrap),
  /** TODO(BE): no states or cities list exists. The pincode lookup replaces them for signup; admin freight still needs cities. */
  states: (): Promise<State[]> => (isReal('states') ? Promise.resolve([]) : get<State[]>('states')),
  cities: (zone?: 'delivery' | 'sourcing'): Promise<City[]> => (isReal('cities') ? Promise.resolve([]) : get<City[]>('cities', { zone })),
}

export const productApi = {
  list: (q: ProductQuery) => get<Page<ProductSummary>>('products', q),
  bySlug: (slug: string) => get<ProductDetail>(`products/${slug}`),
  landedCost: (productId: number, qty: number, cityId: number) => get<LandedCost>('landed-cost', { productId, qty, cityId }),
}
