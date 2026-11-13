import { get, post, upload } from '../client'
import type { PincodeInfo, AddressInput, BusinessAddress, BusinessDetail, BusinessDocument, CreateBusinessInput, DocType } from '../types'

export const businessApi = {
  create: (input: CreateBusinessInput) => post<BusinessDetail>('businesses', input),
  me: () => get<BusinessDetail>('businesses/me'),
  /** TODO(BE): pincode lookup does not exist yet (see BACKEND_REQUESTS.md). */
  pincode: (pin: string) => get<PincodeInfo>(`pincodes/${pin}`),
  addAddress: (input: AddressInput) => post<BusinessAddress>('businesses/me/addresses', input),
  addDocument: (docType: DocType, file: File) => {
    const form = new FormData()
    form.append('docType', docType)
    form.append('file', file)
    return upload<BusinessDocument>('businesses/me/documents', form)
  },
}
