import { useState, type FormEvent } from 'react'
import { useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { FileQuestion, Phone } from 'lucide-react'
import { sellerRfqApi } from '@/api/endpoints'
import { ApiError } from '@/api/client'
import { qk } from '@/api/keys'
import type { Quotation, RfqDetail } from '@/api/types'
import { RfqStatusBadge, unitLabel } from '@/components/RfqParts'
import { Button } from '@/components/ui/button'
import { Field, Input, Textarea } from '@/components/ui/field'
import { EmptyState, ErrorState, LinkButton, PageHeader, Skeleton } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { fmt, S } from '@/i18n'
import { formatShortDate, money } from '@/lib/utils'

/** Send or change one quotation. Starts with the numbers already sent, or the full quantity asked for. */
function QuoteForm({ rid, d, mine }: { rid: number; d: RfqDetail; mine?: Quotation }) {
  const qc = useQueryClient()
  const toast = useToast()
  const max = d.summary.quantity
  const [v, setV] = useState({ price: mine ? String(mine.pricePerUnit) : '', qty: String(mine?.quantity ?? max), lead: mine ? String(mine.leadTimeDays) : '', note: mine?.note ?? '' })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const set = (k: keyof typeof v) => (e: { target: { value: string } }) => setV((x) => ({ ...x, [k]: e.target.value }))

  const saved = (data: RfqDetail, msg: string) => {
    qc.setQueryData(qk.sellerRfq(rid), data)
    void qc.invalidateQueries({ queryKey: qk.sellerRfqs() })
    void qc.invalidateQueries({ queryKey: qk.sellerQuotes })
    toast.success(msg)
  }
  const send = useMutation({
    mutationFn: () => sellerRfqApi.quote(rid, { pricePerUnit: Number(v.price), quantity: Math.floor(Number(v.qty)), leadTimeDays: Math.floor(Number(v.lead)), note: v.note.trim() || undefined }),
    onSuccess: (data) => saved(data, S.rfq.qSent),
    onError: (e) => toast.error(e.message),
  })
  const withdraw = useMutation({
    mutationFn: () => sellerRfqApi.withdraw(rid),
    onSuccess: (data) => saved(data, S.rfq.qWithdrawn),
    onError: (e) => toast.error(e.message),
  })

  const submit = (e: FormEvent) => {
    e.preventDefault()
    const next: Record<string, string> = {}
    if (!(Number(v.price) > 0)) next.price = S.rfq.needPrice
    if (!(Number(v.qty) >= 1 && Number(v.qty) <= max)) next.qty = fmt(S.rfq.needQtyMax, { max })
    if (!(Number(v.lead) >= 1 && Number(v.lead) <= 365)) next.lead = S.rfq.needLead
    setErrors(next)
    if (Object.keys(next).length === 0) send.mutate()
  }

  const active = mine && mine.status !== 'Withdrawn'
  return (
    <form onSubmit={submit} noValidate className="space-y-4 rounded-card border border-line bg-surface p-4">
      <h2 className="text-lg font-semibold">{S.rfq.yourQuote}</h2>
      <Field label={S.rfq.qPrice} error={errors.price}>
        {(id, invalid) => <Input id={id} invalid={invalid} inputMode="decimal" value={v.price} onChange={set('price')} />}
      </Field>
      <div className="grid grid-cols-2 gap-3">
        <Field label={S.rfq.qQty} error={errors.qty}>
          {(id, invalid) => <Input id={id} invalid={invalid} inputMode="numeric" value={v.qty} onChange={set('qty')} />}
        </Field>
        <Field label={S.rfq.qLead} error={errors.lead}>
          {(id, invalid) => <Input id={id} invalid={invalid} inputMode="numeric" value={v.lead} onChange={set('lead')} />}
        </Field>
      </div>
      <Field label={S.rfq.qNote}>{(id) => <Textarea id={id} value={v.note} maxLength={500} onChange={set('note')} />}</Field>
      <Button type="submit" size="lg" block loading={send.isPending}>
        {active ? S.rfq.qUpdate : S.rfq.qSend}
      </Button>
      {mine?.status === 'Submitted' && (
        <Button type="button" variant="dangerOutline" block loading={withdraw.isPending} onClick={() => withdraw.mutate()}>
          {S.rfq.qWithdraw}
        </Button>
      )}
    </form>
  )
}

/** A request as a manufacturer sees it: what is wanted, the buyer's target, and the manufacturer's own quotation. The buyer is hidden until chosen. */
export default function SellerRfqDetailPage() {
  const { id } = useParams()
  const rid = Number(id)
  const q = useQuery({ queryKey: qk.sellerRfq(rid), queryFn: () => sellerRfqApi.get(rid), enabled: Number.isInteger(rid) && rid > 0 })

  if (q.error instanceof ApiError && q.error.status === 404) {
    return (
      <div>
        <PageHeader title={S.rfq.sTitle} back="/seller/rfq" />
        <EmptyState icon={<FileQuestion className="size-7" aria-hidden />} title={S.rfq.notFound} action={<LinkButton to="/seller/rfq">{S.rfq.sTitle}</LinkButton>} />
      </div>
    )
  }

  const d = q.data
  const s = d?.summary
  const mine = d?.quotations.find((x) => x.isMine)
  const open = s?.status === 'Open'

  return (
    <div className="pb-10">
      <PageHeader title={S.rfq.sTitle} back="/seller/rfq" />
      <div className="space-y-5 px-4 pt-4">
        {q.error ? (
          <ErrorState error={q.error} onRetry={() => void q.refetch()} />
        ) : !d || !s ? (
          <div role="status" aria-label={S.app.loading} className="space-y-3">
            <Skeleton className="h-32 !rounded-card" />
            <Skeleton className="h-64 !rounded-card" />
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
              {s.targetPricePerUnit ? <p className="text-sm text-mute">{fmt(S.rfq.buyerTarget, { price: money(s.targetPricePerUnit) })}</p> : null}
              <p className="text-sm text-mute">{fmt(S.rfq.openUntil, { date: formatShortDate(s.expiresAt) })}</p>
              {d.description && <p className="mt-3 break-words text-[15px]">{d.description}</p>}
            </section>

            {mine?.status === 'Accepted' && (
              <section className="rounded-card bg-brand-soft p-4">
                <p className="font-semibold text-brand">{S.rfq.won}</p>
                {d.buyerContact && (
                  <>
                    <p className="mt-2 text-sm font-medium">{S.rfq.buyerContact}</p>
                    <p className="text-[15px]">{d.buyerContact.name}</p>
                    <a href={`tel:${d.buyerContact.phone}`} className="press mt-2 inline-flex min-h-11 items-center gap-2 rounded-pill bg-brand px-4 text-sm font-medium text-white">
                      <Phone className="size-4" aria-hidden />
                      {fmt(S.rfq.call, { phone: d.buyerContact.phone })}
                    </a>
                  </>
                )}
              </section>
            )}
            {mine?.status === 'Rejected' && <p className="rounded-card bg-peach-soft p-4 text-[15px] text-saving">{S.rfq.lost}</p>}

            {open && (!mine || mine.status === 'Submitted' || mine.status === 'Withdrawn') ? (
              <>
                <QuoteForm key={`${mine?.id ?? 'new'}-${mine?.status ?? ''}`} rid={rid} d={d} mine={mine} />
                <p className="text-center text-[13px] text-mute">{S.rfq.buyerHidden}</p>
              </>
            ) : null}
          </>
        )}
      </div>
    </div>
  )
}
