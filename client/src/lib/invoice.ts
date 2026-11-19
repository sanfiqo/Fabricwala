import { API_BASE, isMockFor } from '@/api/client'
import { S } from '@/i18n'

export type InvoiceType = 'SellerToRetailer' | 'PlatformCommission'

/**
 * Downloads the GST invoice PDF of one seller order (`GET /invoices/seller-orders/{id}/download`).
 * The retailer and the manufacturer get the tax invoice; only the manufacturer may ask for the platform fee invoice.
 * Throws an Error with a message that is safe to show.
 */
export async function downloadInvoice(sellerOrderId: number, type: InvoiceType = 'SellerToRetailer'): Promise<void> {
  const path = `invoices/seller-orders/${sellerOrderId}/download`
  if (isMockFor(path)) throw new Error(S.invoice.demo)
  let res: Response
  try {
    res = await fetch(`${API_BASE}/${path}?type=${type}`, { credentials: 'include' })
  } catch {
    throw new Error(S.errors.offline)
  }
  if (!res.ok) throw new Error(S.invoice.failed)
  const blob = await res.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `invoice-${sellerOrderId}${type === 'PlatformCommission' ? '-fee' : ''}.pdf`
  document.body.appendChild(a)
  a.click()
  a.remove()
  setTimeout(() => URL.revokeObjectURL(url), 10_000)
}
