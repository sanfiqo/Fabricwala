import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { CheckCircle2, ChevronRight, UserCheck } from 'lucide-react'
import { adminApi } from '@/api/endpoints'
import { qk } from '@/api/keys'
import { Chip, ErrorState, Skeleton } from '@/components/ui/misc'
import { fmt, S } from '@/i18n'
import { money } from '@/lib/utils'

const WINDOWS: Array<[number, () => string]> = [
  [1, () => S.ad.today],
  [7, () => S.ad.days7],
  [30, () => S.ad.days30],
]

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="bg-surface p-4">
      <p className="text-xs text-mute">{label}</p>
      <p className="mt-0.5 break-words text-2xl font-semibold tabular-nums">{value}</p>
    </div>
  )
}

/** Admin home: what needs a decision, and money and activity for Today, 7 days or 30 days. Works on a phone and on a laptop. */
export default function AdminHome() {
  const [days, setDays] = useState(7)
  const q = useQuery({ queryKey: [...qk.adminDashboard, days], queryFn: () => adminApi.dashboard(days) })
  const d = q.data

  return (
    <div className="pb-4">
      <h1 className="mx-5 mt-6 text-balance text-[30px] font-semibold leading-[1.15] tracking-[-0.02em] lg:mx-2">
        {!d ? S.ad.homeTitle : d.pendingApprovals === 0 ? S.ad.allClear : d.pendingApprovals === 1 ? S.ad.waitingOne : fmt(S.ad.waitingN, { n: d.pendingApprovals })}
      </h1>

      {d && d.pendingApprovals > 0 && (
        <Link to="/admin/businesses" viewTransition className="press mx-5 mt-4 flex items-center gap-3 rounded-card bg-ink p-4 text-background lg:mx-2">
          <UserCheck className="size-6 shrink-0" aria-hidden />
          <span className="flex-1 text-base font-medium">{S.ad.openApprovals}</span>
          <ChevronRight className="size-5" aria-hidden />
        </Link>
      )}

      <nav aria-label={S.ad.homeTitle} className="mx-5 mt-4 flex gap-2 lg:mx-2">
        {WINDOWS.map(([n, label]) => (
          <Chip key={n} active={days === n} onClick={() => setDays(n)}>{label()}</Chip>
        ))}
      </nav>

      {q.isLoading ? (
        <div role="status" aria-label={S.app.loading} className="space-y-3 px-5 pt-3 lg:px-2">
          <Skeleton className="h-40 !rounded-card" />
          <Skeleton className="h-40 !rounded-card" />
        </div>
      ) : q.error && !d ? (
        <ErrorState error={q.error} onRetry={() => void q.refetch()} />
      ) : d ? (
        <>
          <div className="mx-5 mt-3 grid grid-cols-2 gap-px overflow-hidden rounded-card border border-line bg-line lg:mx-2 lg:grid-cols-4">
            <Stat label={S.ad.sales} value={money(d.gmv)} />
            <Stat label={S.ad.orders} value={String(d.orders)} />
            <Stat label={S.ad.fee} value={money(d.commission)} />
            <Stat label={S.ad.newBiz} value={String(d.newBusinesses)} />
          </div>

          <section aria-label={S.ad.decide} className="mx-5 mt-4 overflow-hidden rounded-card border border-line bg-surface lg:mx-2">
            <h2 className="px-4 pb-1.5 pt-3.5 text-[15px] font-semibold">{S.ad.decide}</h2>
            {d.decisions.length === 0 ? (
              <p className="flex items-center gap-2 px-4 pb-4 text-mute">
                <CheckCircle2 className="size-5" aria-hidden />
                {S.ad.noDecide}
              </p>
            ) : (
              <ul>
                {d.decisions.map((x) => (
                  <li key={x.link}>
                    <Link to={x.link} viewTransition className="press flex min-h-[60px] items-center gap-3 border-t border-line px-4 py-3">
                      <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-soft" aria-hidden>
                        <UserCheck className="size-5" />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate font-medium">{x.title}</span>
                        <span className="block truncate text-[13px] text-mute">{x.sub}</span>
                      </span>
                      <ChevronRight className="size-4 text-mute" aria-hidden />
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      ) : null}
    </div>
  )
}
