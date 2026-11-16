import { isReal, post, upload } from '../client'
import type { Claim, ClaimInput, ClaimReason } from '../types'

// The real API names two reasons differently.
const REAL_REASON: Record<ClaimReason, string> = { Damaged: 'Damaged', WrongItem: 'WrongItem', LessQuantity: 'ShortQuantity', NotAsShown: 'QualityIssue', Other: 'Other' }

export const claimApi = {
  create: (sellerOrderId: number, input: ClaimInput) =>
    post<Claim>(`seller-orders/${sellerOrderId}/claims`, isReal('seller-orders') ? { ...input, reason: REAL_REASON[input.reason] } : input).then((c) => ({ ...c, sellerOrderId })),
  addPhoto: (claimId: number, file: File, sellerOrderId?: number) => {
    const form = new FormData()
    form.append('file', file)
    return upload<void>(sellerOrderId && isReal('seller-orders') ? `seller-orders/${sellerOrderId}/claims/${claimId}/photos` : `claims/${claimId}/photos`, form)
  },
}
