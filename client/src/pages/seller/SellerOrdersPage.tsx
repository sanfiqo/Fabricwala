import { useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Inbox, PackageCheck } from 'lucide-react'
import { sellerCanMarkDelivery, sellerOrderApi } from '@/api/endpoints'
import { qk } from '@/api/keys'
import type { SellerOrder, SellerOrderStatus } from '@/api/types'
import { InvoiceRow } from '@/components/InvoiceRow'
import { OrderStatusBadge, PaymentBadge, StatusTimeline } from '@/components/OrderParts'
import { StickyActionBar } from '@/components/StickyActionBar'
import { Button } from '@/components/ui/button'
import { Field, Input, Textarea } from '@/components/ui/field'
import { BackButton, Chip, EmptyState, ErrorState, Sheet, Skeleton } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { fmt, S } from '@/i18n'
import { haptic } from '@/motion'
import { useCountdown } from '@/lib/useCountdown'
import { formatDate, money } from '@/lib/utils'

type Tab = 'new' | 'pack' | 'shipped' | 'done'
const tabOf = (s: SellerOrderStatus): Tab => (s === 'PendingAcceptance' ? 'new' : s === 'Accepted' ? 'pack' : s === 'Dispatched' || s === 'ReachedKashmir' ? 'shipped' : 'done')
const ACCEPT_HOURS = 24
const deadline = (o: SellerOrder) => o.acceptDeadline ?? new Date(new Date(o.placedAt).getTime() + ACCEPT_HOURS * 3_600_000).toISOString()
const time = (iso: string) => new Date(iso).toLocaleTimeString('en-IN', { hour: 'numeric', minute: '2-digit' })

/** Accept, cannot do, and ship with LR number. One place, used by the list and the detail page. */
function useActions() {
  const qc = useQueryClient()
  const toast = useToast()
  const [reject, setReject] = useState<SellerOrder | null>(null)
  const [ship, setShip] = useState<SellerOrder | null>(null)
  const [reason, setReason] = useState('')
  const [lr, setLr] = useState('')
  const [carrier, setCarrier] = useState('')
  const [formError, setFormError] = useState('')

  const done = async (o: SellerOrder) => {
    haptic()
    qc.setQueryData(qk.sellerOrder(o.id), o)
    await Promise.all([qc.invalidateQueries({ queryKey: qk.sellerOrders }), qc.invalidateQueries({ queryKey: qk.sellerDashboard })])
    setReject(null)
    setShip(null)
    setReason('')
    setLr('')
    setCarrier('')
  }
  const fail = (e: Error) => (reject || ship ? setFormError(e.message) : toast.error(e.message))
  const accept = useMutation({ mutationFn: (o: SellerOrder) => sellerOrderApi.accept(o.id), onSuccess: done, onError: fail })
  const rejectM = useMutation({ mutationFn: (v: { o: SellerOrder; r: string }) => sellerOrderApi.reject(v.o.id, v.r), onSuccess: done, onError: fail })
  const shipM = useMutation({ mutationFn: (v: { o: SellerOrder; lr: string; c: string }) => sellerOrderApi.dispatch(v.o.id, v.lr, v.c), onSuccess: done, onError: fail })
  const status = useMutation({ mutationFn: (v: { o: SellerOrder; s: 'ReachedKashmir' | 'Delivered' }) => sellerOrderApi.setStatus(v.o.id, v.s), onSuccess: done, onError: fail })

  const sheets = (
    <>
      <Sheet open={!!reject} onClose={() => setReject(null)} title={S.so.sheetReject}>
        <div className="space-y-4">
          <p className="text-sm text-mute">{S.so.rejectNote}</p>
          <Field label={S.orders.rejectReason} error={formError}>
            {(id, inv) => <Textarea id={id} invalid={inv} value={reason} onChange={(e) => { setReason(e.target.value); setFormError('') }} />}
          </Field>
          <Button
            block
            variant="danger"
            loading={rejectM.isPending}
            onClick={() => (reason.trim() ? reject && rejectM.mutate({ o: reject, r: reason.trim() }) : setFormError(S.seller.needReason))}
          >
            {S.so.cantDo}
          </Button>
        </div>
      </Sheet>
      <Sheet open={!!ship} onClose={() => setShip(null)} title={S.seller.dispatchTitle}>
        <div className="space-y-4">
          <Field label={S.seller.lrNumber} error={formError}>
            {(id, inv) => <Input id={id} invalid={inv} value={lr} autoCapitalize="characters" onChange={(e) => { setLr(e.target.value); setFormError('') }} />}
          </Field>
          <Field label={`${S.seller.carrier}`}>{(id) => <Input id={id} value={carrier} onChange={(e) => setCarrier(e.target.value)} />}</Field>
          <Button block loading={shipM.isPending} onClick={() => (lr.trim() ? ship && shipM.mutate({ o: ship, lr: lr.trim(), c: carrier.trim() }) : setFormError(S.seller.needLr))}>
            {S.seller.dispatch}
          </Button>
        </div>
      </Sheet>
    </>
  )
  return {
    accept: (o: SellerOrder) => accept.mutate(o),
    accepting: accept.isPending,
    openReject: (o: SellerOrder) => { setFormError(''); setReject(o) },
    openShip: (o: SellerOrder) => { setFormError(''); setShip(o) },
    setStatus: (o: SellerOrder, s: 'ReachedKashmir' | 'Delivered') => status.mutate({ o, s }),
    statusPending: status.isPending,
    sheets,
  }
}
type Actions = ReturnType<typeof useActions>

function Money({ o }: { o: SellerOrder }) {
  return (
    <div className="border-t border-line pb-1 pt-2.5 text-[13px] text-mute">
      <div className="flex justify-between py-0.5"><span>{S.so.orderValue}</span><span className="tabular-nums">{money(o.orderValue ?? o.subTotal)}</span></div>
      <div className="flex justify-between py-0.5"><span>{fmt(S.so.fee, { r: o.commissionRate })}</span><span className="tabular-nums">− {money(o.platformFee ?? o.commissionAmount)}</span></div>
      <div className="flex justify-between pt-1.5 text-[15px] font-semibold text-ink"><span>{S.so.youGet}</span><span className="tabular-nums">{money(o.youGet ?? o.subTotal - o.commissionAmount)}</span></div>
    </div>
  )
}

function Countdown({ o }: { o: SellerOrder }) {
  const c = useCountdown(deadline(o))
  return (
    <span className={c.over ? 'rounded-full bg-error-bg px-2.5 py-1 text-xs font-medium text-error' : 'rounded-full bg-saving-bg px-2.5 py-1 text-xs font-medium text-saving'}>
      {c.over ? S.so.timeUp : fmt(S.so.replyIn, { t: c.text ?? '' })}
    </span>
  )
}

function OrderCard({ o, tab, a }: { o: SellerOrder; tab: Tab; a: Actions }) {
  const paid = o.paymentStatus === 'Paid'
  return (
    <article className={tab === 'new' ? 'overflow-hidden rounded-card border border-ink bg-surface' : 'overflow-hidden rounded-card border border-line bg-surface'}>
      <Link to={`/seller/orders/${o.id}`} viewTransition className="press block">
        <div className="flex items-start justify-between gap-2 px-4 pt-3.5">
          <span className="flex min-w-0 flex-col gap-0.5">
            <span className="truncate text-base font-semibold">{o.retailerName}</span>
            <span className="text-[13px] text-mute">{o.deliveryCity} · {o.subOrderNumber} · {time(o.placedAt)}</span>
          </span>
          {tab === 'new' && <Countdown o={o} />}
          {tab !== 'new' && <OrderStatusBadge status={o.status} />}
        </div>
        <ul className="px-4 pt-3 text-[15px]">
          {o.items.map((i) => (
            <li key={i.id} className="flex justify-between gap-3 pb-2">
              <span className="min-w-0 break-words">{fmt(S.so.pieces, { n: i.quantity, title: i.title })}</span>
              <span className="shrink-0 tabular-nums">{money(i.lineTotal)}</span>
            </li>
          ))}
        </ul>
      </Link>
      <div className="px-4 pb-3.5">
        <Money o={o} />
        {tab === 'new' && (
          <div className="mt-2.5 space-y-2.5">
            <p className="text-xs leading-snug text-mute">{paid ? S.so.paidNote : S.so.waitPay}</p>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" onClick={() => a.openReject(o)}>{S.so.cantDo}</Button>
              <Button size="sm" className="flex-1" disabled={!paid} loading={a.accepting} onClick={() => a.accept(o)}>{S.so.accept}</Button>
            </div>
          </div>
        )}
        {tab === 'pack' && (
          <Button size="sm" block className="mt-3" onClick={() => a.openShip(o)}>{S.so.shipNow}</Button>
        )}
      </div>
    </article>
  )
}

export function SellerOrdersPage() {
  const [tab, setTab] = useState<Tab>('new')
  const orders = useQuery({ queryKey: qk.sellerOrders, queryFn: sellerOrderApi.list })
  const a = useActions()
  const all = [...(orders.data ?? [])].sort((x, y) => y.placedAt.localeCompare(x.placedAt))
  const count = (t: Tab) => all.filter((o) => tabOf(o.status) === t).length
  const list = all.filter((o) => tabOf(o.status) === tab)
  const empty = { new: S.so.emptyNew, pack: S.so.emptyPack, shipped: S.so.emptyShipped, done: S.so.emptyDone }[tab]

  return (
    <div className="pb-4">
      <h1 className="mx-5 mt-7 text-[30px] font-semibold tracking-[-0.02em]">{S.so.title}</h1>
      <nav aria-label={S.so.title} className="mx-5 mt-3.5 flex gap-2 overflow-x-auto [scrollbar-width:none]">
        {([['new', S.so.tabNew], ['pack', S.so.tabPack], ['shipped', S.so.tabShipped], ['done', S.so.tabDone]] as const).map(([t, l]) => (
          <Chip key={t} active={tab === t} onClick={() => setTab(t)} className="shrink-0">
            {l}
            {t !== 'done' && count(t) > 0 && ` · ${count(t)}`}
          </Chip>
        ))}
      </nav>
      <div className="space-y-3 px-5 pt-4">
        {orders.isLoading ? (
          <div role="status" aria-label={S.app.loading} className="space-y-3">
            <Skeleton className="h-64 !rounded-card" />
            <Skeleton className="h-48 !rounded-card" />
          </div>
        ) : orders.error && !orders.data ? (
          <ErrorState error={orders.error} onRetry={() => void orders.refetch()} />
        ) : list.length === 0 ? (
          <EmptyState icon={<Inbox className="size-8" />} title={empty} sub={S.so.emptySub} />
        ) : (
          list.map((o) => <OrderCard key={o.id} o={o} tab={tab} a={a} />)
        )}
      </div>
      {a.sheets}
    </div>
  )
}

export function SellerOrderDetailPage() {
  const { id = '' } = useParams()
  const order = useQuery({ queryKey: qk.sellerOrder(Number(id)), queryFn: () => sellerOrderApi.get(Number(id)) })
  const a = useActions()

  const Top = (
    <div className="flex items-center gap-1.5 px-4 pt-[18px]">
      <BackButton to="/seller/orders" />
      <span className="text-[15px] font-medium">{order.data?.subOrderNumber ?? ''}</span>
    </div>
  )
  if (order.isLoading) {
    return (
      <div role="status" aria-label={S.app.loading}>
        {Top}
        <div className="space-y-3 px-5 pt-4"><Skeleton className="h-10 w-2/3" /><Skeleton className="h-52 !rounded-card" /></div>
      </div>
    )
  }
  if (order.error || !order.data) return <div>{Top}<ErrorState error={order.error ?? new Error(S.orders.notFound)} onRetry={() => void order.refetch()} /></div>
  const o = order.data
  const paid = o.paymentStatus === 'Paid'

  let bar: ReactNode = null
  if (o.status === 'PendingAcceptance') {
    bar = paid ? (
      <div className="flex gap-2">
        <Button variant="outline" onClick={() => a.openReject(o)}>{S.so.cantDo}</Button>
        <Button size="lg" className="flex-1" loading={a.accepting} onClick={() => a.accept(o)}>{S.so.accept}</Button>
      </div>
    ) : (
      <p className="rounded-2xl bg-saving-bg p-3 text-sm font-medium text-saving">{S.so.waitPay}</p>
    )
  } else if (o.status === 'Accepted') {
    bar = <Button size="lg" block onClick={() => a.openShip(o)}>{S.so.shipNow}</Button>
  } else if (o.status === 'Dispatched' && !sellerCanMarkDelivery()) {
    bar = null // The shopkeeper confirms delivery.
  } else if (o.status === 'Dispatched') {
    bar = (
      <div className="flex gap-2">
        <Button variant="soft" loading={a.statusPending} onClick={() => a.setStatus(o, 'ReachedKashmir')}>{S.so.reached}</Button>
        <Button size="lg" className="flex-1" loading={a.statusPending} onClick={() => a.setStatus(o, 'Delivered')}><PackageCheck className="size-5" aria-hidden />{S.so.deliveredMark}</Button>
      </div>
    )
  } else if (o.status === 'ReachedKashmir') {
    bar = <Button size="lg" block loading={a.statusPending} onClick={() => a.setStatus(o, 'Delivered')}><PackageCheck className="size-5" aria-hidden />{S.so.deliveredMark}</Button>
  }

  return (
    <div className={bar ? 'pb-40' : 'pb-6'}>
      {Top}
      <div className="px-5 pt-1.5">
        <p className="text-[15px] text-mute">{o.retailerName} · {o.deliveryCity}</p>
        <h1 className="mt-0.5 text-balance text-[30px] font-semibold leading-[1.15] tracking-[-0.02em]">{S.orders.headline[o.status]}</h1>
        <p className="mt-1 text-[13px] text-mute">{formatDate(o.placedAt)} · <PaymentBadge status={o.paymentStatus} /></p>
      </div>

      <div className="mx-5 mt-5 overflow-hidden rounded-card border border-line bg-surface">
        <ul className="px-4 pt-3.5 text-[15px]">
          {o.items.map((i) => (
            <li key={i.id} className="flex justify-between gap-3 pb-2.5">
              <span className="min-w-0 break-words">{fmt(S.so.pieces, { n: i.quantity, title: i.title })}<span className="block text-[13px] text-mute">{i.colour} · {i.size}</span></span>
              <span className="shrink-0 tabular-nums">{money(i.lineTotal)}</span>
            </li>
          ))}
        </ul>
        <div className="px-4 pb-3"><Money o={o} /></div>
        {(o.paymentStatus === 'Paid' || o.paymentStatus === 'PartiallyRefunded') && o.status !== 'Rejected' && o.status !== 'Cancelled' && (
          <>
            <InvoiceRow sellerOrderId={o.id} label={S.invoice.download} />
            <InvoiceRow sellerOrderId={o.id} type="PlatformCommission" label={S.invoice.fee} />
          </>
        )}
      </div>

      {o.shipment && (
        <div className="mx-5 mt-4 rounded-card bg-soft p-4 text-sm">
          <div className="flex justify-between gap-3"><span className="text-mute">{S.orders.carrier}</span><span>{o.shipment.carrierName || '—'}</span></div>
          <div className="flex justify-between gap-3 pt-1"><span className="text-mute">{S.orders.lr}</span><span className="font-medium">{o.shipment.lrNumber}</span></div>
        </div>
      )}
      {o.rejectReason && <p className="mx-5 mt-4 rounded-2xl bg-error-bg p-3.5 text-sm font-medium text-error">{S.orders.rejectReason}: {o.rejectReason}</p>}

      <div className="mx-5 mt-6"><StatusTimeline order={o} /></div>

      {bar && <StickyActionBar>{bar}</StickyActionBar>}
      {a.sheets}
    </div>
  )
}
