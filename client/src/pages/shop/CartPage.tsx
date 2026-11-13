import { useEffect, useRef, useState, type CSSProperties } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, MapPin, MessageCircle, ShoppingBag, X } from 'lucide-react'
import { cartApi } from '@/api/endpoints'
import { qk } from '@/api/keys'
import type { Cart, CartItem } from '@/api/types'
import { useMe } from '@/auth'
import { AddressSheet } from '@/components/AddressSheet'
import { ProductImage } from '@/components/ProductImage'
import { PullToRefresh } from '@/components/PullToRefresh'
import { QtyStepper } from '@/components/QtyStepper'
import { StickyActionBar } from '@/components/StickyActionBar'
import { BackButton, Badge, EmptyState, ErrorState, KeyValue, LinkButton, Skeleton } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { useDebounced, useDeliveryAddress } from '@/hooks'
import { fmt, S } from '@/i18n'
import { CountUp, haptic, useMotion } from '@/motion'
import { clearReorderPrices, readReorderPrices } from '@/lib/reorder'
import { usePhoto } from '@/lib/photos'
import { money } from '@/lib/utils'
import { waLink } from '@/lib/whatsapp'

/** Products whose total quantity (all sizes/colours, all rows) is under the minimum order. */
export function moqProblems(cart: Cart) {
  const total = new Map<number, { qty: number; item: CartItem }>()
  for (const g of cart.groups)
    for (const i of g.items) total.set(i.productId, { qty: (total.get(i.productId)?.qty ?? 0) + i.quantity, item: i })
  return [...total.values()].filter((t) => t.qty < t.item.moq).map((t) => t.item)
}

export function CartTotals({ cart }: { cart: Cart }) {
  return (
    <div>
      <KeyValue label={S.cart.factory} value={money(cart.subTotal)} />
      <KeyValue label={S.cart.delivery} value={cart.shippingTotal === null ? <Badge tone="warn">{S.cart.onRequest}</Badge> : money(cart.shippingTotal)} />
      <KeyValue label={S.cart.tax} value={money(cart.taxTotal)} />
      <div className="mt-1 border-t border-line pt-1">
        <KeyValue strong label={S.cart.total} value={<CountUp value={cart.grandTotal} format={money} />} />
      </div>
      <p className="mt-1 text-xs text-mute">{S.cart.estimate}</p>
    </div>
  )
}

function CartRow({ item, onChange, onRemove }: { item: CartItem; onChange: (q: number) => void; onRemove: () => void }) {
  const photo = usePhoto(item.productId, 1, item.imageUrl)
  const [qty, setQty] = useState(item.quantity)
  const [leaving, setLeaving] = useState(false)
  const { still } = useMotion()
  const debounced = useDebounced(qty, 500)
  const sent = useRef(item.quantity)
  useEffect(() => {
    if (debounced >= 1 && debounced !== sent.current) {
      sent.current = debounced
      onChange(debounced)
    }
  })

  const remove = () => {
    haptic()
    if (still) return onRemove()
    setLeaving(true)
    window.setTimeout(onRemove, 180)
  }

  return (
    <li
      className={`grid grid-cols-[76px_minmax(0,1fr)] gap-3 rounded-card bg-soft p-3 transition-[opacity,transform] duration-200 ${leaving ? '-translate-x-4 opacity-0' : ''}`}
    >
      <Link
        to={`/shop/p/${item.slug}`}
        viewTransition
        aria-label={item.title}
        className="photo-slot size-[76px] items-start"
        style={{ '--shadow-bottom': '2px', '--shadow-width': '60%', '--shadow-height': '8px' } as CSSProperties}
      >
        <ProductImage src={photo} alt="" width={120} height={120} className="relative z-[1] mt-1 size-[62px] rounded-2xl" />
      </Link>
      <div className="flex min-w-0 flex-col gap-1">
        <p className="line-clamp-2 break-words text-[15px] font-medium leading-5">{item.title}</p>
        <p className="text-[13px] text-mute">
          {item.colour} · {item.size} · {fmt(S.cart.each, { price: money(item.unitPrice) })}
        </p>
        <div className="mt-1 flex flex-wrap items-center justify-between gap-x-3 gap-y-2">
          <QtyStepper label={item.title} min={1} value={qty} onChange={setQty} />
          <b className="text-[15px] tabular-nums">
            <CountUp value={item.unitPrice * qty} format={money} />
          </b>
        </div>
        <button type="button" onClick={remove} className="press -ml-1 min-h-11 self-start rounded-full px-2 text-[13px] text-mute underline">
          {S.cart.remove}
        </button>
      </div>
    </li>
  )
}

function Header() {
  return (
    <div className="flex items-center gap-1.5 px-4 pt-[18px]">
      <BackButton to="/shop" />
      <span className="text-[15px] font-medium">{S.cart.title}</span>
    </div>
  )
}

function CartSkeleton() {
  return (
    <div role="status" aria-label={S.app.loading}>
      <Header />
      <div className="space-y-3 px-5 pt-4">
        <Skeleton className="h-16 w-3/4" />
        <Skeleton className="h-14" />
        <Skeleton className="h-28 !rounded-card" />
        <Skeleton className="h-28 !rounded-card" />
      </div>
    </div>
  )
}

export default function CartPage() {
  const qc = useQueryClient()
  const toast = useToast()
  const { data: me } = useMe()
  const { addr } = useDeliveryAddress()
  const [changing, setChanging] = useState(false)
  const [oldPrices, setOldPrices] = useState(readReorderPrices)
  const cart = useQuery({ queryKey: ['cart'], queryFn: cartApi.get })
  const apply = (c: Cart) => qc.setQueryData(['cart'], c)
  // Optimistic: the row shows the new quantity at once. Money is never calculated here; totals stay as the server last sent them until its answer arrives.
  const update = useMutation({
    mutationFn: (v: { id: number; q: number }) => cartApi.update(v.id, v.q),
    onMutate: async (v) => {
      await qc.cancelQueries({ queryKey: qk.cart })
      const before = qc.getQueryData<Cart>(qk.cart)
      if (before) qc.setQueryData<Cart>(qk.cart, { ...before, groups: before.groups.map((g) => ({ ...g, items: g.items.map((i) => (i.id === v.id ? { ...i, quantity: v.q } : i)) })) })
      return { before }
    },
    onSuccess: apply,
    onError: (e, _v, ctx) => {
      if (ctx?.before) qc.setQueryData(qk.cart, ctx.before)
      toast.error(e.message)
    },
  })
  const remove = useMutation({ mutationFn: (id: number) => cartApi.remove(id), onSuccess: apply, onError: (e) => toast.error(e.message) })

  const removeWithUndo = (item: CartItem) =>
    remove.mutate(item.id, {
      onSuccess: () =>
        toast.undo(fmt(S.cart.removed, { name: item.title }), S.app.undo, () => {
          cartApi
            .add(item.variantId, item.quantity)
            .then(apply)
            .catch((e: Error) => toast.error(e.message))
        }),
    })

  if (cart.isLoading) return <CartSkeleton />
  if (cart.error) {
    return (
      <div>
        <Header />
        <ErrorState error={cart.error} onRetry={() => void cart.refetch()} />
      </div>
    )
  }
  const c = cart.data!
  if (c.groups.length === 0) {
    return (
      <div>
        <Header />
        <EmptyState icon={<ShoppingBag className="size-8" />} title={S.cart.empty} sub={S.cart.emptySub} action={<LinkButton to="/shop">{S.nav.home}</LinkButton>} />
      </div>
    )
  }

  const problems = moqProblems(c)
  const pieces = c.groups.reduce((a, g) => a + g.items.reduce((x, i) => x + i.quantity, 0), 0)
  const changed = c.groups.flatMap((g) => g.items).filter((i) => oldPrices[i.variantId] !== undefined && oldPrices[i.variantId] !== i.unitPrice)
  const dismissNotice = () => {
    clearReorderPrices()
    setOldPrices({})
  }

  return (
    <div className="pb-48">
      <PullToRefresh onRefresh={() => cart.refetch()} />
      <Header />
      <h1 className="mx-5 mt-1.5 text-[30px] font-semibold leading-[1.2] tracking-[-0.02em]">
        {fmt(S.cart.pieces, { n: pieces })}
        <br />
        <span className="font-medium text-mute">{fmt(c.groups.length === 1 ? S.cart.fromSeller : S.cart.fromSellers, { n: c.groups.length })}</span>
      </h1>

      {addr && (
        <button type="button" onClick={() => setChanging(true)} className="press mx-5 mt-4 flex w-[calc(100%-40px)] items-center gap-3 rounded-[18px] border border-line bg-surface px-4 py-3.5 text-left">
          <MapPin className="size-5 shrink-0" aria-hidden />
          <span className="flex min-w-0 flex-1 flex-col">
            <span className="truncate text-sm font-medium">{me?.business?.displayName ?? S.cart.deliverTo}</span>
            <span className="truncate text-[13px] text-mute">{addr.line1}, {addr.cityName} {addr.pincode}</span>
          </span>
          <span className="shrink-0 text-[13px] font-medium underline">{S.app.change}</span>
        </button>
      )}

      {changed.length > 0 && (
        <div role="status" className="mx-5 mt-3 flex items-start gap-2 rounded-[14px] bg-saving-bg px-3.5 py-3 text-[13px] leading-snug text-saving">
          <p className="min-w-0 flex-1 space-y-1">
            {changed.map((i) => (
              <span key={i.id} className="block break-words">{fmt(S.cart.priceUpdated, { name: i.title, now: money(i.unitPrice), was: money(oldPrices[i.variantId]) })}</span>
            ))}
          </p>
          <button type="button" aria-label={S.cart.dismiss} onClick={dismissNotice} className="press -m-2 flex size-11 shrink-0 items-center justify-center rounded-full">
            <X className="size-4" aria-hidden />
          </button>
        </div>
      )}

      {problems.map((i) => (
        <div key={i.productId} role="alert" className="mx-5 mt-3 flex gap-2 rounded-[14px] bg-saving-bg px-3.5 py-3 text-[13px] leading-snug text-saving">
          <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />
          <span className="break-words">{i.title}: {fmt(S.cart.moqWarn, { moq: `${i.moq} ${S.units[i.unit]}` })}</span>
        </div>
      ))}

      {c.groups.map((g) => (
        <section key={g.manufacturerId} aria-label={g.manufacturerName}>
          <div className="flex items-baseline justify-between gap-3 px-5 pb-2.5 pt-[18px] text-[13px] text-mute">
            <span className="min-w-0 truncate">
              <b className="font-semibold text-ink">{g.manufacturerName}</b>
            </span>
            <span className="shrink-0 tabular-nums">{money(g.subTotal)}</span>
          </div>
          <ul className="space-y-2.5 px-5">
            {g.items.map((i) => (
              <CartRow key={`${i.id}-${i.quantity}`} item={i} onChange={(q) => update.mutate({ id: i.id, q })} onRemove={() => removeWithUndo(i)} />
            ))}
          </ul>
        </section>
      ))}

      <div className="px-5 pt-5">
        <p className="mb-3 text-xs text-mute">{S.cart.slabNote}</p>
        <CartTotals cart={c} />
        <a
          href={waLink(S.cart.waHelpMsg)}
          target="_blank"
          rel="noreferrer"
          className="press mt-4 flex min-h-[52px] items-center gap-2.5 rounded-2xl border border-line bg-surface px-4 text-sm"
        >
          <MessageCircle className="size-5 shrink-0" aria-hidden />
          {S.cart.help}
        </a>
      </div>

      <StickyActionBar>
        <p className="text-center text-xs text-mute">{S.cart.escrow}</p>
        {problems.length > 0 ? (
          <span aria-disabled className="flex min-h-14 items-center justify-center rounded-pill bg-ink/40 text-base font-medium text-background">
            {fmt(S.cart.checkoutTotal, { total: money(c.grandTotal) })}
          </span>
        ) : (
          <LinkButton to="/shop/checkout" className="min-h-14 w-full text-base">
            {fmt(S.cart.checkoutTotal, { total: money(c.grandTotal) })}
          </LinkButton>
        )}
      </StickyActionBar>

      <AddressSheet open={changing} onClose={() => setChanging(false)} />
    </div>
  )
}
