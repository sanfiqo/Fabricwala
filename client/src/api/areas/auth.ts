import { get, isReal, post } from '../client'
import type { Me } from '../types'

/** The backend wants +91 and 10 digits. Screens work with the 10 digits. */
export const toE164 = (phone: string) => (phone.startsWith('+') ? phone : `+91${phone}`)
export const fromE164 = (phone: string | null | undefined) => (phone ?? '').replace(/^\+91/, '')

const me = (m: Me): Me => ({ ...m, phone: fromE164(m.phone) })

export const authApi = {
  csrf: () => get<{ token: string }>('auth/csrf'),
  requestOtp: (phone: string) => post<void>('auth/otp/request', { phoneNumber: isReal('auth/otp/request') ? toE164(phone) : phone }),
  verifyOtp: (phone: string, code: string) => post<Me>('auth/otp/verify', { phoneNumber: isReal('auth/otp/verify') ? toE164(phone) : phone, code }).then(me),
  logout: () => post<void>('auth/logout'),
  me: () => get<Me>('auth/me').then(me),
  /** Renews the cookie, for example right after an admin approved the business. */
  refresh: () => post<void>('auth/refresh'),
}
