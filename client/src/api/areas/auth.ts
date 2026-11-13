import { get, post } from '../client'
import type { Me } from '../types'

export const authApi = {
  csrf: () => get<{ token: string }>('auth/csrf'),
  requestOtp: (phone: string) => post<void>('auth/otp/request', { phoneNumber: phone }),
  verifyOtp: (phone: string, code: string) => post<Me>('auth/otp/verify', { phoneNumber: phone, code }),
  logout: () => post<void>('auth/logout'),
  me: () => get<Me>('auth/me'),
}
