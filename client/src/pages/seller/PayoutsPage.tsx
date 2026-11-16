import { useQuery } from '@tanstack/react-query'
import { Download, Wallet } from 'lucide-react'
import { payoutApi } from '@/api/endpoints'
import { qk } from '@/api/keys'
import type { PayoutRow } from '@/api/types'
import { PullToRefresh } from '@/components/PullToRefresh'
import { Badge, EmptyState, ErrorState, Skeleton } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { fmt, S } from '@/i18n'
import { formatShortDate, money } from '@/lib/utils'

const tone = { Paid: 'good', Scheduled: 'neutral', OnHold: 'warn' } as const

function Row({ r }: { r: PayoutRow }) {
  const label = r.status === 'Paid' ? S.po.paid : r.status === 'Scheduled' ? (r.date ? fmt(S.po.scheduled, { d: formatShortDate(r.date) }) : S.po.paid) : S.po.hold
  return (
    <li className="flex items-center gap-3 border-t border-line px-4 py-3.5 first:border-t-0">
      <span className="flex min-w-0 flex-1 flex-col gap-0.5">
        <span className="truncate font-medium">{r.buyer || r.orderNumber}</span>
        <span className="text-[13px] text-mute">
          {r.buyer ? `${r.orderNumber} · ` : ''}{r.deliveredAt ? fmt(S.po.delivered, { d: formatShortDate(r.deliveredAt) }) : fmt(S.po.placed, { d: formatShortDate(r.placedAt) })}
        </span>
      </span>
      <span className="flex shrink-0 flex-col items-end gap-1">
        <span className="font-semibold tabular-nums">{money(r.amount)}</span>
        <Badge tone={tone[r.status]}>{label}</Badge>
      </span>
    </li>
  )
}

/** Payouts: what is coming, what is waiting for delivery, and each order. The seller is paid after delivery is confirmed, or after 7 days. */
export default function PayoutsPage() {
  const q = useQuery({ queryKey: qk.payouts, queryFn: payoutApi.list })
  const toast = useToast()
  const d = q.data

  const download = async () => {
    try {
      const text = await payoutApi.csv()
      const url = URL.createObjectURL(new Blob([text], { type: 'text/csv' }))
      const a = document.createElement('a')
      a.href = url
      a.download = 'payouts.csv'
      a.click()
      URL.revokeObjectURL(url)
    } catch (e) {
      toast.error((e as Error).message)
    }
  }

  return (
    <div className="pb-4">
      <PullToRefresh onRefresh={() => q.refetch()} />
      <h1 className="mx-5 mt-7 text-[30px] font-semibold tracking-[-0.02em]">{S.po.title}</h1>

      {q.isLoading ? (
        <div role="status" aria-label={S.app.loading} className="space-y-3 px-5 pt-4">
          <Skeleton className="h-32 !rounded-[22px]" />
          <div className="grid grid-cols-2 gap-3"><Skeleton className="h-20 !rounded-card" /><Skeleton className="h-20 !rounded-card" /></div>
          <Skeleton className="h-48 !rounded-card" />
        </div>
      ) : q.error && !d ? (
        <ErrorState error={q.error} onRetry={() => void q.refetch()} />
      ) : d ? (
        <>
          <div className="mx-5 mt-4 rounded-[22px] bg-ink p-[18px] text-background">
            <p className="text-[13px] text-line">{d.next.date ? fmt(S.po.nextOn, { d: formatShortDate(d.next.date) }) : S.po.next}</p>
            <p className="mt-1 text-[34px] font-semibold leading-tight tracking-[-0.02em] tabular-nums">{money(d.next.amount)}</p>
            <p className="mt-1 text-[13px] text-line">{d.next.amount > 0 ? S.po.toBank : S.po.noNext}</p>
          </div>
          <div className="mx-5 mt-3 grid grid-cols-2 gap-3">
            <div className="rounded-card bg-soft px-4 py-3.5">
              <p className="text-xs text-mute">{S.po.onHold}</p>
              <p className="text-xl font-semibold tabular-nums">{money(d.onHold)}</p>
              <p className="text-xs text-mute">{S.po.onHoldSub}</p>
            </div>
            <div className="rounded-card bg-soft px-4 py-3.5">
              <p className="text-xs text-mute">{S.po.paidMonth}</p>
              <p className="text-xl font-semibold tabular-nums">{money(d.paidThisMonth)}</p>
            </div>
          </div>

          <div className="mx-5 mb-2 mt-5 flex items-center justify-between gap-3">
            <h2 className="text-[15px] font-semibold">{S.po.byOrder}</h2>
            {d.rows.length > 0 && (
              <button type="button" onClick={() => void download()} className="press flex min-h-11 items-center gap-1.5 rounded-full px-2 text-sm font-medium">
                <Download className="size-4" aria-hidden />
                {S.po.csv}
              </button>
            )}
          </div>
          {d.rows.length === 0 ? (
            <EmptyState icon={<Wallet className="size-8" />} title={S.po.none} sub={S.po.noneSub} />
          ) : (
            <ul className="mx-5 overflow-hidden rounded-card border border-line bg-surface">{d.rows.map((r) => <Row key={r.id} r={r} />)}</ul>
          )}
          <p className="mx-5 mt-3.5 text-[13px] leading-relaxed text-mute">{S.po.rule}</p>
        </>
      ) : null}
    </div>
  )
}
