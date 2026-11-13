import { api, get } from '../client'
import type { Payouts } from '../types'

export const payoutApi = {
  list: () => get<Payouts>('seller/payouts'),
  /** CSV for the accountant. TODO(BE): `?csv=true` exists per API_SCREENS.md; the client downloads the text it returns. */
  csv: () => api<string>('GET', 'seller/payouts', { query: { csv: true } }),
}
