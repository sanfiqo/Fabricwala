import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, ShoppingBag, Trash2 } from 'lucide-react'
import { cartApi } from '@/api/endpoints'
import type { Cart, CartItem } from '@/api/types'
import { QtyStepper } from '@/components/QtyStepper'
import { Badge, Card, EmptyState, ErrorState, KeyValue, LinkButton, PageHeader, PageLoading } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { useDebounced } from '@/hooks'
import { fmt, S } from '@/i18n'
import { money } from '@/lib/utils'

/** Products whose total quantity (all sizes/colours, all rows) is under the minimum order. */
export function moqProblems(cart: Cart) {
  const total = new Map<number, { qty: number; item: CartItem }>()
  for (const g of cart.groups)
    for (const i of g.items) total.set(i.productId, { qty: (total.get(i.productId)?.qty ?? 0) + i.quantity, item: i })
  return [...total.values()].filter((t) => t.qty < t.item.moq).map((t) => t.item)
}

export function CartTotals({ cart }: { cart: Cart }) {
  return (
    <Card className="px-4 py-3">
      <KeyValue label={S.cart.subTotal} value={money(cart.subTotal)} />
      <KeyValue label={S.cart.shipping} value={cart.shippingTotal === null ? <Badge tone="warn">{S.cart.onRequest}</Badge> : money(cart.shippingTotal)} />
      <KeyValue label={S.cart.tax} value={money(cart.taxTotal)} />
      <hr className="stitch my-2" />
      <KeyValue strong label={S.cart.total} value={money(cart.grandTotal)} />
      <p className="mt-2 text-xs text-mute">{S.cart.estimate}</p>
    </Card>
  )
}

function CartRow({ item, onChange, onRemove }: { item: CartItem; onChange: (q: number) => void; onRemove: () => void }) {
  const [qty, setQty] = useState(item.quantity)
  const debounced = useDebounced(qty, 500)
  const sent = useRef(item.quantity)
  useEffect(() => {
    if (debounced >= 1 && debounced !== sent.current) {
      sent.current = debounced
      onChange(debounced)
    }
  })

  return (
    <div className="flex gap-3 py-3">
      <Link to={`/shop/p/${item.slug}`} className="size-20 shrink-0 overflow-hidden rounded-xl bg-navy-50">
        {item.imageUrl && <img src={item.imageUrl} alt="" className="size-full object-cover" />}
      </Link>
      <div className="min-w-0 flex-1">
        <p className="line-clamp-2 font-bold leading-5 text-navy-900">{item.title}</p>
        <p className="text-sm text-mute">{item.colour} · {item.size} · {money(item.unitPrice)}</p>
        <div className="mt-2 flex items-center justify-between gap-2">
          <QtyStepper label={item.title} min={1} value={qty} onChange={setQty} className="scale-90 origin-left" />
          <button type="button" onClick={onRemove} aria-label={S.cart.remove} className="flex size-11 items-center justify-center rounded-full text-bad hover:bg-bad-bg">
            <Trash2 className="size-5" />
          </button>
        </div>
      </div>
      <p className="w-20 shrink-0 text-right font-extrabold text-navy-900">{money(item.unitPrice * qty)}</p>
    </div>
  )
}

export default function CartPage() {
  const qc = useQueryClient()
  const toast = useToast()
  const cart = useQuery({ queryKey: ['cart'], queryFn: cartApi.get })
  const apply = (c: Cart) => qc.setQueryData(['cart'], c)
  const update = useMutation({ mutationFn: (v: { id: number; q: number }) => cartApi.update(v.id, v.q), onSuccess: apply, onError: (e) => toast.error(e.message) })
  const remove = useMutation({ mutationFn: (id: number) => cartApi.remove(id), onSuccess: apply, onError: (e) => toast.error(e.message) })

  if (cart.isLoading) return <PageLoading />
  if (cart.error) return <ErrorState error={cart.error} onRetry={() => void cart.refetch()} />
  const c = cart.data!
  if (c.groups.length === 0) {
    return (
      <>
        <PageHeader title={S.cart.title} />
        <EmptyState icon={<ShoppingBag className="size-8" />} title={S.cart.empty} sub={S.cart.emptySub} action={<LinkButton to="/shop">{S.nav.home}</LinkButton>} />
      </>
    )
  }
  const problems = moqProblems(c)

  return (
    <div>
      <PageHeader title={S.cart.title} />
      <div className="space-y-4 px-4 pt-4">
        {problems.map((i) => (
          <div key={i.productId} role="alert" className="flex gap-2 rounded-xl bg-warn-bg p-3 text-sm font-semibold text-warn">
            <AlertTriangle className="size-5 shrink-0" />
            <span>{i.title}: {fmt(S.cart.moqWarn, { moq: `${i.moq} ${S.units[i.unit]}` })}</span>
          </div>
        ))}

        {c.groups.map((g) => (
          <Card key={g.manufacturerId} className="overflow-hidden">
            <div className="bg-navy-900 px-4 py-2.5 text-sm font-bold text-white">{fmt(S.cart.from, { name: g.manufacturerName })}</div>
            <div className="divide-y divide-line px-4">
              {g.items.map((i) => (
                <CartRow key={`${i.id}-${i.quantity}`} item={i} onChange={(q) => update.mutate({ id: i.id, q })} onRemove={() => remove.mutate(i.id)} />
              ))}
            </div>
            <div className="border-t border-dashed border-leaf-600/40 bg-leaf-50 px-4 py-2 text-sm">
              <KeyValue label={S.cart.subTotal} value={money(g.subTotal)} />
            </div>
          </Card>
        ))}
        <p className="text-xs text-mute">{S.cart.slabNote}</p>
        <CartTotals cart={c} />
        {problems.length > 0 ? (
          <span className="flex min-h-12 w-full items-center justify-center rounded-xl bg-navy-900/40 font-semibold text-white">{S.cart.checkout}</span>
        ) : (
          <LinkButton to="/shop/checkout" className="w-full">{S.cart.checkout}</LinkButton>
        )}
      </div>
    </div>
  )
}
