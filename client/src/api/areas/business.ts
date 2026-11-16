import { get, isReal, post, upload } from '../client'
import type { AddressInput, BusinessAddress, BusinessDetail, BusinessDocument, CreateBusinessInput, DocType, PincodeInfo } from '../types'
import { fromE164, toE164 } from './auth'

interface RealAddress { id: number; type?: string; addressType?: string; line1: string; line2?: string | null; pincode: string; cityId: number; city?: string; state?: string; isDefault: boolean }
export const toAddress = (a: RealAddress): BusinessAddress => ({
  id: a.id, addressType: (a.addressType ?? a.type ?? 'Delivery') as BusinessAddress['addressType'], line1: a.line1, line2: a.line2 ?? undefined, cityId: a.cityId, pincode: a.pincode,
  isDefault: a.isDefault, cityName: a.city ?? '', stateName: a.state ?? '',
})

/** The shopkeeper's or seller's addresses with city and state names. */
export const accountAddresses = () => get<RealAddress[]>('account/addresses').then((l) => l.map(toAddress))

export const businessApi = {
  create: (input: CreateBusinessInput & { pincode?: string }) =>
    isReal('businesses')
      ? post<BusinessDetail>('businesses', {
          businessType: input.type, legalName: input.legalName, displayName: input.displayName, gstin: input.gstin || null, phone: toE164(input.phone), email: input.email ?? null, pincode: input.pincode,
        })
      : post<BusinessDetail>('businesses', input),
  me: async (): Promise<BusinessDetail> => {
    if (!isReal('businesses/me')) return get<BusinessDetail>('businesses/me')
    const [b, addresses] = await Promise.all([get<Record<string, unknown>>('businesses/me'), accountAddresses().catch(() => [] as BusinessAddress[])])
    return {
      id: b.id as number, type: (b.businessType ?? b.type) as BusinessDetail['type'], displayName: b.displayName as string, legalName: b.legalName as string, gstin: (b.gstin as string | null) ?? null,
      phone: fromE164(b.phone as string), email: (b.email as string | null) ?? null, verificationStatus: b.verificationStatus as BusinessDetail['verificationStatus'], rejectionNote: (b.rejectionNote as string | null) ?? null,
      addresses, documents: [],
    }
  },
  pincode: (pin: string) =>
    get<Record<string, unknown>>(`pincodes/${pin}`).then((r): PincodeInfo => ({ pincode: pin, cityId: r.cityId as number, cityName: (r.cityName ?? r.city) as string, stateId: r.stateId as number, stateName: (r.stateName ?? r.state) as string })),
  addAddress: (input: AddressInput) => post<RealAddress>('businesses/me/addresses', input).then(toAddress),
  addDocument: (docType: DocType, file: File) => {
    const form = new FormData()
    if (!isReal('businesses/me/documents')) form.append('docType', docType)
    form.append('file', file)
    return upload<BusinessDocument>('businesses/me/documents', form)
  },
}
