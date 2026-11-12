import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, ExternalLink, FileText, UserX } from 'lucide-react'
import { adminApi } from '@/api/endpoints'
import type { AdminBusiness, VerificationStatus } from '@/api/types'
import { Button } from '@/components/ui/button'
import { Field, Textarea } from '@/components/ui/field'
import { Badge, Card, EmptyState, ErrorState, KeyValue, PageHeader, PageLoading, Segmented, Sheet } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { label, S } from '@/i18n'
import { formatDate } from '@/lib/utils'

type Tab = Extract<VerificationStatus, 'Pending' | 'Approved' | 'Rejected'>

export default function AdminBusinessesPage() {
  const [tab, setTab] = useState<Tab>('Pending')
  const [sel, setSel] = useState<AdminBusiness | null>(null)
  const [rejecting, setRejecting] = useState(false)
  const [note, setNote] = useState('')
  const [noteError, setNoteError] = useState('')
  const qc = useQueryClient()
  const toast = useToast()
  const list = useQuery({ queryKey: ['admin-businesses', tab], queryFn: () => adminApi.businesses(tab) })
  const pendingCount = useQuery({ queryKey: ['admin-businesses', 'Pending'], queryFn: () => adminApi.businesses('Pending') })

  const close = () => { setSel(null); setRejecting(false); setNote(''); setNoteError('') }
  const done = async () => {
    await qc.invalidateQueries({ queryKey: ['admin-businesses'] })
    close()
  }
  const approve = useMutation({ mutationFn: (id: number) => adminApi.approve(id), onSuccess: done, onError: (e) => toast.error(e.message) })
  const reject = useMutation({ mutationFn: (v: { id: number; note: string }) => adminApi.reject(v.id, v.note), onSuccess: done, onError: (e) => toast.error(e.message) })

  const submitReject = () => {
    if (!note.trim()) return setNoteError(S.admin.needNote)
    if (sel) reject.mutate({ id: sel.id, note: note.trim() })
  }

  return (
    <div>
      <PageHeader title={S.admin.approvals} />
      <div className="space-y-3 px-4 pt-4">
        <Segmented
          value={tab}
          onChange={setTab}
          options={[
            { value: 'Pending', label: S.admin.pending, count: pendingCount.data?.length },
            { value: 'Approved', label: S.admin.approved },
            { value: 'Rejected', label: S.admin.rejected },
          ]}
        />
        {list.isLoading ? (
          <PageLoading />
        ) : list.error ? (
          <ErrorState error={list.error} onRetry={() => void list.refetch()} />
        ) : (list.data ?? []).length === 0 ? (
          <EmptyState icon={<CheckCircle2 className="size-8" />} title={S.admin.none} />
        ) : (
          list.data!.map((b) => (
            <button key={b.id} type="button" onClick={() => setSel(b)} className="block w-full text-left">
              <Card className="p-4 active:bg-navy-50">
                <div className="flex items-start justify-between gap-2">
                  <p className="min-w-0 truncate font-extrabold text-navy-900">{b.displayName}</p>
                  <Badge tone={b.type === 'Manufacturer' ? 'neutral' : 'good'}>{b.type === 'Manufacturer' ? S.reg.manufacturer : S.reg.retailer}</Badge>
                </div>
                <p className="text-sm text-mute">{b.legalName}</p>
                <p className="mt-1 text-sm">{b.gstin ?? '—'} · {b.cityName} · {formatDate(b.createdAt)}</p>
              </Card>
            </button>
          ))
        )}
      </div>

      <Sheet open={!!sel} onClose={close} title={sel?.displayName ?? ''}>
        {sel && (
          <div className="space-y-4">
            <Card className="px-4 py-2">
              <KeyValue label={S.reg.legalName} value={sel.legalName} />
              <KeyValue label={S.reg.type} value={sel.type === 'Manufacturer' ? S.reg.manufacturer : S.reg.retailer} />
              <KeyValue label={S.reg.gstin} value={sel.gstin ?? '—'} />
              <KeyValue label={S.reg.phone} value={<a href={`tel:${sel.phone}`} className="font-bold text-navy-800 underline">{sel.phone}</a>} />
              <KeyValue label={S.reg.city} value={sel.cityName} />
              <KeyValue label={S.pending.status} value={<Badge tone={sel.verificationStatus === 'Approved' ? 'good' : sel.verificationStatus === 'Pending' ? 'warn' : 'bad'}>{label(S.status, sel.verificationStatus)}</Badge>} />
            </Card>
            {sel.rejectionNote && <p className="rounded-xl bg-bad-bg p-3 text-sm font-semibold text-bad">{sel.rejectionNote}</p>}

            <div>
              <p className="mb-2 font-extrabold text-navy-900">{S.admin.documents}</p>
              {sel.documents.length === 0 ? (
                <p className="text-mute">—</p>
              ) : (
                <ul className="space-y-2">
                  {sel.documents.map((d) => (
                    <li key={d.id} className="flex items-center gap-3 rounded-xl border border-line p-3">
                      <FileText className="size-5 shrink-0 text-navy-800" />
                      <span className="flex-1 font-semibold">{S.docType[d.docType]}</span>
                      {d.url ? (
                        <a href={d.url} target="_blank" rel="noreferrer" className="inline-flex min-h-10 items-center gap-1 rounded-lg px-3 text-sm font-bold text-leaf-700 hover:bg-leaf-50">
                          {S.admin.openDoc}<ExternalLink className="size-4" />
                        </a>
                      ) : (
                        <Badge>{d.status}</Badge>
                      )}
                    </li>
                  ))}
                </ul>
              )}
            </div>

            {sel.verificationStatus !== 'Approved' && !rejecting && (
              <Button block variant="accent" loading={approve.isPending} onClick={() => approve.mutate(sel.id)}><CheckCircle2 className="size-5" />{S.admin.approve}</Button>
            )}
            {sel.verificationStatus !== 'Rejected' && !rejecting && (
              <Button block variant="dangerOutline" onClick={() => setRejecting(true)}><UserX className="size-5" />{S.admin.reject}</Button>
            )}
            {rejecting && (
              <div className="space-y-3 rounded-2xl bg-bad-bg/50 p-3">
                <Field label={S.admin.rejectNote} error={noteError}>{(id, inv) => <Textarea id={id} invalid={inv} value={note} onChange={(e) => { setNote(e.target.value); setNoteError('') }} />}</Field>
                <div className="flex gap-2">
                  <Button variant="outline" onClick={() => setRejecting(false)}>{S.app.cancel}</Button>
                  <Button variant="danger" className="flex-1" loading={reject.isPending} onClick={submitReject}>{S.admin.reject}</Button>
                </div>
              </div>
            )}
          </div>
        )}
      </Sheet>
    </div>
  )
}
