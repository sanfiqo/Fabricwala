import { ApiError } from './errors'
import { mockRequest } from './mock/router'

export { ApiError }

export const API_BASE = '/api/v1'
const MOCK_KEY = 'tc_use_mocks'

/** Mock mode: `?mock=1` / `?mock=0` in the URL or the switch in Account wins over VITE_USE_MOCKS. */
function readUseMocks(): boolean {
  try {
    const q = new URLSearchParams(window.location.search).get('mock')
    if (q === '1' || q === '0') localStorage.setItem(MOCK_KEY, q === '1' ? 'true' : 'false')
    const saved = localStorage.getItem(MOCK_KEY)
    if (saved === 'true' || saved === 'false') return saved === 'true'
  } catch {
    /* storage blocked: use the env value */
  }
  return import.meta.env.VITE_USE_MOCKS === 'true'
}
export const USE_MOCKS = readUseMocks()

/** Switch between sample data and the real backend. Reloads so every cache starts clean. */
export function setUseMocks(on: boolean) {
  try {
    localStorage.setItem(MOCK_KEY, on ? 'true' : 'false')
  } catch {
    /* storage blocked */
  }
  window.location.assign('/')
}

type Method = 'GET' | 'POST' | 'PUT' | 'DELETE'
export interface RequestOptions {
  query?: object
  body?: unknown
  form?: FormData
}

/** Fired on any 401 so the auth layer can clear the session. */
export const UNAUTHORIZED_EVENT = 'tc:unauthorized'

let csrfToken: string | null = null
let csrfPending: Promise<string> | null = null

async function loadCsrf(force = false): Promise<string> {
  if (csrfToken && !force) return csrfToken
  csrfPending ??= fetch(`${API_BASE}/auth/csrf`, { credentials: 'include' })
    .then(async (res) => {
      if (!res.ok) throw new ApiError(res.status, 'Could not get security token')
      const data = (await res.json()) as { token?: string; requestToken?: string }
      csrfToken = data.token ?? data.requestToken ?? ''
      return csrfToken
    })
    .finally(() => {
      csrfPending = null
    })
  return csrfPending
}

function buildUrl(path: string, query?: object) {
  const url = `${API_BASE}/${path.replace(/^\//, '')}`
  if (!query) return url
  const params = new URLSearchParams()
  for (const [k, v] of Object.entries(query)) {
    if (v !== undefined && v !== null && v !== '') params.set(k, String(v))
  }
  const qs = params.toString()
  return qs ? `${url}?${qs}` : url
}

async function toError(res: Response): Promise<ApiError> {
  let message = `Request failed (${res.status})`
  let fieldErrors: Record<string, string[]> | undefined
  try {
    const data = (await res.json()) as { detail?: string; title?: string; message?: string; errors?: Record<string, string[]> }
    message = data.detail || data.message || data.title || message
    fieldErrors = data.errors
  } catch {
    /* body is not JSON */
  }
  return new ApiError(res.status, message, fieldErrors)
}

async function send(method: Method, path: string, opts: RequestOptions, retry: boolean): Promise<Response> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  let body: BodyInit | undefined
  if (opts.form) body = opts.form
  else if (opts.body !== undefined) {
    headers['Content-Type'] = 'application/json'
    body = JSON.stringify(opts.body)
  }
  if (method !== 'GET') headers['X-CSRF-TOKEN'] = await loadCsrf()

  const res = await fetch(buildUrl(path, opts.query), { method, headers, body, credentials: 'include' })
  // Token may be stale (e.g. after login the cookie changes): fetch a fresh one once.
  if (method !== 'GET' && retry && res.status === 400) {
    const text = await res.clone().text()
    if (/antiforgery|csrf/i.test(text)) {
      await loadCsrf(true)
      return send(method, path, opts, false)
    }
  }
  return res
}

export async function api<T>(method: Method, path: string, opts: RequestOptions = {}): Promise<T> {
  if (USE_MOCKS) return mockRequest<T>(method, path, opts)

  const res = await send(method, path, opts, true)
  if (!res.ok) {
    if (res.status === 401) window.dispatchEvent(new Event(UNAUTHORIZED_EVENT))
    throw await toError(res)
  }
  // Login/logout change the session, so the old CSRF token is no longer valid.
  if (path.startsWith('auth/')) csrfToken = null
  if (res.status === 204) return undefined as T
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const get = <T>(path: string, query?: object) => api<T>('GET', path, { query })
export const post = <T>(path: string, body?: unknown) => api<T>('POST', path, { body })
export const put = <T>(path: string, body?: unknown) => api<T>('PUT', path, { body })
export const del = <T>(path: string) => api<T>('DELETE', path)
export const upload = <T>(path: string, form: FormData) => api<T>('POST', path, { form })
