import { Check, X } from 'lucide-react'
import type { CSSProperties } from 'react'
import type { PaymentStatus, SellerOrder, SellerOrderStatus } from '@/api/types'
import { Badge, type Tone } from '@/components/ui/misc'
import { fmt, S, label } from '@/i18n'
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

/** The shopkeeper can confirm delivery once the parcel is on the way (real) or has reached the city (sample data). */
export const canConfirm = (s: SellerOrderStatus) => s === 'ReachedKashmir' || s === 'Dispatched'

/** Placed, Accepted, Shipped, Reached your city, Delivered. */
export const ORDER_STEPS: SellerOrderStatus[] = ['PendingAcceptance', 'Accepted', 'Dispatched', 'ReachedKashmir', 'Delivered']

/** How many steps are done (1 = placed). Rejected and cancelled orders stop after "placed". */
export function stepsDone(status: SellerOrderStatus): number {
  const i = ORDER_STEPS.indexOf(status)
  return i < 0 ? 1 : i + 1
}

/** Thin segmented bar. It fills step by step when it appears. */
export function StepBar({ status }: { status: SellerOrderStatus }) {
  const done = stepsDone(status)
  const bad = status === 'Rejected' || status === 'Cancelled'
  return (
    <div role="img" aria-label={fmt(S.orders.stepsDone, { n: done, total: ORDER_STEPS.length })} className="grid gap-1" style={{ gridTemplateColumns: `repeat(${ORDER_STEPS.length}, minmax(0, 1fr))` }}>
      {ORDER_STEPS.map((st, i) => (
        <span key={st} className="h-1.5 overflow-hidden rounded-full bg-line">
          {i < done && <span className={cn('fill-x block h-full', bad ? 'bg-error' : 'bg-ink')} style={{ '--i': i } as CSSProperties} />}
        </span>
      ))}
    </div>
  )
}

/** Vertical timeline from the status history. Done steps are solid, the current step is ringed and pulses twice, future steps are grey. */
export function StatusTimeline({ order }: { order: Pick<SellerOrder, 'history' | 'status'> }) {
  const failed = order.status === 'Rejected' || order.status === 'Cancelled'
  const path: SellerOrderStatus[] = failed ? ['PendingAcceptance', order.status] : ORDER_STEPS
  const at = new Map(order.history.map((h) => [h.status, h]))
  const current = failed ? order.status : (ORDER_STEPS.find((st) => !at.has(st)) ?? 'Delivered')
  return (
    <ol>
      {path.map((st, i) => {
        const h = at.get(st)
        const done = !!h
        const bad = st === 'Rejected' || st === 'Cancelled'
        const isCurrent = !failed && !done && st === current
        const last = i === path.length - 1
        return (
          <li key={st} className="flex gap-3.5">
            <span className="flex w-6 shrink-0 flex-col items-center">
              <span
                className={cn(
                  'flex size-6 shrink-0 items-center justify-center rounded-full',
                  bad ? 'bg-error text-white' : done ? 'bg-ink text-background' : isCurrent ? 'pulse-twice border-[5px] border-ink bg-surface' : 'border-2 border-[#B9B5AC] bg-surface',
                )}
                aria-hidden
              >
                {bad ? <X className="size-3.5" /> : done ? <Check className="size-3.5" /> : null}
              </span>
              {!last && <span className={cn('my-1 w-0.5 flex-1', at.has(path[i + 1]) ? 'bg-ink' : 'bg-line')} />}
            </span>
            <span className={cn('flex min-w-0 flex-1 flex-col pb-5', last && 'pb-0')}>
              <span className={cn('text-[15px]', done || isCurrent ? 'font-medium' : 'text-mute')}>{label(S.orders.steps, st)}</span>
              <span className="text-[13px] text-mute">
                {h ? formatDateTime(h.changedAt) + (h.note ? ` · ${h.note}` : '') : st === 'Delivered' ? S.orders.afterDelivered : ''}
              </span>
            </span>
          </li>
        )
      })}
    </ol>
  )
}
