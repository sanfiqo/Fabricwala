import { useEffect } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { clearSavedData } from '@/queryClient'
import { authApi } from '@/api/endpoints'
import { ApiError, UNAUTHORIZED_EVENT } from '@/api/client'
import type { Me } from '@/api/types'
import { PageLoading, ErrorState } from '@/components/ui/misc'

export type Area = 'Retailer' | 'Manufacturer' | 'Admin'

export function useMe() {
  return useQuery<Me | null>({
    queryKey: ['me'],
    queryFn: async () => {
      try {
        return await authApi.me()
      } catch (e) {
        if (e instanceof ApiError && e.status === 401) return null
        throw e
      }
    },
    retry: false,
    staleTime: 60_000,
  })
}

/** The area a user belongs to. Admin by role; others by their business type. */
export function areaOf(me: Me): Area | null {
  if (me.roles.includes('Admin')) return 'Admin'
  return me.business?.type ?? null
}

/** Where a signed-in user should land. */
export function homeFor(me: Me | null | undefined): string {
  if (!me) return '/login'
  const area = areaOf(me)
  if (area === 'Admin') return '/admin/businesses'
  if (!me.business) return '/register'
  if (me.business.verificationStatus !== 'Approved') return '/pending'
  return area === 'Manufacturer' ? '/seller/products' : '/shop'
}

/** Clears the cached session when any API call answers 401. */
export function useUnauthorizedListener() {
  const qc = useQueryClient()
  useEffect(() => {
    const h = () => {
      qc.setQueryData(['me'], null)
      qc.removeQueries({ predicate: (q) => q.queryKey[0] !== 'me' })
      clearSavedData()
    }
    window.addEventListener(UNAUTHORIZED_EVENT, h)
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, h)
  }, [qc])
}

/** Route guard. `area` = require an approved user of that area; omitted = just be logged in. */
export function Gate({ area }: { area?: Area }) {
  const { data: me, isLoading, error, refetch } = useMe()
  const loc = useLocation()
  if (isLoading) return <PageLoading />
  if (error) return <ErrorState error={error} onRetry={() => void refetch()} />
  if (!me) return <Navigate to="/login" replace state={{ from: loc.pathname }} />
  if (area) {
    const home = homeFor(me)
    const inArea = areaOf(me) === area && (area === 'Admin' || me.business?.verificationStatus === 'Approved')
    if (!inArea) return <Navigate to={home} replace />
  }
  return <Outlet />
}
