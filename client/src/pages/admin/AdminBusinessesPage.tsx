import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { CheckCircle2, Search } from 'lucide-react'
import { adminApi } from '@/api/endpoints'
import { qk } from '@/api/keys'
import type { AdminBusiness, VerificationStatus } from '@/api/types'
import { Badge, Chip, EmptyState, ErrorState, Skeleton } from '@/components/ui/misc'
import { fmt, S } from '@/i18n'
import { formatShortDate } from '@/lib/utils'

type Tab = Extract<VerificationStatus, 'Pending' | 'Approved' | 'Rejected'>
const typeLabel = (b: AdminBusiness) => (b.type === 'Manufacturer' ? S.reg.manufacturer : S.reg.retailer)

/** Approvals: a table on a laptop, cards on a phone. Every row opens with Tab and Enter. */
export default function AdminBusinessesPage() {
  const [tab, setTab] = useState<Tab>('Pending')
  const [q, setQ] = useState('')
  const nav = useNavigate()
  const list = useQuery({ queryKey: qk.adminBusinesses(tab), queryFn: () => adminApi.businesses(tab) })
  const pending = useQuery({ queryKey: qk.adminBusinesses('Pending'), queryFn: () => adminApi.businesses('Pending') })

  const needle = q.trim().toLowerCase()
  const rows = useMemo(
    () => (list.data ?? []).filter((b) => !needle || [b.displayName, b.legalName, b.gstin ?? '', b.cityName].some((x) => x.toLowerCase().includes(needle))),
    [list.data, needle],
  )
  const open = (b: AdminBusiness) => nav(`/admin/businesses/${b.id}`, { viewTransition: true })

  return (
    <div className="pb-4">
      <h1 className="mx-5 mt-6 text-[30px] font-semibold tracking-[-0.02em] lg:mx-2">{S.admin.approvals}</h1>
      <div className="mx-5 mt-3.5 flex flex-wrap items-center gap-2 lg:mx-2">
        {([['Pending', S.admin.pending], ['Approved', S.admin.approved], ['Rejected', S.admin.rejected]] as const).map(([t, l]) => (
          <Chip key={t} active={tab === t} onClick={() => setTab(t)}>
            {l}
            {t === 'Pending' && (pending.data?.length ?? 0) > 0 ? ` ${pending.data!.length}` : ''}
          </Chip>
        ))}
        <label className="relative ml-auto min-w-[220px] flex-1 lg:max-w-sm">
          <span className="sr-only">{S.ad.search}</span>
          <Search className="pointer-events-none absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-mute" aria-hidden />
          <input type="search" value={q} onChange={(e) => setQ(e.target.value)} placeholder={S.ad.search} className="min-h-11 w-full rounded-pill bg-soft pl-10 pr-4 text-sm outline-none placeholder:text-mute focus-visible:outline-2" />
        </label>
      </div>

      <div className="px-5 pt-4 lg:px-2">
        {list.isLoading ? (
          <div role="status" aria-label={S.app.loading} className="space-y-2.5">
            {Array.from({ length: 4 }, (_, i) => <Skeleton key={i} className="h-[72px] !rounded-card" />)}
          </div>
        ) : list.error && !list.data ? (
          <ErrorState error={list.error} onRetry={() => void list.refetch()} />
        ) : rows.length === 0 ? (
          <EmptyState icon={<CheckCircle2 className="size-8" />} title={S.admin.none} />
        ) : (
          <>
            <table className="hidden w-full border-separate border-spacing-0 overflow-hidden rounded-card border border-line bg-surface text-left text-sm md:table">
              <thead className="bg-soft text-xs text-mute">
                <tr>
                  {[S.ad.colBusiness, S.ad.colType, S.ad.colCity, S.ad.colGst, S.ad.colDocs, S.ad.colSent].map((h) => (
                    <th key={h} scope="col" className="px-4 py-3 font-medium">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {rows.map((b) => (
                  <tr key={b.id} onClick={() => open(b)} className="cursor-pointer hover:bg-soft/60">
                    <td className="border-t border-line px-4 py-3">
                      <Link to={`/admin/businesses/${b.id}`} className="block min-h-11 py-2.5 font-medium underline-offset-2 hover:underline">{b.displayName}</Link>
                      <span className="block text-xs text-mute">{b.legalName}</span>
                    </td>
                    <td className="border-t border-line px-4 py-3">{typeLabel(b)}</td>
                    <td className="border-t border-line px-4 py-3">{b.cityName}</td>
                    <td className="border-t border-line px-4 py-3 tabular-nums">{b.gstin ?? '—'}</td>
                    <td className="border-t border-line px-4 py-3">{b.documents.length}</td>
                    <td className="border-t border-line px-4 py-3 text-mute">{formatShortDate(b.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>

            <ul className="space-y-2.5 md:hidden">
              {rows.map((b) => (
                <li key={b.id}>
                  <Link to={`/admin/businesses/${b.id}`} viewTransition className="press block rounded-card border border-line bg-surface p-4">
                    <span className="flex items-start justify-between gap-2">
                      <span className="min-w-0 truncate font-semibold">{b.displayName}</span>
                      <Badge tone={b.type === 'Manufacturer' ? 'neutral' : 'good'}>{typeLabel(b)}</Badge>
                    </span>
                    <span className="mt-0.5 block text-[13px] text-mute">{b.legalName}</span>
                    <span className="mt-1.5 block text-[13px] text-mute">{b.gstin ?? '—'} · {b.cityName} · {fmt(S.ad.docsN, { n: b.documents.length })} · {fmt(S.ad.sent, { d: formatShortDate(b.createdAt) })}</span>
                  </Link>
                </li>
              ))}
            </ul>
          </>
        )}
      </div>
    </div>
  )
}
