import { useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, Inbox, PackageCheck, Truck, XCircle } from 'lucide-react'
import { sellerOrderApi } from '@/api/endpoints'
import type { SellerOrder, SellerOrderStatus } from '@/api/types'
import { OrderStatusBadge, PaymentBadge, StatusTimeline } from '@/components/OrderParts'
import { Button } from '@/components/ui/button'
import { Field, Input, Textarea } from '@/components/ui/field'
import { Card, EmptyState, ErrorState, KeyValue, PageHeader, PageLoading, Segmented, Sheet, SectionTitle } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { S } from '@/i18n'
import { formatDate, money } from '@/lib/utils'

type Tab = 'new' | 'open' | 'done'
const tabOf = (s: SellerOrderStatus): Tab => (s === 'PendingAcceptance' ? 'new' : ['Accepted', 'Dispatched', 'ReachedKashmir'].includes(s) ? 'open' : 'done')

export function SellerOrdersPage() {
  const [tab, setTab] = useState<Tab>('new')
  const orders = useQuery({ queryKey: ['seller-orders'], queryFn: sellerOrderApi.list })
  if (orders.isLoading) return <PageLoading />
  if (orders.error) return <ErrorState error={orders.error} onRetry={() => void orders.refetch()} />
  const all = orders.data ?? []
  const count = (t: Tab) => all.filter((o) => tabOf(o.status) === t).length
  const list = all.filter((o) => tabOf(o.status) === tab)

  return (
    <div>
      <PageHeader title={S.seller.inboxTitle} />
      <div className="space-y-3 px-4 pt-4">
        <Segmented
          value={tab}
          onChange={setTab}
          options={[
            { value: 'new', label: S.seller.tabNew, count: count('new') },
            { value: 'open', label: S.seller.tabOpen, count: count('open') },
            { value: 'done', label: S.seller.tabDone },
          ]}
        />
        {list.length === 0 ? (
          <EmptyState icon={<Inbox className="size-8" />} title={S.seller.inboxEmpty} sub={S.seller.inboxEmptySub} />
        ) : (
          list.map((o) => (
            <Link key={o.id} to={`/seller/orders/${o.id}`} className="block">
              <Card className="p-4 active:bg-navy-50">
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="font-extrabold text-navy-900">{o.subOrderNumber}</p>
                    <p className="truncate text-sm text-mute">{o.retailerName} · {o.deliveryCity}</p>
                  </div>
                  <p className="text-lg font-extrabold text-navy-900">{money(o.total)}</p>
                </div>
                <p className="mt-2 line-clamp-1 text-sm">{o.items.map((i) => `${i.title} × ${i.quantity}`).join(', ')}</p>
                <div className="mt-3 flex flex-wrap items-center gap-2">
                  <OrderStatusBadge status={o.status} />
                  <PaymentBadge status={o.paymentStatus} />
                  <span className="text-xs text-mute">{formatDate(o.placedAt)}</span>
                </div>
              </Card>
            </Link>
          ))
        )}
      </div>
    </div>
  )
}

type Dialog = null | 'reject' | 'dispatch'

export function SellerOrderDetailPage() {
  const { id = '' } = useParams()
  const qc = useQueryClient()
  const toast = useToast()
  const [dialog, setDialog] = useState<Dialog>(null)
  const [reason, setReason] = useState('')
  const [lr, setLr] = useState('')
  const [carrier, setCarrier] = useState('')
  const [formError, setFormError] = useState('')
  const order = useQuery({ queryKey: ['seller-order', id], queryFn: () => sellerOrderApi.get(Number(id)) })

  const done = async (o: SellerOrder) => {
    qc.setQueryData(['seller-order', id], o)
    await qc.invalidateQueries({ queryKey: ['seller-orders'] })
    setDialog(null)
  }
  const run = <V,>(fn: (v: V) => Promise<SellerOrder>) => ({ mutationFn: fn, onSuccess: done, onError: (e: Error) => (dialog ? setFormError(e.message) : toast.error(e.message)) })
  const nid = Number(id)
  const accept = useMutation(run(() => sellerOrderApi.accept(nid)))
  const reject = useMutation(run((r: string) => sellerOrderApi.reject(nid, r)))
  const dispatch = useMutation(run((v: { lr: string; carrier: string }) => sellerOrderApi.dispatch(nid, v.lr, v.carrier)))
  const setStatus = useMutation(run((s: 'ReachedKashmir' | 'Delivered') => sellerOrderApi.setStatus(nid, s)))

  if (order.isLoading) return <PageLoading />
  if (order.error || !order.data) return <ErrorState error={order.error ?? new Error(S.orders.notFound)} onRetry={() => void order.refetch()} />
  const o = order.data
  const open = (d: Dialog) => { setFormError(''); setDialog(d) }

  let actions: ReactNode = null
  if (o.status === 'PendingAcceptance') {
    actions =
      o.paymentStatus === 'Paid' ? (
        <div className="space-y-2">
          <p className="text-sm font-semibold text-warn">{S.seller.deadline}</p>
          <Button block variant="accent" loading={accept.isPending} onClick={() => accept.mutate()}><CheckCircle2 className="size-5" />{S.seller.accept}</Button>
          <Button block variant="dangerOutline" onClick={() => open('reject')}><XCircle className="size-5" />{S.seller.reject}</Button>
        </div>
      ) : (
        <p className="rounded-xl bg-warn-bg p-3 text-sm font-semibold text-warn">{S.seller.notPaid}</p>
      )
  } else if (o.status === 'Accepted') {
    actions = <Button block variant="accent" onClick={() => open('dispatch')}><Truck className="size-5" />{S.seller.dispatch}</Button>
  } else if (o.status === 'Dispatched') {
    actions = (
      <div className="space-y-2">
        <Button block variant="outline" loading={setStatus.isPending} onClick={() => setStatus.mutate('ReachedKashmir')}>{S.seller.markReached}</Button>
        <Button block variant="accent" loading={setStatus.isPending} onClick={() => setStatus.mutate('Delivered')}><PackageCheck className="size-5" />{S.seller.markDelivered}</Button>
      </div>
    )
  } else if (o.status === 'ReachedKashmir') {
    actions = <Button block variant="accent" loading={setStatus.isPending} onClick={() => setStatus.mutate('Delivered')}><PackageCheck className="size-5" />{S.seller.markDelivered}</Button>
  }

  const submitReject = () => {
    if (!reason.trim()) return setFormError(S.seller.needReason)
    reject.mutate(reason.trim())
  }
  const submitDispatch = () => {
    if (!lr.trim()) return setFormError(S.seller.needLr)
    dispatch.mutate({ lr: lr.trim(), carrier: carrier.trim() })
  }

  return (
    <div>
      <PageHeader title={o.subOrderNumber} back="/seller/orders" />
      <div className="space-y-4 px-4 pb-8 pt-4">
        <Card className="px-4 py-2">
          <KeyValue label={S.orders.paymentStatus} value={<PaymentBadge status={o.paymentStatus} />} />
          <KeyValue label={S.orders.orderDate} value={formatDate(o.placedAt)} />
          <KeyValue label={S.seller.buyer} value={o.retailerName} />
          <KeyValue label={S.seller.deliverCity} value={o.deliveryCity} />
          <KeyValue label={S.seller.status} value={<OrderStatusBadge status={o.status} />} />
        </Card>

        <Card className="px-4 py-3">
          <ul className="divide-y divide-line">
            {o.items.map((i) => (
              <li key={i.id} className="flex justify-between gap-3 py-2 text-sm">
                <span className="min-w-0"><span className="block font-semibold">{i.title}</span><span className="text-mute">{i.colour} · {i.size} · {i.quantity} × {money(i.unitPrice)}</span></span>
                <span className="font-bold">{money(i.lineTotal)}</span>
              </li>
            ))}
          </ul>
          <hr className="stitch my-2" />
          <KeyValue label={S.cart.subTotal} value={money(o.subTotal)} />
          <KeyValue label={`${S.seller.commission} (${o.commissionRate}%)`} value={`− ${money(o.commissionAmount)}`} />
          <KeyValue strong label={S.seller.youGet} value={money(o.subTotal - o.commissionAmount)} />
        </Card>

        {o.shipment && (
          <Card className="bg-leaf-50 px-4 py-2">
            <KeyValue label={S.orders.carrier} value={o.shipment.carrierName || '—'} />
            <KeyValue label={S.orders.lr} value={o.shipment.lrNumber} />
          </Card>
        )}
        {o.rejectReason && <p className="rounded-xl bg-bad-bg p-3 text-sm font-semibold text-bad">{S.orders.rejectReason}: {o.rejectReason}</p>}

        {actions}

        <SectionTitle>{S.orders.timeline}</SectionTitle>
        <Card className="p-4"><StatusTimeline order={o} /></Card>
      </div>

      <Sheet open={dialog === 'reject'} onClose={() => setDialog(null)} title={S.seller.rejectTitle}>
        <div className="space-y-4">
          <p className="text-sm text-mute">{S.seller.rejectHint}</p>
          <Field label={S.orders.rejectReason} error={formError}>{(fid, inv) => <Textarea id={fid} invalid={inv} value={reason} onChange={(e) => setReason(e.target.value)} />}</Field>
          <Button block variant="danger" loading={reject.isPending} onClick={submitReject}>{S.seller.reject}</Button>
        </div>
      </Sheet>
      <Sheet open={dialog === 'dispatch'} onClose={() => setDialog(null)} title={S.seller.dispatchTitle}>
        <div className="space-y-4">
          <Field label={S.seller.lrNumber} error={formError}>{(fid, inv) => <Input id={fid} invalid={inv} value={lr} onChange={(e) => setLr(e.target.value)} autoCapitalize="characters" />}</Field>
          <Field label={S.seller.carrier}>{(fid) => <Input id={fid} value={carrier} onChange={(e) => setCarrier(e.target.value)} />}</Field>
          <Button block variant="accent" loading={dispatch.isPending} onClick={submitDispatch}>{S.seller.dispatch}</Button>
        </div>
      </Sheet>
    </div>
  )
}
