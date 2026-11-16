import { useCallback, useEffect, useState, useSyncExternalStore } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { businessApi, masterApi, notificationApi, orderApi } from '@/api/endpoints'
import type { BusinessAddress, Order } from '@/api/types'

export function useCategories() {
  return useQuery({ queryKey: ['categories'], queryFn: masterApi.categories, staleTime: 10 * 60_000 })
}

export function useMyBusiness() {
  return useQuery({ queryKey: ['business-me'], queryFn: businessApi.me })
}

const ADDR_KEY = 'tc_delivery_addr'
const readChosen = (): number | null => {
  try {
    const v = localStorage.getItem(ADDR_KEY)
    return v ? Number(v) : null
  } catch {
    return null
  }
}

/** The delivery address prices are calculated for: the one the shopkeeper picked, else the default one. Shared by all screens. */
export function useDeliveryAddress() {
  const biz = useMyBusiness()
  const qc = useQueryClient()
  const chosen = useQuery({ queryKey: ['delivery-addr'], queryFn: readChosen, staleTime: Infinity, gcTime: Infinity })
  const addrs: BusinessAddress[] = biz.data?.addresses.filter((a) => a.addressType === 'Delivery') ?? []
  const addr = addrs.find((a) => a.id === chosen.data) ?? addrs.find((a) => a.isDefault) ?? addrs[0]
  const choose = useCallback(
    (id: number) => {
      try {
        localStorage.setItem(ADDR_KEY, String(id))
      } catch {
        /* storage blocked: choice lasts until reload */
      }
      qc.setQueryData(['delivery-addr'], id)
    },
    [qc],
  )
  return { addr, addrs, choose, loading: biz.isLoading }
}

/** City used for landed-cost. */
export function useDeliveryCity() {
  const { addr, loading } = useDeliveryAddress()
  return { cityId: addr?.cityId, cityName: addr?.cityName, loading }
}

/** The shopkeeper's most recent order, for "Order again". */
export function useLastOrder() {
  const q = useQuery({ queryKey: ['orders'], queryFn: orderApi.list })
  const last: Order | undefined = [...(q.data ?? [])].sort((a, b) => b.placedAt.localeCompare(a.placedAt))[0]
  return { last, loading: q.isLoading }
}

/** Browser online/offline state. */
export function useOnline() {
  return useSyncExternalStore(
    (cb) => {
      window.addEventListener('online', cb)
      window.addEventListener('offline', cb)
      return () => {
        window.removeEventListener('online', cb)
        window.removeEventListener('offline', cb)
      }
    },
    () => navigator.onLine,
    () => true,
  )
}

export function useDebounced<T>(value: T, ms = 400): T {
  const [v, setV] = useState(value)
  useEffect(() => {
    const t = setTimeout(() => setV(value), ms)
    return () => clearTimeout(t)
  }, [value, ms])
  return v
}

/** Number of unread notifications, for the bell. Refreshes every minute while the app is open. */
export function useUnreadCount() {
  const q = useQuery({ queryKey: ['notifications', 'unread'], queryFn: notificationApi.unread, refetchInterval: 60_000, staleTime: 0 })
  return q.data?.count ?? 0
}
