import type { ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Truck } from 'lucide-react'
import { productApi } from '@/api/endpoints'
import type { ProductDetail } from '@/api/types'
import { Badge, ErrorState, Skeleton } from '@/components/ui/misc'
import { useDebounced, useDeliveryCity } from '@/hooks'
import { fmt, S } from '@/i18n'
import { cn, money } from '@/lib/utils'

function Row({ label, value, strong }: { label: string; value: ReactNode; strong?: boolean }) {
  return (
    <div className={cn('flex items-baseline justify-between gap-4 border-t border-line py-3 first:border-t-0 first:pt-0', strong && 'font-semibold')}>
      <span className={strong ? '' : 'text-mute'}>{label}</span>
      <span className="text-right tabular-nums">{value}</span>
    </div>
  )
}

/** What one piece really costs when it reaches the shopkeeper's city. Server calculated. */
export function LandedBreakdown({ p, qty }: { p: ProductDetail; qty: number }) {
  const city = useDeliveryCity()
  const debounced = useDebounced(qty)
  const enabled = debounced >= p.moq && !!city.cityId
  const q = useQuery({
    queryKey: ['landed', p.id, debounced, city.cityId],
    queryFn: () => productApi.landedCost(p.id, debounced, city.cityId!),
    enabled,
  })

  if (!city.loading && !city.cityId) return null
  const c = q.data
  const name = city.cityName ?? ''

  if (!enabled) {
    return (
      <p className="rounded-2xl bg-soft p-4 text-sm text-mute">
        {qty > 0 && qty < p.moq ? fmt(S.product.moqError, { moq: `${p.moq} ${S.units[p.unit]}` }) : `${fmt(S.product.landed, { city: name })}: ${S.product.qty} ≥ ${p.moq}`}
      </p>
    )
  }
  if (q.isLoading) {
    return (
      <div role="status" aria-label={S.app.loading} className="space-y-3">
        <Skeleton className="h-5" />
        <Skeleton className="h-5" />
        <Skeleton className="h-5" />
        <Skeleton className="h-6" />
      </div>
    )
  }
  if (q.error || !c) return <ErrorState error={q.error} onRetry={() => void q.refetch()} />

  return (
    <div>
      <Row label={S.product.factoryPrice} value={money(c.slabPrice)} />
      <Row label={fmt(S.product.deliveryTo, { city: name })} value={c.shippingPerUnit === null ? <Badge tone="warn">{S.product.shippingOnRequest}</Badge> : money(c.shippingPerUnit)} />
      <Row label={fmt(S.product.gstPct, { n: p.gstRate })} value={money(c.gstPerUnit)} />
      {c.platformFeePerUnit > 0 && <Row label={S.product.platformFee} value={money(c.platformFeePerUnit)} />}
      <Row strong label={S.product.youPay} value={c.landedPerUnit === null ? <span className="text-saving">{S.product.shippingOnRequest}</span> : money(c.landedPerUnit)} />
      {c.total !== null && <Row label={fmt(S.product.total, { qty: c.qty })} value={money(c.total)} />}
      {c.transitDays !== null && (
        <p className="mt-1 flex items-center gap-1.5 text-sm text-mute">
          <Truck className="size-4" aria-hidden />
          {fmt(S.product.transit, { d: c.transitDays })}
        </p>
      )}
    </div>
  )
}
