import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, ExternalLink, FileText, Info, UserX } from 'lucide-react'
import { adminApi } from '@/api/endpoints'
import { qk } from '@/api/keys'
import { BackButton, Badge, EmptyState, ErrorState, Skeleton } from '@/components/ui/misc'
import { StickyActionBar } from '@/components/StickyActionBar'
import { Button } from '@/components/ui/button'
import { Field, Textarea } from '@/components/ui/field'
import { useToast } from '@/components/ui/toast'
import { fmt, label, S } from '@/i18n'
import { formatShortDate } from '@/lib/utils'

/** Review one business: details, documents, a note, Approve or Reject. A note is needed to reject (UX_RULES 11). Esc goes back. */
export default function AdminReviewPage() {
  const { id = '' } = useParams()
  const nav = useNavigate()
  const qc = useQueryClient()
  const toast = useToast()
  const [note, setNote] = useState('')
  const [noteError, setNoteError] = useState('')
  const list = useQuery({ queryKey: ['admin-business', id], queryFn: () => adminApi.business(Number(id)) })
  const b = list.data

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !(e.target instanceof HTMLTextAreaElement)) nav('/admin/businesses')
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [nav])

  const done = async (text: string) => {
    await qc.invalidateQueries({ queryKey: qk.adminBusinesses() })
    await qc.invalidateQueries({ queryKey: ['admin-business', id] })
    await qc.invalidateQueries({ queryKey: qk.adminDashboard })
    toast.success(text)
    nav('/admin/businesses', { replace: true })
  }
  const approve = useMutation({ mutationFn: () => adminApi.approve(Number(id)), onSuccess: () => done(S.ad.approvedOk), onError: (e) => toast.error(e.message) })
  const reject = useMutation({ mutationFn: (n: string) => adminApi.reject(Number(id), n), onSuccess: () => done(S.ad.rejectedOk), onError: (e) => toast.error(e.message) })

  const Top = (
    <div className="flex items-center gap-1.5 px-4 pt-[18px] lg:px-1">
      <BackButton to="/admin/businesses" />
    </div>
  )
  if (list.isLoading) {
    return (
      <div role="status" aria-label={S.app.loading}>
        {Top}
        <div className="space-y-3 px-5 pt-4 lg:px-2"><Skeleton className="h-10 w-1/2" /><Skeleton className="h-44 !rounded-card" /></div>
      </div>
    )
  }
  if (list.error && !list.data) return <div>{Top}<ErrorState error={list.error} onRetry={() => void list.refetch()} /></div>
  if (!b) return <div>{Top}<EmptyState title={S.ad.notFound} /></div>

  const tone = b.verificationStatus === 'Approved' ? 'good' : b.verificationStatus === 'Pending' ? 'warn' : 'bad'
  const submitReject = () => {
    if (note.trim().length < 5) return setNoteError(S.ad.noteShort)
    reject.mutate(note.trim())
  }

  return (
    <div className="pb-40">
      {Top}
      <div className="mx-5 mt-3 lg:mx-2">
        <div className="flex flex-wrap items-center gap-2">
          <Badge>{b.type === 'Manufacturer' ? S.reg.manufacturer : S.reg.retailer}</Badge>
          <Badge tone={tone}>{label(S.status, b.verificationStatus)}</Badge>
        </div>
        <h1 className="mt-2 break-words text-[30px] font-semibold leading-[1.15] tracking-[-0.02em]">{b.displayName}</h1>
        <p className="mt-1 text-[15px] text-mute">{b.legalName} · {b.cityName} · {fmt(S.ad.sent, { d: formatShortDate(b.createdAt) })}</p>
      </div>

      <dl className="mx-5 mt-4 overflow-hidden rounded-card border border-line bg-surface lg:mx-2">
        {[
          [S.reg.gstin, b.gstin ?? '—'],
          [S.ad.owner, b.phone],
          [S.reg.city, b.cityName],
        ].map(([k, v]) => (
          <div key={k} className="flex justify-between gap-4 border-t border-line px-4 py-3 first:border-t-0">
            <dt className="text-sm text-mute">{k}</dt>
            <dd className="break-words text-right font-medium">{v}</dd>
          </div>
        ))}
      </dl>
      {b.gstin && (
        <p className="mx-5 mt-3 flex gap-2 rounded-2xl bg-soft p-3.5 text-sm leading-snug lg:mx-2">
          <Info className="mt-0.5 size-4 shrink-0" aria-hidden />
          {S.ad.gstNote}
        </p>
      )}
      {b.rejectionNote && <p className="mx-5 mt-3 rounded-2xl bg-error-bg p-3.5 text-sm font-medium text-error lg:mx-2">{b.rejectionNote}</p>}

      <section aria-label={S.admin.documents} className="mx-5 mt-5 lg:mx-2">
        <h2 className="mb-2 text-sm font-medium">{S.admin.documents}</h2>
        {b.documents.length === 0 ? (
          <p className="rounded-2xl bg-soft p-3.5 text-sm text-mute">{S.ad.docMissing}</p>
        ) : (
          <ul className="grid gap-2.5 sm:grid-cols-2">
            {b.documents.map((d) => (
              <li key={d.id} className="flex items-center gap-3 rounded-card border border-line bg-surface p-3.5">
                <FileText className="size-5 shrink-0" aria-hidden />
                <span className="flex-1 font-medium">{S.docType[d.docType]}</span>
                {d.url ? (
                  <a href={d.url} target="_blank" rel="noreferrer" className="press inline-flex min-h-11 items-center gap-1 rounded-full px-3 text-sm font-medium underline">
                    {S.admin.openDoc}
                    <ExternalLink className="size-4" aria-hidden />
                  </a>
                ) : (
                  <Badge>{d.status}</Badge>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>

      <div className="mx-5 mt-5 lg:mx-2">
        <Field label={`${S.ad.noteLabel} ${S.ad.noteNeeded}`} error={noteError}>
          {(fid, inv) => (
            <Textarea
              id={fid}
              invalid={inv}
              value={note}
              onChange={(e) => {
                setNote(e.target.value)
                setNoteError('')
              }}
            />
          )}
        </Field>
        <p className="mt-3 hidden text-xs text-mute lg:block">{S.ad.keys}</p>
      </div>

      <StickyActionBar className="lg:left-60">
        <div className="mx-auto flex w-full max-w-3xl gap-2.5">
          {b.verificationStatus !== 'Rejected' && (
            <Button variant="dangerOutline" size="lg" className="flex-1" loading={reject.isPending} onClick={submitReject}>
              <UserX className="size-5" aria-hidden />
              {S.admin.reject}
            </Button>
          )}
          {b.verificationStatus !== 'Approved' && (
            <Button size="lg" className="flex-1" loading={approve.isPending} onClick={() => approve.mutate()}>
              <CheckCircle2 className="size-5" aria-hidden />
              {S.admin.approve}
            </Button>
          )}
        </div>
      </StickyActionBar>
    </div>
  )
}
