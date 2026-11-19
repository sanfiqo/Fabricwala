import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { FileQuestion, Phone } from 'lucide-react'
import { rfqApi } from '@/api/endpoints'
import { ApiError } from '@/api/client'
import { qk } from '@/api/keys'
import type { Quotation, RfqDetail } from '@/api/types'
import { RfqStatusBadge, unitLabel } from '@/components/RfqParts'
import { Button } from '@/components/ui/button'
import { Badge, EmptyState, ErrorState, LinkButton, PageHeader, Skeleton, type Tone } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { fmt, S } from '@/i18n'
import { cn, formatShortDate, money } from '@/lib/utils'

const QTONE: Record<Quotation['status'], Tone> = { Submitted: 'neutral', Accepted: 'good', Rejected: 'warn', Withdrawn: 'warn' }

function QuotationCard({ q, unit, lowest, canAccept, sure, busy, onAccept }: { q: Quotation; unit: string; lowest: boolean; canAccept: boolean; sure: boolean; busy: boolean; onAccept: () => void }) {
  return (
    <li className={cn('rounded-card border bg-surface p-4', q.status === 'Accepted' ? 'border-brand' : 'border-line')}>
      <div className="flex items-start justify-between gap-3">
        <Link to={`/shop/store/${q.sellerBusinessId}`} viewTransition className="min-w-0 break-words font-medium underline decoration-line decoration-2 underline-offset-4">
          {q.sellerName}
        </Link>
        <span className="flex shrink-0 flex-wrap justify-end gap-1.5">
          {lowest && <Badge tone="good">{S.rfq.lowest}</Badge>}
          <Badge tone={QTONE[q.status]}>{S.rfq.quoteStatus[q.status]}</Badge>
        </span>
      </div>
      <p className="mt-2 text-xl font-semibold tabular-nums">{fmt(S.rfq.perUnit, { price: money(q.pricePerUnit), unit: unitLabel(unit) })}</p>
      <p className="text-sm text-mute">
        {q.quantity} {unitLabel(unit)} · {fmt(S.rfq.total, { total: money(q.total) })} · {fmt(S.rfq.leadDays, { n: q.leadTimeDays })}
      </p>
      {q.note && <p className="mt-2 break-words text-sm">{q.note}</p>}
      {q.contact && (
        <div className="mt-3 rounded-2xl bg-brand-soft p-3">
          <p className="text-sm font-medium">{S.rfq.contactTitle}</p>
          <p className="text-[13px] text-mute">{S.rfq.contactSub}</p>
          <a href={`tel:${q.contact.phone}`} className="press mt-2 inline-flex min-h-11 items-center gap-2 rounded-pill bg-brand px-4 text-sm font-medium text-white">
            <Phone className="size-4" aria-hidden />
            {fmt(S.rfq.call, { phone: q.contact.phone })}
          </a>
        </div>
      )}
      {canAccept && (
        <Button className="mt-3" block variant={sure ? 'primary' : 'soft'} loading={busy} onClick={onAccept}>
          {sure ? S.rfq.acceptSure : S.rfq.accept}
        </Button>
      )}
    </li>
  )
}

/** One request with every quotation, cheapest first. Accepting one is final, so it asks for a second tap. */
export default function RfqDetailPage() {
  const { id } = useParams()
  const rid = Number(id)
  const qc = useQueryClient()
  const toast = useToast()
  const [sure, setSure] = useState<number | 'cancel' | null>(null)
  const q = useQuery({ queryKey: qk.rfq(rid), queryFn: () => rfqApi.get(rid), enabled: Number.isInteger(rid) && rid > 0 })

  const done = (d: RfqDetail, msg: string) => {
    qc.setQueryData(qk.rfq(rid), d)
    void qc.invalidateQueries({ queryKey: qk.rfqs() })
    toast.success(msg)
    setSure(null)
  }
  const fail = (e: Error) => {
    toast.error(e.message)
    setSure(null)
  }
  const accept = useMutation({ mutationFn: (quotationId: number) => rfqApi.accept(rid, quotationId), onSuccess: (d) => done(d, S.rfq.accepted), onError: fail })
  const cancel = useMutation({ mutationFn: () => rfqApi.cancel(rid), onSuccess: (d) => done(d, S.rfq.cancelled), onError: fail })

  if (q.error instanceof ApiError && q.error.status === 404) {
    return (
      <div>
        <PageHeader title={S.rfq.title} back="/shop/rfq" />
        <EmptyState icon={<FileQuestion className="size-7" aria-hidden />} title={S.rfq.notFound} action={<LinkButton to="/shop/rfq">{S.rfq.mineTitle}</LinkButton>} />
      </div>
    )
  }

  const d = q.data
  const s = d?.summary
  const open = s?.status === 'Open'
  const quotes = d?.quotations ?? []

  return (
    <div className="pb-10">
      <PageHeader title={S.rfq.title} back="/shop/rfq" />
      <div className="space-y-5 px-4 pt-4">
        {q.error ? (
          <ErrorState error={q.error} onRetry={() => void q.refetch()} />
        ) : !d || !s ? (
          <div role="status" aria-label={S.app.loading} className="space-y-3">
            <Skeleton className="h-32 !rounded-card" />
            <Skeleton className="h-28 !rounded-card" />
          </div>
        ) : (
          <>
            <section className="rounded-card border border-line bg-surface p-4">
              <div className="flex items-start justify-between gap-3">
                <h2 className="min-w-0 break-words text-xl font-semibold leading-tight">{s.title}</h2>
                <RfqStatusBadge status={s.status} />
              </div>
              <p className="mt-2 text-[15px]">{fmt(S.rfq.wanted, { qty: s.quantity, unit: unitLabel(s.unit) })}</p>
              <p className="text-sm text-mute">{fmt(S.rfq.deliverTo, { city: s.deliveryCity, state: s.deliveryState })}</p>
              {s.targetPricePerUnit ? <p className="text-sm text-mute">{fmt(S.rfq.target, { price: money(s.targetPricePerUnit) })}</p> : null}
              <p className="text-sm text-mute">{fmt(S.rfq.openUntil, { date: formatShortDate(s.expiresAt) })}</p>
              {d.description && <p className="mt-3 break-words text-[15px]">{d.description}</p>}
            </section>

            <section>
              <h2 className="mb-2 text-sm font-semibold text-mute">
                {S.rfq.quotations} · {fmt(S.rfq.quotesCount, { n: quotes.length })}
              </h2>
              {quotes.length === 0 ? (
                <p className="rounded-card bg-peach-soft p-4 text-[15px] text-saving">{S.rfq.noQuotes}</p>
              ) : (
                <ul className="space-y-3">
                  {quotes.map((x, i) => (
                    <QuotationCard
                      key={x.id}
                      q={x}
                      unit={s.unit}
                      lowest={open && i === 0 && quotes.length > 1}
                      canAccept={open && x.status === 'Submitted'}
                      sure={sure === x.id}
                      busy={accept.isPending && sure === x.id}
                      onAccept={() => (sure === x.id ? accept.mutate(x.id) : setSure(x.id))}
                    />
                  ))}
                </ul>
              )}
            </section>

            {open && (
              <Button block variant={sure === 'cancel' ? 'danger' : 'dangerOutline'} loading={cancel.isPending} onClick={() => (sure === 'cancel' ? cancel.mutate() : setSure('cancel'))}>
                {sure === 'cancel' ? S.rfq.cancelSure : S.rfq.cancel}
              </Button>
            )}
          </>
        )}
      </div>
    </div>
  )
}
