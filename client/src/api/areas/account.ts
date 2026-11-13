import { post, upload } from '../client'
import type { Claim, ClaimInput } from '../types'

export const claimApi = {
  create: (sellerOrderId: number, input: ClaimInput) => post<Claim>(`seller-orders/${sellerOrderId}/claims`, input),
  addPhoto: (claimId: number, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return upload<void>(`claims/${claimId}/photos`, form)
  },
}
