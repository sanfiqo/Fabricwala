import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ClipboardList, PackageCheck } from 'lucide-react'
import { orderApi } from '@/api/endpoints'
import { OrderStatusBadge, PaymentBadge, StatusTimeline } from '@/components/OrderParts'
import { Button } from '@/components/ui/button'
import { Card, EmptyState, ErrorState, KeyValue, PageHeader, PageLoading, SectionTitle } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { fmt, S } from '@/i18n'
import { formatDate, money } from '@/lib/utils'

export function OrdersPage() {
  const orders = useQuery({ queryKey: ['orders'], queryFn: orderApi.list })
  if (orders.isLoading) return <PageLoading />
  if (orders.error) return <ErrorState error={orders.error} onRetry={() => void orders.refetch()} />
  const list = orders.data ?? []
  return (
    <div>
      <PageHeader title={S.orders.title} />
      {list.length === 0 ? (
        <EmptyState icon={<ClipboardList className="size-8" />} title={S.orders.empty} sub={S.orders.emptySub} />
      ) : (
        <div className="space-y-3 px-4 pt-4">
          {list.map((o) => (
            <Link key={o.id} to={`/shop/orders/${o.id}`} className="block">
              <Card className="p-4 active:bg-navy-50">
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <p className="font-extrabold text-navy-900">{fmt(S.orders.number, { n: o.orderNumber })}</p>
                    <p className="text-sm text-mute">{fmt(S.orders.placedOn, { d: formatDate(o.placedAt) })}</p>
                  </div>
                  <p className="text-lg font-extrabold text-navy-900">{money(o.grandTotal)}</p>
                </div>
                <div className="mt-3 flex flex-wrap gap-2">
                  {o.sellerOrders.map((so) => <OrderStatusBadge key={so.id} status={so.status} />)}
                  <PaymentBadge status={o.paymentStatus} />
                </div>
                <p className="mt-2 text-sm text-mute">{o.sellerOrders.map((s) => s.manufacturerName).join(', ')}</p>
              </Card>
            </Link>
          ))}
        </div>
      )}
    </div>
  )
}

export function OrderDetailPage() {
  const { id = '' } = useParams()
  const qc = useQueryClient()
  const toast = useToast()
  const order = useQuery({ queryKey: ['order', id], queryFn: () => orderApi.get(Number(id)) })
  const confirm = useMutation({
    mutationFn: () => orderApi.confirmDelivery(Number(id)),
    onSuccess: async (o) => {
      qc.setQueryData(['order', id], o)
      await qc.invalidateQueries({ queryKey: ['orders'] })
      toast.success(S.orders.confirmed)
    },
    onError: (e) => toast.error(e.message),
  })

  if (order.isLoading) return <PageLoading />
  if (order.error || !order.data) return <ErrorState error={order.error ?? new Error(S.orders.notFound)} onRetry={() => void order.refetch()} />
  const o = order.data

  return (
    <div>
      <PageHeader title={fmt(S.orders.number, { n: o.orderNumber })} back="/shop/orders" />
      <div className="space-y-4 px-4 pb-6 pt-4">
        <Card className="px-4 py-2">
          <KeyValue label={S.orders.orderDate} value={formatDate(o.placedAt)} />
          <KeyValue label={S.orders.paymentStatus} value={<PaymentBadge status={o.paymentStatus} />} />
          <KeyValue label={S.checkout.deliverTo} value={`${o.deliveryAddress.cityName} ${o.deliveryAddress.pincode}`} />
          <KeyValue strong label={S.orders.total} value={money(o.grandTotal)} />
        </Card>

        {o.sellerOrders.map((so) => (
          <Card key={so.id} className="overflow-hidden">
            <div className="flex items-center justify-between gap-2 bg-navy-900 px-4 py-2.5 text-white">
              <p className="min-w-0 truncate font-bold">{so.manufacturerName}</p>
              <OrderStatusBadge status={so.status} />
            </div>
            <div className="px-4 py-3">
              <ul className="divide-y divide-line">
                {so.items.map((i) => (
                  <li key={i.id} className="flex justify-between gap-3 py-2 text-sm">
                    <span className="min-w-0"><span className="block font-semibold">{i.title}</span><span className="text-mute">{i.colour} · {i.size} · {i.quantity} × {money(i.unitPrice)}</span></span>
                    <span className="font-bold">{money(i.lineTotal)}</span>
                  </li>
                ))}
              </ul>
              <hr className="stitch my-2" />
              <KeyValue label={S.cart.subTotal} value={money(so.subTotal)} />
              <KeyValue label={S.cart.shipping} value={money(so.shippingAmount)} />
              <KeyValue label={S.cart.tax} value={money(so.taxAmount)} />
              <KeyValue strong label={S.cart.total} value={money(so.total)} />

              {so.shipment && (
                <div className="mt-3 rounded-xl bg-leaf-50 p-3 text-sm">
                  <KeyValue label={S.orders.carrier} value={so.shipment.carrierName || '—'} />
                  <KeyValue label={S.orders.lr} value={so.shipment.lrNumber} />
                </div>
              )}
              {so.rejectReason && <p className="mt-3 rounded-xl bg-bad-bg p-3 text-sm font-semibold text-bad">{S.orders.rejectReason}: {so.rejectReason}</p>}
            </div>

            <div className="border-t border-line px-4 py-4">
              <SectionTitle>{S.orders.timeline}</SectionTitle>
              <StatusTimeline order={so} />
            </div>
            {so.status === 'ReachedKashmir' && (
              <div className="border-t border-line p-4">
                <Button block variant="accent" loading={confirm.isPending} onClick={() => confirm.mutate()}>
                  <PackageCheck className="size-5" />
                  {S.orders.confirmDelivery}
                </Button>
              </div>
            )}
          </Card>
        ))}
      </div>
    </div>
  )
}
