import { del, get, post, put } from '../client'
import type { MyQuotationRow, Page, QuotationInput, RfqDetail, RfqInput, RfqSummary } from '../types'

/** Which optional parts of the platform are switched on (`{ "Rfq": true }`). RFQ screens only show when `Rfq` is on. */
export const featureApi = {
  all: () => get<Record<string, boolean>>('features'),
}

/** Retailer side: ask for quotations and pick one. Routes under `/rfqs`, see docs/RFQ.md. */
export const rfqApi = {
  list: (status?: string, page = 1) => get<Page<RfqSummary>>('rfqs', { status, page, pageSize: 50 }),
  get: (id: number) => get<RfqDetail>(`rfqs/${id}`),
  create: (input: RfqInput) => post<RfqDetail>('rfqs', input),
  cancel: (id: number) => post<RfqDetail>(`rfqs/${id}/cancel`),
  accept: (id: number, quotationId: number) => post<RfqDetail>(`rfqs/${id}/quotations/${quotationId}/accept`),
}

/** Manufacturer side: see open requests and send, change or withdraw one quotation per request. */
export const sellerRfqApi = {
  feed: (q?: string, categoryId?: number, page = 1) => get<Page<RfqSummary>>('seller/rfqs', { q, categoryId, page, pageSize: 50 }),
  get: (id: number) => get<RfqDetail>(`seller/rfqs/${id}`),
  quote: (id: number, input: QuotationInput) => put<RfqDetail>(`seller/rfqs/${id}/quotation`, input),
  withdraw: (id: number) => del<RfqDetail>(`seller/rfqs/${id}/quotation`),
  mine: (status?: string, page = 1) => get<Page<MyQuotationRow>>('seller/quotations', { status, page, pageSize: 50 }),
}
