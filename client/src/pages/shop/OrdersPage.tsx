import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronRight, MessageCircle, Package, PackageCheck, Truck } from 'lucide-react'
import { orderApi } from '@/api/endpoints'
import { qk } from '@/api/keys'
import type { Order, SellerOrder } from '@/api/types'
import { InvoiceRow } from '@/components/InvoiceRow'
import { RateOrderRow } from '@/components/RateOrder'
import { canConfirm, PaymentBadge, StatusTimeline, StepBar } from '@/components/OrderParts'
import { StickyActionBar } from '@/components/StickyActionBar'
import { Button } from '@/components/ui/button'
import { BackButton, EmptyState, ErrorState, LinkButton, Skeleton } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { fmt, S } from '@/i18n'
import { haptic } from '@/motion'
import { cn, formatDate, formatShortDate, money } from '@/lib/utils'
import { waLink } from '@/lib/whatsapp'

type Filter = 'active' | 'delivered' | 'problems'
const AUTO_CONFIRM_DAYS = 7 // UX_RULES 3: paid after delivery is confirmed, or automatically after 7 days

const dotTone = (st: SellerOrder['status']) => (st === 'Delivered' || st === 'ReachedKashmir' || st === 'Dispatched' ? 'bg-ink' : st === 'Rejected' || st === 'Cancelled' ? 'bg-error' : 'bg-[#B9B5AC]')
const pieces = (so: SellerOrder) => so.items.reduce((a, i) => a + i.quantity, 0)
const needsAction = (o: Order) => o.sellerOrders.some((s) => canConfirm(s.status))
const isProblem = (o: Order) => o.sellerOrders.length > 0 && o.sellerOrders.every((s) => s.status === 'Rejected' || s.status === 'Cancelled')
const isDone = (o: Order) => o.sellerOrders.length > 0 && o.sellerOrders.every((s) => s.status === 'Delivered')
const reachedAt = (so: SellerOrder) => so.history.find((h) => h.status === 'ReachedKashmir' || h.status === 'Dispatched')?.changedAt ?? so.placedAt

function OrderRow({ so, showStatus }: { so: SellerOrder; showStatus?: boolean }) {
  return (
    <div className="flex items-center gap-3 px-4 py-3">
      <span className="flex size-[52px] shrink-0 items-center justify-center rounded-[18px] bg-brand-soft text-brand" aria-hidden>
        <Package className="size-6" />
      </span>
      <span className="flex min-w-0 flex-1 flex-col gap-0.5">
        <span className="truncate text-[15px] font-medium">{so.manufacturerName}</span>
        <span className="text-[13px] text-mute">{fmt(S.orders.pieces, { n: pieces(so) })} · {money(so.total)}</span>
      </span>
      {showStatus && (
        <span className="flex shrink-0 items-center gap-1.5 text-[13px] font-medium">
          <span className={cn('size-2 rounded-full', dotTone(so.status))} aria-hidden />
          {S.orderStatus[so.status]}
        </span>
      )}
    </div>
  )
}

function ConfirmCard({ o }: { o: Order }) {
  const qc = useQueryClient()
  const toast = useToast()
  const so = o.sellerOrders.find((s) => canConfirm(s.status))!
  const auto = new Date(new Date(reachedAt(so)).getTime() + AUTO_CONFIRM_DAYS * 86_400_000).toISOString()
  const confirm = useMutation({
    mutationFn: () => orderApi.confirmDelivery(o.id),
    onSuccess: async (n) => {
      haptic()
      qc.setQueryData(qk.order(o.id), n)
      await qc.invalidateQueries({ queryKey: qk.orders })
      toast.success(S.orders.confirmed)
    },
    onError: (e) => toast.error(e.message),
  })
  return (
    <div className="overflow-hidden rounded-card border border-ink bg-surface">
      <div className="flex items-baseline justify-between gap-2 border-b border-line px-4 py-3.5">
        <span className="text-[15px] font-semibold">{fmt(S.orders.reachedOn, { d: formatShortDate(reachedAt(so)) })}</span>
        <span className="text-[13px] text-mute">{o.orderNumber}</span>
      </div>
      <OrderRow so={so} />
      <div className="flex flex-col gap-2.5 border-t border-line px-4 pb-4 pt-3">
        <p className="text-[13px] leading-snug text-mute">{fmt(S.orders.confirmHint, { d: formatShortDate(auto) })}</p>
        <div className="flex gap-2">
          <Link to={`/shop/orders/${o.id}/report`} className="press flex min-h-11 items-center justify-center rounded-pill border border-line px-4 text-sm font-medium">
            {S.orders.reportProblem}
          </Link>
          <Button size="sm" className="flex-1" loading={confirm.isPending} onClick={() => confirm.mutate()}>
            {S.orders.allGood}
          </Button>
        </div>
      </div>
    </div>
  )
}

function OrderCard({ o }: { o: Order }) {
  return (
    <Link to={`/shop/orders/${o.id}`} viewTransition className="press block overflow-hidden rounded-card border border-line bg-surface shadow-card">
      <div className="flex items-baseline justify-between gap-2 border-b border-line px-4 py-3.5">
        <span className="text-[15px] font-semibold">{fmt(S.orders.orderedOn, { d: formatShortDate(o.placedAt) })} · {money(o.grandTotal)}</span>
        <span className="shrink-0 text-[13px] text-mute">{o.orderNumber}</span>
      </div>
      <div className="divide-y divide-line">
        {o.sellerOrders.map((so) => <OrderRow key={so.id} so={so} showStatus />)}
      </div>
      <div className="flex justify-end border-t border-line px-4 py-3">
        <span className="inline-flex min-h-10 items-center gap-1.5 rounded-pill bg-brand px-4 text-sm font-medium text-white">
          {S.orders.whereIs}
          <ChevronRight className="size-4" aria-hidden />
        </span>
      </div>
    </Link>
  )
}

function ListSkeleton() {
  return (
    <div role="status" aria-label={S.app.loading} className="space-y-3 px-5">
      <Skeleton className="h-36 !rounded-card" />
      <Skeleton className="h-44 !rounded-card" />
    </div>
  )
}

export function OrdersPage() {
  const orders = useQuery({ queryKey: qk.orders, queryFn: orderApi.list })
  const [filter, setFilter] = useState<Filter>('active')
  const list = useMemo(() => [...(orders.data ?? [])].sort((a, b) => b.placedAt.localeCompare(a.placedAt)), [orders.data])
  const shown = list.filter((o) => (filter === 'delivered' ? isDone(o) : filter === 'problems' ? isProblem(o) : !isDone(o) && !isProblem(o)))
  const action = filter === 'active' ? shown.filter(needsAction) : []
  const rest = shown.filter((o) => !action.includes(o))
  const activeCount = list.filter((o) => !isDone(o) && !isProblem(o)).length

  return (
    <div className="pb-4">
      <h1 className="mx-5 mt-7 text-[30px] font-semibold tracking-[-0.02em]">{S.orders.title}</h1>
      <nav aria-label={S.orders.title} className="mx-5 mt-4 flex gap-1 rounded-pill bg-soft p-1">
        {([['active', S.orders.filterActive + (activeCount > 0 ? ` · ${activeCount}` : '')], ['delivered', S.orders.filterDelivered], ['problems', S.orders.filterProblems]] as const).map(([k, text]) => (
          <button
            key={k}
            type="button"
            aria-pressed={filter === k}
            onClick={() => setFilter(k)}
            className={cn('press min-h-10 flex-1 truncate rounded-pill px-3 text-sm font-medium transition-colors', filter === k ? 'bg-brand text-white shadow-card' : 'text-mute')}
          >
            {text}
          </button>
        ))}
      </nav>

      <div className="pt-4">
        {orders.isLoading ? (
          <ListSkeleton />
        ) : orders.error && !orders.data ? (
          <ErrorState error={orders.error} onRetry={() => void orders.refetch()} />
        ) : list.length === 0 ? (
          <EmptyState icon={<Package className="size-8" />} title={S.orders.empty} sub={S.orders.emptySub} action={<LinkButton to="/shop">{S.orders.startShopping}</LinkButton>} />
        ) : shown.length === 0 ? (
          <EmptyState icon={<Package className="size-8" />} title={S.orders.emptyFilter} sub={S.orders.emptyFilterSub} />
        ) : (
          <div className="space-y-5 px-5">
            {action.length > 0 && (
              <section aria-label={S.orders.needsAction} className="space-y-2.5">
                <h2 className="text-[13px] text-mute">{S.orders.needsAction}</h2>
                {action.map((o) => <ConfirmCard key={o.id} o={o} />)}
              </section>
            )}
            {rest.length > 0 && (
              <section aria-label={S.orders.onTheWay} className="space-y-2.5">
                {action.length > 0 && <h2 className="text-[13px] text-mute">{S.orders.onTheWay}</h2>}
                {rest.map((o) => <OrderCard key={o.id} o={o} />)}
              </section>
            )}
          </div>
        )}
      </div>
    </div>
  )
}

function DetailHeader({ title }: { title: string }) {
  return (
    <div className="flex items-center gap-1.5 px-4 pt-[18px]">
      <BackButton to="/shop/orders" />
      <span className="text-[15px] font-medium">{title}</span>
    </div>
  )
}

export function OrderDetailPage() {
  const { id = '' } = useParams()
  const qc = useQueryClient()
  const toast = useToast()
  const order = useQuery({ queryKey: qk.order(Number(id)), queryFn: () => orderApi.get(Number(id)) })
  const [sel, setSel] = useState(0)
  const confirm = useMutation({
    mutationFn: () => orderApi.confirmDelivery(Number(id)),
    onSuccess: async (o) => {
      haptic()
      qc.setQueryData(qk.order(Number(id)), o)
      await qc.invalidateQueries({ queryKey: qk.orders })
      toast.success(S.orders.confirmed)
    },
    onError: (e) => toast.error(e.message),
  })

  if (order.isLoading) {
    return (
      <div role="status" aria-label={S.app.loading}>
        <DetailHeader title="" />
        <div className="space-y-3 px-5 pt-4">
          <Skeleton className="h-4 w-1/2" />
          <Skeleton className="h-14 w-3/4" />
          <Skeleton className="h-12 !rounded-pill" />
          <Skeleton className="h-52 !rounded-card" />
        </div>
      </div>
    )
  }
  if (order.error || !order.data) {
    return (
      <div>
        <DetailHeader title="" />
        <ErrorState error={order.error ?? new Error(S.orders.notFound)} onRetry={() => void order.refetch()} />
      </div>
    )
  }
  const o = order.data
  const so = o.sellerOrders[Math.min(sel, o.sellerOrders.length - 1)]
  const failed = so.status === 'Rejected' || so.status === 'Cancelled'
  const canReport = canConfirm(so.status) || so.status === 'Delivered'

  return (
    <div className={cn(canConfirm(so.status) ? 'pb-48' : 'pb-6')}>
      <DetailHeader title={fmt(S.orders.number, { n: o.orderNumber })} />

      <div className="px-5 pt-1.5">
        <p className="text-[15px] text-mute">{so.manufacturerName} · {fmt(S.orders.pieces, { n: pieces(so) })}</p>
        <h1 className="mt-0.5 text-balance text-[34px] font-semibold leading-[1.1] tracking-[-0.025em]">{S.orders.headline[so.status]}</h1>
      </div>

      {o.sellerOrders.length > 1 && (
        <nav aria-label={S.orders.sellerTabs} className="mx-5 mt-[18px] grid gap-0 rounded-3xl bg-soft p-1" style={{ gridTemplateColumns: `repeat(${o.sellerOrders.length}, minmax(0, 1fr))` }}>
          {o.sellerOrders.map((s, i) => (
            <button
              key={s.id}
              type="button"
              aria-current={i === sel ? 'page' : undefined}
              onClick={() => setSel(i)}
              className={cn('press min-h-[42px] truncate rounded-[21px] px-2 text-sm', i === sel ? 'bg-surface font-medium' : 'text-mute')}
            >
              {s.manufacturerName}
            </button>
          ))}
        </nav>
      )}

      <div className="mx-5 mt-5">
        <StepBar key={so.id + so.status} status={so.status} />
      </div>

      {so.shipment ? (
        <div className="mx-5 mt-4 flex items-center gap-3 rounded-card bg-ink p-4 text-background">
          <span className="flex size-12 shrink-0 items-center justify-center rounded-full bg-background/15" aria-hidden>
            <Truck className="size-6" />
          </span>
          <span className="flex min-w-0 flex-1 flex-col gap-0.5">
            <span className="text-[15px] font-medium">{S.orders.inTransit}</span>
            <span className="break-words text-[13px] text-line">{so.shipment.carrierName || S.orders.carrier} · {S.orders.lr} {so.shipment.lrNumber}</span>
          </span>
        </div>
      ) : (
        failed && so.rejectReason && <p className="mx-5 mt-4 rounded-2xl bg-error-bg p-3.5 text-sm font-medium text-error">{S.orders.rejectReason}: {so.rejectReason}. {S.orders.refundNote}</p>
      )}

      <div className="mx-5 mt-6">
        <StatusTimeline order={so} />
      </div>

      <div className="mx-5 mt-6 overflow-hidden rounded-card border border-line bg-surface">
        {so.items.map((i) => (
          <div key={i.id} className="flex justify-between gap-3 px-4 py-3.5 text-[15px]">
            <span className="min-w-0 break-words">{i.quantity} {i.title}<span className="block text-[13px] text-mute">{i.colour} · {i.size}</span></span>
            <span className="shrink-0 tabular-nums">{money(i.lineTotal)}</span>
          </div>
        ))}
        <div className="space-y-1 border-t border-line px-4 py-3 text-sm text-mute">
          <div className="flex justify-between"><span>{S.cart.shipping}</span><span className="tabular-nums">{money(so.shippingAmount)}</span></div>
          <div className="flex justify-between"><span>{S.cart.tax}</span><span className="tabular-nums">{money(so.taxAmount)}</span></div>
          <div className="flex justify-between text-base font-semibold text-ink"><span>{S.cart.total}</span><span className="tabular-nums">{money(so.total)}</span></div>
          <div className="flex items-center justify-between pt-1"><span>{S.orders.paymentStatus}</span><PaymentBadge status={o.paymentStatus} /></div>
        </div>
        <a href={waLink(fmt(S.orders.waMsg, { n: o.orderNumber }))} target="_blank" rel="noreferrer" className="press flex min-h-[52px] items-center gap-3 border-t border-line px-4 text-sm">
          <MessageCircle className="size-5 shrink-0" aria-hidden />
          <span className="flex-1">{S.orders.helpWa}</span>
          <ChevronRight className="size-4" aria-hidden />
        </a>
        {(o.paymentStatus === 'Paid' || o.paymentStatus === 'PartiallyRefunded') && !failed && <InvoiceRow sellerOrderId={so.id} label={S.invoice.download} />}
        {o.sellerOrders.some((s) => s.status === 'Delivered') && <RateOrderRow orderId={o.id} />}
        {canReport && (
          <Link to={`/shop/orders/${o.id}/report`} className="press flex min-h-[52px] items-center gap-3 border-t border-line px-4 text-sm">
            <span className="flex-1">{S.orders.reportProblem}</span>
            <ChevronRight className="size-4" aria-hidden />
          </Link>
        )}
      </div>
      <p className="mx-5 mt-3 text-xs text-mute">{fmt(S.orders.placedOn, { d: formatDate(o.placedAt) })}</p>

      {canConfirm(so.status) && (
        <StickyActionBar>
          <p className="text-center text-xs text-mute">{S.cart.escrow}</p>
          <Button size="lg" block loading={confirm.isPending} onClick={() => confirm.mutate()}>
            <PackageCheck className="size-5" aria-hidden />
            {S.orders.confirmDelivery}
          </Button>
        </StickyActionBar>
      )}
    </div>
  )
}
