import { S } from '@/i18n'

const BY_STATUS: Record<number, keyof typeof S.errors> = {
  0: 'offline', 400: 'invalid', 401: 'sessionExpired', 403: 'forbidden', 404: 'notFound', 409: 'conflict', 422: 'invalid', 429: 'tooMany',
}

/** User-facing text for a backend error `code` (or, failing that, the HTTP status). Raw server text is never shown. */
export function friendlyMessage(status: number, code?: string): string {
  const table = S.errors as Record<string, string>
  if (code && table[code]) return table[code]
  if (status >= 500) return S.errors.server
  return S.errors[BY_STATUS[status] ?? 'generic']
}

export class ApiError extends Error {
  status: number
  code?: string
  fieldErrors?: Record<string, string[]>
  /** What the server said. For logs and debugging only, never shown to the user. */
  serverMessage: string
  constructor(status: number, serverMessage: string, fieldErrors?: Record<string, string[]>, code?: string) {
    super(friendlyMessage(status, code))
    this.status = status
    this.code = code
    this.fieldErrors = fieldErrors
    this.serverMessage = serverMessage
  }
}
