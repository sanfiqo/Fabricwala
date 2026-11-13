import { get, post, upload } from '../client'
import type { AddressInput, BusinessAddress, BusinessDetail, BusinessDocument, CreateBusinessInput, DocType } from '../types'

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
