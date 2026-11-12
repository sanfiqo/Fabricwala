import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { businessApi, masterApi } from '@/api/endpoints'

export function useCategories() {
  return useQuery({ queryKey: ['categories'], queryFn: masterApi.categories, staleTime: 10 * 60_000 })
}

export function useMyBusiness() {
  return useQuery({ queryKey: ['business-me'], queryFn: businessApi.me })
}

/** City used for landed-cost: the retailer's default delivery address. */
export function useDeliveryCity() {
  const biz = useMyBusiness()
  const addrs = biz.data?.addresses.filter((a) => a.addressType === 'Delivery') ?? []
  const addr = addrs.find((a) => a.isDefault) ?? addrs[0]
  return { cityId: addr?.cityId, cityName: addr?.cityName, loading: biz.isLoading }
}

export function useDebounced<T>(value: T, ms = 400): T {
  const [v, setV] = useState(value)
  useEffect(() => {
    const t = setTimeout(() => setV(value), ms)
    return () => clearTimeout(t)
  }, [value, ms])
  return v
}
