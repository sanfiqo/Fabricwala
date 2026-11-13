import { QueryClient } from '@tanstack/react-query'
import { createSyncStoragePersister } from '@tanstack/query-sync-storage-persister'
import { ApiError } from '@/api/client'
import { STALE } from '@/api/keys'

const DAY = 24 * 60 * 60 * 1000

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      gcTime: DAY, // must be at least the persist maxAge
      refetchOnWindowFocus: false,
      // Offline: show the saved copy instead of waiting forever. Writes are never queued (payments must not be replayed).
      networkMode: 'offlineFirst',
      // Do not hammer the server on client errors (401, 403, 404, 422).
      retry: (count, err) => !(err instanceof ApiError && err.status < 500) && count < 2,
    },
  },
})

// Stale times per area (see api/keys.ts). Anything not listed uses the 30 s default above.
queryClient.setQueryDefaults(['categories'], { staleTime: STALE.master })
queryClient.setQueryDefaults(['products'], { staleTime: STALE.catalog })
queryClient.setQueryDefaults(['product'], { staleTime: STALE.catalog })
for (const k of ['cart', 'orders', 'seller-orders', 'payouts', 'notifications']) queryClient.setQueryDefaults([k], { staleTime: STALE.live })

const KEY = 'tc_query_cache'
export const persister = createSyncStoragePersister({ storage: typeof window === 'undefined' ? undefined : window.localStorage, key: KEY, throttleTime: 1000 })

/** Only screens that must open without internet are saved on the phone (UX: Home, categories, cart, orders). */
const SAVED = new Set(['me', 'categories', 'products', 'product', 'cart', 'orders', 'business-me'])
export const persistOptions = {
  persister,
  maxAge: DAY,
  buster: 'v1',
  dehydrateOptions: { shouldDehydrateQuery: (q: { state: { status: string }; queryKey: readonly unknown[] }) => q.state.status === 'success' && SAVED.has(String(q.queryKey[0])) },
}

/** Call on log out so the next person on this phone never sees saved orders. */
export function clearSavedData() {
  try {
    localStorage.removeItem(KEY)
  } catch {
    /* storage blocked */
  }
}
