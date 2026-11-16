import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Bell, Package } from 'lucide-react'
import { sellerHomeApi } from '@/api/endpoints'
import { qk } from '@/api/keys'
import { useMe } from '@/auth'
import { roundIcon } from '@/components/HeaderButtons'
import { PullToRefresh } from '@/components/PullToRefresh'
import { ErrorState, Skeleton } from '@/components/ui/misc'
import { useUnreadCount } from '@/hooks'
import { useCountdown } from '@/lib/useCountdown'
import { fmt, S } from '@/i18n'
import { formatShortDate, money } from '@/lib/utils'

const initials = (name: string) =>
  name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w.charAt(0).toUpperCase())
    .join('') || '·'

function Stat({ label, value, sub, good }: { label: string; value: string; sub: string; good?: boolean }) {
  return (
    <div className="flex min-w-0 flex-col gap-0.5 bg-surface p-4">
      <span className="text-xs text-mute">{label}</span>
      <span className="break-words text-xl font-semibold tabular-nums">{value}</span>
      <span className={good ? 'text-xs text-success' : 'text-xs text-mute'}>{sub}</span>
    </div>
  )
}

/** Seller home: what needs doing today, money, a 6 month chart and low stock. All numbers come from the server. */
export default function SellerHome() {
  const { data: me } = useMe()
  const unread = useUnreadCount()
  const q = useQuery({ queryKey: qk.sellerDashboard, queryFn: sellerHomeApi.dashboard })
  const d = q.data
  const left = useCountdown(d?.newOrders.acceptBy)
  const name = me?.business?.displayName ?? me?.fullName ?? ''
  const max = Math.max(1, ...(d?.salesByMonth.map((m) => m.amount) ?? [1]))

  return (
    <div className="pb-4">
      <PullToRefresh onRefresh={() => q.refetch()} />
      <header className="flex items-center justify-between px-5 pt-[22px]">
        <Link to="/seller/account" viewTransition aria-label={S.sh.menu} className="press flex items-center gap-2.5">
          <span className="flex size-[46px] shrink-0 items-center justify-center rounded-full bg-ink text-[15px] font-medium text-background" aria-hidden>
            {initials(name)}
          </span>
          <span className="block max-w-[190px] truncate font-medium">{name}</span>
        </Link>
        <Link
          to="/seller/notifications"
          viewTransition
          aria-label={unread > 0 ? `${S.notif.bell}, ${fmt(S.notif.newCount, { n: unread })}` : S.notif.bell}
          className={roundIcon}
        >
          <Bell className="size-5" aria-hidden />
          {unread > 0 && <span className="absolute right-[11px] top-2.5 size-2 rounded-full border-2 border-surface bg-dot" aria-hidden />}
        </Link>
      </header>

      {q.isLoading ? (
        <div role="status" aria-label={S.app.loading} className="space-y-4 px-5 pt-6">
          <Skeleton className="h-16 w-3/4" />
          <Skeleton className="h-20 !rounded-card" />
          <Skeleton className="h-44 !rounded-card" />
          <Skeleton className="h-40 !rounded-card" />
        </div>
      ) : q.error && !d ? (
        <ErrorState error={q.error} onRetry={() => void q.refetch()} />
      ) : d ? (
        <>
          <h1 className="mx-5 mt-5 text-balance text-[30px] font-semibold leading-[1.15] tracking-[-0.02em]">
            {d.newOrders.count === 0 ? S.sh.none : d.newOrders.count === 1 ? S.sh.waitingOne : fmt(S.sh.waiting, { n: d.newOrders.count })}
          </h1>

          {d.newOrders.count > 0 ? (
            <Link to="/seller/orders" viewTransition className="press mx-5 mt-4 flex items-center gap-3 rounded-card bg-ink p-4 text-background">
              <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                <span className="text-[13px] text-line">
                  {money(d.newOrders.value)}
                  {left.text ? ` · ${fmt(S.sh.acceptBy, { t: left.text })}` : left.over ? ` · ${S.so.timeUp}` : ''}
                </span>
                <span className="break-words text-base font-medium">{d.newOrders.cities.join(', ')}</span>
              </span>
              <span className="flex min-h-11 shrink-0 items-center rounded-pill bg-background px-4 text-sm font-medium text-ink">{S.sh.seeOrders}</span>
            </Link>
          ) : (
            <p className="mx-5 mt-2 text-mute">{S.sh.noneSub}</p>
          )}

          <div className="mx-5 mt-4 grid grid-cols-2 gap-px overflow-hidden rounded-card border border-line bg-line">
            <Stat
              label={S.sh.salesMonth}
              value={money(d.salesThisMonth)}
              sub={d.salesChangePct === null ? ' ' : `${d.salesChangePct >= 0 ? '↑' : '↓'} ${fmt(S.sh.vsLast, { p: Math.abs(d.salesChangePct) })}`}
              good={(d.salesChangePct ?? 0) >= 0}
            />
            <Stat label={S.sh.nextPayout} value={money(d.payoutDue)} sub={d.payoutDue > 0 && d.nextPayoutDate ? fmt(S.sh.payoutOn, { d: formatShortDate(d.nextPayoutDate) }) : S.sh.payoutNone} />
            <Stat label={S.sh.toPack} value={d.toPack === 1 ? S.sh.ordersOne : fmt(S.sh.ordersN, { n: d.toPack })} sub={S.sh.shipSoon} />
            <Stat label={S.sh.rating} value={d.rating ? `★ ${d.rating}` : '—'} sub={d.rating ? fmt(S.sh.fromShops, { n: d.ratingCount }) : S.sh.noRating} />
          </div>

          <section aria-label={S.sh.chart} className="mx-5 mt-4 rounded-card border border-line bg-surface p-4">
            <div className="flex items-baseline justify-between">
              <h2 className="text-[15px] font-semibold">{S.sh.chart}</h2>
              <span className="text-[13px] text-mute">{S.sh.lakh}</span>
            </div>
            <div
              role="img"
              aria-label={d.salesByMonth.map((m) => `${m.month} ${(m.amount / 100000).toFixed(1)}`).join(', ')}
              className="mt-3.5 grid h-[110px] items-end gap-2.5"
              style={{ gridTemplateColumns: `repeat(${d.salesByMonth.length}, minmax(0, 1fr))` }}
            >
              {d.salesByMonth.map((m, i) => (
                <span key={m.month + i} className={i === d.salesByMonth.length - 1 ? 'rounded-lg bg-ink' : 'rounded-lg bg-line'} style={{ height: `${Math.max(6, (m.amount / max) * 100)}%` }} />
              ))}
            </div>
            <div className="mt-1.5 grid gap-2.5 text-center text-xs text-mute" style={{ gridTemplateColumns: `repeat(${d.salesByMonth.length}, minmax(0, 1fr))` }} aria-hidden>
              {d.salesByMonth.map((m, i) => (
                <span key={m.month + i} className={i === d.salesByMonth.length - 1 ? 'font-medium text-ink' : ''}>
                  {m.month}
                </span>
              ))}
            </div>
          </section>

          {d.lowStock[0] && (
            <Link to="/seller/products" viewTransition className="press mx-5 mt-4 flex items-center gap-3 rounded-2xl bg-saving-bg p-3.5 text-saving">
              <span className="flex-1 text-sm leading-snug">
                <b className="font-semibold">{S.sh.lowStock}</b> {d.lowStock[0].title}, {d.lowStock[0].variant}. {fmt(S.product.inStock, { n: d.lowStock[0].qty })}.
              </span>
              <span className="shrink-0 text-sm font-semibold underline">{S.sh.update}</span>
            </Link>
          )}

          {d.topProducts.length > 0 && (
            <section aria-label={S.sh.top} className="mx-5 mt-4 overflow-hidden rounded-card border border-line bg-surface">
              <h2 className="px-4 pt-3.5 text-[15px] font-semibold">{S.sh.top}</h2>
              <ul>
                {d.topProducts.map((t) => (
                  <li key={t.productId} className="flex items-center gap-3 border-t border-line px-4 py-3 first:border-t-0">
                    <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-soft text-mute" aria-hidden>
                      <Package className="size-5" />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-[15px] font-medium">{t.title}</span>
                      <span className="block text-[13px] text-mute">{fmt(S.orders.pieces, { n: t.pieces })}</span>
                    </span>
                    <span className="shrink-0 text-[15px] font-semibold tabular-nums">{money(t.amount)}</span>
                  </li>
                ))}
              </ul>
            </section>
          )}
        </>
      ) : null}
    </div>
  )
}
