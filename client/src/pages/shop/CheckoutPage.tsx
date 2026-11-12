import { useState } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, MapPin } from 'lucide-react'
import { cartApi, orderApi } from '@/api/endpoints'
import { USE_MOCKS } from '@/api/client'
import type { Order } from '@/api/types'
import { Button } from '@/components/ui/button'
import { Card, ErrorState, LinkButton, PageHeader, PageLoading, SectionTitle } from '@/components/ui/misc'
import { fmt, S } from '@/i18n'
import { cn, money } from '@/lib/utils'
import { CartTotals, moqProblems } from './CartPage'

export default function CheckoutPage() {
  const qc = useQueryClient()
  const cart = useQuery({ queryKey: ['cart'], queryFn: cartApi.get })
  const [addressId, setAddressId] = useState<number | null>(null)
  const [placed, setPlaced] = useState<{ order: Order; payNote: string } | null>(null)

  const place = useMutation({
    mutationFn: async (deliveryAddressId: number) => {
      const order = await orderApi.checkout(deliveryAddressId)
      // The order exists now. A payment problem must not hide that fact from the user.
      let payNote: string = S.checkout.paySoon
      try {
        await orderApi.pay(order.id)
        if (USE_MOCKS) payNote = S.checkout.payDemo
      } catch {
        /* keep the "payment is being set up" message */
      }
      return { order, payNote }
    },
    onSuccess: async (r) => {
      setPlaced(r)
      qc.setQueryData(['cart'], { ...cart.data, groups: [], subTotal: 0, shippingTotal: 0, taxTotal: 0, grandTotal: 0 })
      await Promise.all([qc.invalidateQueries({ queryKey: ['cart'] }), qc.invalidateQueries({ queryKey: ['orders'] })])
    },
  })

  if (placed) {
    return (
      <div className="px-4 py-12 text-center">
        <div className="mx-auto flex size-24 items-center justify-center rounded-full bg-leaf-50 text-leaf-600"><CheckCircle2 className="size-14" /></div>
        <h1 className="mt-5 text-2xl font-extrabold text-navy-900">{S.checkout.placed}</h1>
        <p className="mt-1 text-mute">{fmt(S.checkout.placedSub, { n: placed.order.orderNumber })}</p>
        <p className="mx-auto mt-4 max-w-sm rounded-xl bg-navy-50 p-3 text-sm font-semibold text-navy-800">{placed.payNote}</p>
        <LinkButton to={`/shop/orders/${placed.order.id}`} className="mt-6">{S.checkout.viewOrders}</LinkButton>
      </div>
    )
  }

  if (cart.isLoading) return <PageLoading />
  if (cart.error) return <ErrorState error={cart.error} onRetry={() => void cart.refetch()} />
  const c = cart.data!
  if (c.groups.length === 0 || moqProblems(c).length > 0) return <Navigate to="/shop/cart" replace />

  const selected = addressId ?? (c.addresses.find((a) => a.isDefault) ?? c.addresses[0])?.id ?? null

  return (
    <div>
      <PageHeader title={S.checkout.title} back="/shop/cart" />
      <div className="px-4 pb-6">
        <SectionTitle>{S.checkout.deliverTo}</SectionTitle>
        {c.addresses.length === 0 ? (
          <p className="rounded-xl bg-warn-bg p-3 font-semibold text-warn">{S.checkout.noAddress}</p>
        ) : (
          <div className="space-y-2" role="radiogroup" aria-label={S.checkout.deliverTo}>
            {c.addresses.map((a) => (
              <button
                key={a.id}
                type="button"
                role="radio"
                aria-checked={selected === a.id}
                onClick={() => setAddressId(a.id)}
                className={cn('flex w-full items-start gap-3 rounded-2xl border-2 p-4 text-left', selected === a.id ? 'border-leaf-600 bg-leaf-50' : 'border-line')}
              >
                <MapPin className="mt-0.5 size-5 shrink-0 text-navy-900" />
                <span>
                  <span className="block font-bold text-navy-900">{a.line1}{a.line2 ? `, ${a.line2}` : ''}</span>
                  <span className="block text-mute">{a.cityName}, {a.stateName} {a.pincode}</span>
                </span>
              </button>
            ))}
          </div>
        )}

        <SectionTitle>{S.checkout.summary}</SectionTitle>
        <div className="space-y-3">
          {c.groups.map((g) => (
            <Card key={g.manufacturerId} className="overflow-hidden">
              <div className="bg-navy-50 px-4 py-2 text-sm font-bold text-navy-900">{fmt(S.cart.from, { name: g.manufacturerName })}</div>
              <ul className="divide-y divide-line px-4">
                {g.items.map((i) => (
                  <li key={i.id} className="flex justify-between gap-3 py-2.5 text-sm">
                    <span className="min-w-0"><span className="block truncate font-semibold">{i.title}</span><span className="text-mute">{i.colour} · {i.size} · {i.quantity} × {money(i.unitPrice)}</span></span>
                    <span className="font-bold">{money(i.lineTotal)}</span>
                  </li>
                ))}
              </ul>
            </Card>
          ))}
          <CartTotals cart={c} />
        </div>

        {place.error && <p role="alert" className="mt-4 rounded-xl bg-bad-bg p-3 text-sm font-semibold text-bad">{place.error.message}</p>}
        <Button className="mt-5" block variant="accent" disabled={selected === null} loading={place.isPending} onClick={() => selected !== null && place.mutate(selected)}>
          {S.checkout.place} · {money(c.grandTotal)}
        </Button>
        <p className="mt-3 text-center text-xs text-mute">{S.cart.estimate}</p>
        <Link to="/shop/cart" className="mt-2 flex min-h-12 items-center justify-center font-semibold text-navy-800">{S.app.back}</Link>
      </div>
    </div>
  )
}
