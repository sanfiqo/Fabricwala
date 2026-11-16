import { useRef, useState } from 'react'
import { Navigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Clock, MapPin, XCircle } from 'lucide-react'
import { cartApi, orderApi } from '@/api/endpoints'
import { USE_MOCKS } from '@/api/client'
import { qk } from '@/api/keys'
import type { Order } from '@/api/types'
import { StickyActionBar } from '@/components/StickyActionBar'
import { Button } from '@/components/ui/button'
import { BackButton, ErrorState, LinkButton, Skeleton } from '@/components/ui/misc'
import { fmt, S } from '@/i18n'
import { haptic } from '@/motion'
import { cn, money } from '@/lib/utils'
import { CartTotals, moqProblems } from './CartPage'

type PayState = 'success' | 'pending' | 'failed'

function Result({ order, state, retrying, onRetry }: { order: Order; state: PayState; retrying: boolean; onRetry: () => void }) {
  const Icon = state === 'success' ? Check : state === 'pending' ? Clock : XCircle
  const tone = state === 'success' ? 'bg-success text-white' : state === 'pending' ? 'bg-saving-bg text-saving' : 'bg-error-bg text-error'
  return (
    <div role="status" className="flex min-h-[80dvh] flex-col items-center justify-center px-6 text-center">
      <span className={cn('mb-5 flex size-20 items-center justify-center rounded-full', tone)} aria-hidden>
        <Icon className="size-10" />
      </span>
      <h1 className="text-balance text-[28px] font-semibold leading-tight">{S.pay[`${state}Title` as const]}</h1>
      <p className="mt-1 text-sm text-mute">{fmt(S.pay.orderNo, { n: order.orderNumber })} · {money(order.grandTotal)}</p>
      <p className="mt-3 max-w-sm text-mute">{S.pay[`${state}Sub` as const]}</p>
      <div className="mt-7 flex w-full max-w-xs flex-col gap-2">
        {state === 'failed' && (
          <Button size="lg" block loading={retrying} onClick={onRetry}>
            {S.pay.retry}
          </Button>
        )}
        <LinkButton to={`/shop/orders/${order.id}`} className={cn('min-h-14 w-full', state === 'failed' && '!bg-soft !text-ink')}>
          {S.pay.seeOrder}
        </LinkButton>
      </div>
    </div>
  )
}

export default function CheckoutPage() {
  const qc = useQueryClient()
  const cart = useQuery({ queryKey: qk.cart, queryFn: cartApi.get })
  const [addressId, setAddressId] = useState<number | null>(null)
  const [placed, setPlaced] = useState<{ order: Order; state: PayState } | null>(null)
  // One key per order attempt and one per payment attempt. A double tap or a retry sends the same key, so the server does it once.
  const orderKey = useRef(crypto.randomUUID())
  const payKey = useRef(crypto.randomUUID())

  /** Paying never retries by itself (a retry could charge twice). The person decides. */
  const pay = async (order: Order): Promise<PayState> => {
    try {
      const intent = await orderApi.pay(order.id, payKey.current)
      // Razorpay checkout.js is not wired yet (P5.9). Without a gateway key the payment stays pending.
      if (USE_MOCKS) return 'success'
      // Development has a fake gateway with no screen: complete it here. In production this route does not exist and the payment stays pending until the gateway confirms.
      if (import.meta.env.DEV && intent.gatewayOrderId?.startsWith('dev_')) {
        const done = await orderApi.devComplete(intent.gatewayOrderId).catch(() => null)
        if (done?.status === 'Paid') return 'success'
      }
      return 'pending'
    } catch {
      payKey.current = crypto.randomUUID()
      return 'failed'
    }
  }

  const place = useMutation({
    mutationFn: async (deliveryAddressId: number) => {
      const order = await orderApi.checkout(deliveryAddressId, orderKey.current)
      // The order exists now. A payment problem must never hide that from the person.
      return { order, state: await pay(order) }
    },
    onSuccess: async (r) => {
      haptic(20)
      setPlaced(r)
      qc.setQueryData(qk.cart, { ...cart.data, groups: [], subTotal: 0, shippingTotal: 0, taxTotal: 0, grandTotal: 0 })
      await Promise.all([qc.invalidateQueries({ queryKey: qk.cart }), qc.invalidateQueries({ queryKey: qk.orders })])
    },
    onError: () => {
      // Same order attempt may be retried with the same key. Keep it.
    },
  })
  const retryPay = useMutation({
    mutationFn: async (order: Order) => pay(order),
    onSuccess: (state) => setPlaced((p) => (p ? { ...p, state } : p)),
  })

  if (placed) return <Result order={placed.order} state={placed.state} retrying={retryPay.isPending} onRetry={() => retryPay.mutate(placed.order)} />

  const Top = (
    <div className="flex items-center gap-1.5 px-4 pt-[18px]">
      <BackButton to="/shop/cart" />
      <span className="text-[15px] font-medium">{S.checkout.title}</span>
    </div>
  )

  // A saved (offline) copy of an empty cart must not send you back while the real cart is still loading.
  if (cart.isLoading || (cart.isFetching && (cart.data?.groups.length ?? 0) === 0)) {
    return (
      <div role="status" aria-label={S.app.loading}>
        {Top}
        <div className="space-y-3 px-5 pt-4">
          <Skeleton className="h-8 w-1/2" />
          <Skeleton className="h-20 !rounded-card" />
          <Skeleton className="h-40 !rounded-card" />
        </div>
      </div>
    )
  }
  if (cart.error && !cart.data) return <div>{Top}<ErrorState error={cart.error} onRetry={() => void cart.refetch()} /></div>
  const c = cart.data!
  if (c.groups.length === 0 || moqProblems(c).length > 0) return <Navigate to="/shop/cart" replace />

  const selected = addressId ?? (c.addresses.find((a) => a.isDefault) ?? c.addresses[0])?.id ?? null

  return (
    <div className="pb-48">
      {Top}
      <h1 className="mx-5 mt-1.5 text-[30px] font-semibold leading-[1.15] tracking-[-0.02em]">{S.checkout.summary}</h1>

      <section aria-labelledby="deliver-h" className="px-5 pt-5">
        <h2 id="deliver-h" className="mb-2 text-[13px] text-mute">{S.pay.addressTitle}</h2>
        {c.addresses.length === 0 ? (
          <p role="alert" className="rounded-2xl bg-saving-bg p-3.5 text-sm font-medium text-saving">{S.checkout.noAddress}</p>
        ) : (
          <div className="space-y-2" role="radiogroup" aria-labelledby="deliver-h">
            {c.addresses.map((a) => (
              <button
                key={a.id}
                type="button"
                role="radio"
                aria-checked={selected === a.id}
                onClick={() => setAddressId(a.id)}
                className={cn('press flex min-h-14 w-full items-start gap-3 rounded-[18px] border bg-surface p-3.5 text-left', selected === a.id ? 'border-2 border-ink' : 'border-line')}
              >
                <MapPin className="mt-0.5 size-5 shrink-0" aria-hidden />
                <span className="min-w-0 flex-1">
                  <span className="block break-words text-[15px] font-medium">{a.line1}{a.line2 ? `, ${a.line2}` : ''}</span>
                  <span className="block text-[13px] text-mute">{a.cityName}, {a.stateName} {a.pincode}</span>
                </span>
                {selected === a.id && <Check className="mt-0.5 size-5 shrink-0" aria-hidden />}
              </button>
            ))}
          </div>
        )}
      </section>

      <section aria-label={S.checkout.summary} className="space-y-3 px-5 pt-6">
        {c.groups.map((g) => (
          <div key={g.manufacturerId} className="overflow-hidden rounded-card bg-soft">
            <div className="flex items-baseline justify-between gap-3 px-4 pb-1 pt-3 text-[13px] text-mute">
              <b className="truncate font-semibold text-ink">{g.manufacturerName}</b>
              <span className="shrink-0 tabular-nums">{money(g.subTotal)}</span>
            </div>
            <ul className="divide-y divide-line px-4">
              {g.items.map((i) => (
                <li key={i.id} className="flex justify-between gap-3 py-3 text-[15px]">
                  <span className="min-w-0 break-words">
                    {i.title}
                    <span className="block text-[13px] text-mute">{i.colour} · {i.size} · {i.quantity} × {money(i.unitPrice)}</span>
                  </span>
                  <span className="shrink-0 font-medium tabular-nums">{money(i.lineTotal)}</span>
                </li>
              ))}
            </ul>
          </div>
        ))}
        <CartTotals cart={c} />
      </section>

      <StickyActionBar>
        {place.error && <p role="alert" className="rounded-2xl bg-error-bg p-3 text-sm font-medium text-error">{place.error.message}</p>}
        <p className="text-center text-xs text-mute">{S.pay.escrow}</p>
        <Button size="lg" block disabled={selected === null} loading={place.isPending} onClick={() => selected !== null && place.mutate(selected)}>
          {place.isPending ? S.pay.placing : fmt(S.pay.payAndPlace, { total: money(c.grandTotal) })}
        </Button>
      </StickyActionBar>
    </div>
  )
}
