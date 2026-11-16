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

// ---- Per area switch. Areas are the adapter files in ./areas (one file each).
export type ApiArea = 'auth' | 'business' | 'catalog' | 'cart' | 'orders' | 'seller' | 'payouts' | 'notifications' | 'account' | 'admin'
const AREA_PREFIXES: Array<[RegExp, ApiArea]> = [
  [/^auth\//, 'auth'],
  [/^(businesses|pincodes)/, 'business'],
  [/^(categories|states|cities|products|landed-cost|banners|storefronts)/, 'catalog'],
  [/^cart/, 'cart'],
  [/^(checkout|payments|orders)/, 'orders'],
  [/^seller\/payouts/, 'payouts'],
  [/^seller/, 'seller'],
  [/^notifications/, 'notifications'],
  [/^(claims|seller-orders|favourites|reviews|account)/, 'account'],
  [/^admin/, 'admin'],
]
export function areaOfPath(path: string): ApiArea {
  const p = path.replace(/^\//, '')
  return AREA_PREFIXES.find(([re]) => re.test(p))?.[1] ?? 'catalog'
}
function readAreaList(envValue: unknown, storageKey: string): Set<string> {
  let raw = typeof envValue === 'string' ? envValue : ''
  try {
    raw = localStorage.getItem(storageKey) ?? raw
  } catch {
    /* storage blocked */
  }
  return new Set(raw.split(',').map((x) => x.trim()).filter(Boolean))
}
/** Areas forced to the real API even in demo mode (VITE_REAL_AREAS or localStorage tc_real_areas), and areas forced to sample data (VITE_MOCK_AREAS / tc_mock_areas). */
const REAL_AREAS = readAreaList(import.meta.env.VITE_REAL_AREAS, 'tc_real_areas')
const MOCK_AREAS = readAreaList(import.meta.env.VITE_MOCK_AREAS, 'tc_mock_areas')
/** True when this path goes to the real backend. Adapters use it to reshape real answers into what the screens expect. */
export const isReal = (path: string) => !isMockFor(path)
export function isMockFor(path: string): boolean {
  const area = areaOfPath(path)
  if (REAL_AREAS.has(area)) return false
  if (MOCK_AREAS.has(area)) return true
  return USE_MOCKS
}

/** Switch between sample data and the real backend. Reloads so every cache starts clean. */
export function setUseMocks(on: boolean) {
  try {
    localStorage.setItem(MOCK_KEY, on ? 'true' : 'false')
    localStorage.removeItem('tc_query_cache') // saved copies belong to the other mode
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
  /** Sent as `Idempotency-Key`. Set automatically for checkout and payments. */
  idempotencyKey?: string
}

/** Fired on any 401 so the auth layer can clear the session. */
export const UNAUTHORIZED_EVENT = 'tc:unauthorized'

const TIMEOUT_MS = 20_000
const UPLOAD_TIMEOUT_MS = 60_000
/** Calls that move money. A double tap or a retry must never do it twice, so the server gets a key. */
const IDEMPOTENT = new Set(['checkout', 'payments'])

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

/** Reads the backend error body (ProblemDetails or `{ code, message }`). The text is kept for logs; users see the mapped message. */
async function toError(res: Response): Promise<ApiError> {
  let serverMessage = `Request failed (${res.status})`
  let fieldErrors: Record<string, string[]> | undefined
  let code: string | undefined
  try {
    const data = (await res.json()) as { code?: string; errorCode?: string; detail?: string; title?: string; message?: string; errors?: Record<string, string[]>; extensions?: { code?: string } }
    serverMessage = data.detail || data.message || data.title || serverMessage
    fieldErrors = data.errors
    code = data.code ?? data.errorCode ?? data.extensions?.code
  } catch {
    /* body is not JSON */
  }
  return new ApiError(res.status, serverMessage, fieldErrors, code)
}

const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms))

/** fetch with a timeout. Network failure and timeout become ApiError (status 0) so screens show the offline or timeout message. */
async function fetchWithTimeout(url: string, init: RequestInit, ms: number): Promise<Response> {
  const ctl = new AbortController()
  const timer = setTimeout(() => ctl.abort(), ms)
  try {
    return await fetch(url, { ...init, signal: ctl.signal })
  } catch (e) {
    if (e instanceof DOMException && e.name === 'AbortError') throw new ApiError(0, 'Request timed out', undefined, 'TIMEOUT')
    throw new ApiError(0, 'Network error', undefined, 'NETWORK')
  } finally {
    clearTimeout(timer)
  }
}

async function send(method: Method, path: string, opts: RequestOptions, retryCsrf: boolean): Promise<Response> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  let body: BodyInit | undefined
  if (opts.form) body = opts.form
  else if (opts.body !== undefined) {
    headers['Content-Type'] = 'application/json'
    body = JSON.stringify(opts.body)
  }
  if (opts.idempotencyKey) headers['Idempotency-Key'] = opts.idempotencyKey
  if (method !== 'GET') headers['X-CSRF-TOKEN'] = await loadCsrf()

  // Only reads are retried (one more try on a network blip or 502/503/504). Writes are never replayed on their own.
  const attempts = method === 'GET' ? 2 : 1
  let res: Response | undefined
  for (let i = 0; i < attempts; i++) {
    try {
      res = await fetchWithTimeout(buildUrl(path, opts.query), { method, headers, body, credentials: 'include' }, opts.form ? UPLOAD_TIMEOUT_MS : TIMEOUT_MS)
      if (![502, 503, 504].includes(res.status)) break
    } catch (e) {
      if (i === attempts - 1) throw e
    }
    await sleep(400)
  }
  if (!res) throw new ApiError(0, 'Network error', undefined, 'NETWORK')

  // Token may be stale (e.g. after login the cookie changes): fetch a fresh one once.
  if (method !== 'GET' && retryCsrf && res.status === 400) {
    const text = await res.clone().text()
    if (/antiforgery|csrf/i.test(text)) {
      await loadCsrf(true)
      return send(method, path, opts, false)
    }
  }
  return res
}

/** One silent session refresh before giving up on a 401. Skipped if the backend has no refresh route. */
let refreshUnsupported = false
async function tryRefresh(): Promise<boolean> {
  if (refreshUnsupported) return false
  try {
    const res = await fetchWithTimeout(`${API_BASE}/auth/refresh`, { method: 'POST', credentials: 'include', headers: { 'X-CSRF-TOKEN': await loadCsrf() } }, TIMEOUT_MS)
    if (res.status === 404 || res.status === 405) refreshUnsupported = true // TODO(BE): see BACKEND_REQUESTS.md
    return res.ok
  } catch {
    return false
  }
}

export async function api<T>(method: Method, path: string, opts: RequestOptions = {}): Promise<T> {
  const key = method === 'POST' && IDEMPOTENT.has(path.replace(/^\//, '')) ? (opts.idempotencyKey ?? crypto.randomUUID()) : opts.idempotencyKey
  const o = { ...opts, idempotencyKey: key }
  if (isMockFor(path)) return mockRequest<T>(method, path, o)

  let res = await send(method, path, o, true)
  if (res.status === 401 && !path.startsWith('auth/') && (await tryRefresh())) res = await send(method, path, o, true)
  if (!res.ok) {
    if (res.status === 401 && path !== 'auth/me') window.dispatchEvent(new Event(UNAUTHORIZED_EVENT))
    throw await toError(res)
  }
  // Login/logout change the session, so the old CSRF token is no longer valid.
  if (path.startsWith('auth/')) csrfToken = null
  if (res.status === 204) return undefined as T
  const text = await res.text()
  // CSV and other text answers (payout export) are returned as they are.
  if (text && !(res.headers.get('content-type') ?? 'application/json').includes('json')) return text as T
  return (text ? JSON.parse(text) : undefined) as T
}

export const get = <T>(path: string, query?: object) => api<T>('GET', path, { query })
export const post = <T>(path: string, body?: unknown) => api<T>('POST', path, { body })
export const put = <T>(path: string, body?: unknown) => api<T>('PUT', path, { body })
export const del = <T>(path: string) => api<T>('DELETE', path)
export const upload = <T>(path: string, form: FormData) => api<T>('POST', path, { form })
