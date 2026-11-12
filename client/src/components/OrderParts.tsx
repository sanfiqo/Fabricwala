import { Check, X } from 'lucide-react'
import type { PaymentStatus, SellerOrder, SellerOrderStatus } from '@/api/types'
import { Badge, type Tone } from '@/components/ui/misc'
import { S, label } from '@/i18n'
import { cn, formatDateTime } from '@/lib/utils'

const tone: Record<SellerOrderStatus, Tone> = {
  PendingAcceptance: 'warn', Accepted: 'neutral', Dispatched: 'neutral', ReachedKashmir: 'good', Delivered: 'good', Rejected: 'bad', Cancelled: 'bad',
}
export function OrderStatusBadge({ status }: { status: SellerOrderStatus }) {
  return <Badge tone={tone[status]}>{label(S.orderStatus, status)}</Badge>
}
export function PaymentBadge({ status }: { status: PaymentStatus }) {
  return <Badge tone={status === 'Paid' ? 'good' : status === 'Pending' ? 'warn' : 'neutral'}>{label(S.payment, status)}</Badge>
}

const happyPath: SellerOrderStatus[] = ['PendingAcceptance', 'Accepted', 'Dispatched', 'ReachedKashmir', 'Delivered']

/** Vertical timeline built from the status history. Future steps are shown greyed. */
export function StatusTimeline({ order }: { order: Pick<SellerOrder, 'history' | 'status'> }) {
  const failed = order.status === 'Rejected' || order.status === 'Cancelled'
  const path: SellerOrderStatus[] = failed ? ['PendingAcceptance', order.status] : happyPath
  const at = new Map(order.history.map((h) => [h.status, h]))
  return (
    <ol className="relative ml-1">
      {path.map((st, i) => {
        const h = at.get(st)
        const done = !!h
        const bad = st === 'Rejected' || st === 'Cancelled'
        return (
          <li key={st} className="relative flex gap-3 pb-5 last:pb-0">
            {i < path.length - 1 && <span className={cn('absolute left-[13px] top-7 h-[calc(100%-1.75rem)] border-l-2', at.has(path[i + 1]) ? 'border-leaf-600' : 'border-dashed border-line')} />}
            <span className={cn('z-10 flex size-7 shrink-0 items-center justify-center rounded-full border-2', bad ? 'border-bad bg-bad text-white' : done ? 'border-leaf-600 bg-leaf-600 text-white' : 'border-line bg-white')}>
              {bad ? <X className="size-4" /> : done ? <Check className="size-4" /> : null}
            </span>
            <div className="-mt-0.5">
              <p className={cn('font-bold', done ? 'text-navy-900' : 'text-mute')}>{label(S.orderStatus, st)}</p>
              {h && <p className="text-xs text-mute">{formatDateTime(h.changedAt)}{h.note ? ` · ${h.note}` : ''}</p>}
            </div>
          </li>
        )
      })}
    </ol>
  )
}
