import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { MapPin } from 'lucide-react'
import type { RfqStatus, RfqSummary } from '@/api/types'
import { Badge, type Tone } from '@/components/ui/misc'
import { fmt, S } from '@/i18n'
import { formatShortDate } from '@/lib/utils'

const TONE: Record<RfqStatus, Tone> = { Open: 'good', Awarded: 'neutral', Expired: 'warn', Cancelled: 'warn', Closed: 'warn' }

export const unitLabel = (u: string) => S.units[u as keyof typeof S.units] ?? u

export function RfqStatusBadge({ status }: { status: RfqStatus }) {
  return <Badge tone={TONE[status]}>{S.rfq.status[status]}</Badge>
}

/** One request in a list (retailer and manufacturer). `footer` is for the manufacturer's own quotation line. */
export function RfqCard({ r, to, footer }: { r: RfqSummary; to: string; footer?: ReactNode }) {
  return (
    <Link to={to} viewTransition className="press block rounded-card border border-line bg-surface p-4">
      <div className="flex items-start justify-between gap-3">
        <h3 className="min-w-0 flex-1 break-words text-[15px] font-semibold leading-5">{r.title}</h3>
        <RfqStatusBadge status={r.status} />
      </div>
      <p className="mt-1 text-sm text-ink">{fmt(S.rfq.wanted, { qty: r.quantity, unit: unitLabel(r.unit) })}</p>
      <p className="mt-1 flex items-center gap-1.5 text-[13px] text-mute">
        <MapPin className="size-3.5 shrink-0" aria-hidden />
        <span className="truncate">{r.deliveryCity}</span>
        <span aria-hidden>·</span>
        <span className="shrink-0">{fmt(S.rfq.quotesCount, { n: r.quotationCount })}</span>
      </p>
      <p className="mt-0.5 text-[13px] text-mute">{fmt(S.rfq.openUntil, { date: formatShortDate(r.expiresAt) })}</p>
      {footer && <div className="mt-2 border-t border-line pt-2 text-sm">{footer}</div>}
    </Link>
  )
}
