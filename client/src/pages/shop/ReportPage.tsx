import { useMemo, useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Controller, useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Camera, Check, X } from 'lucide-react'
import { claimApi, orderApi } from '@/api/endpoints'
import { qk } from '@/api/keys'
import type { ClaimReason } from '@/api/types'
import { QtyStepper } from '@/components/QtyStepper'
import { StickyActionBar } from '@/components/StickyActionBar'
import { Button } from '@/components/ui/button'
import { Field, Textarea } from '@/components/ui/field'
import { BackButton, Chip, EmptyState, ErrorState, Skeleton } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { fmt, S, useLang } from '@/i18n'
import { compressImage } from '@/lib/image'
import { clearDraft, loadDraft, useSaveDraft } from '@/lib/useDraft'

const REASONS: Array<[ClaimReason, () => string]> = [
  ['Damaged', () => S.report.damaged],
  ['WrongItem', () => S.report.wrongItem],
  ['LessQuantity', () => S.report.lessQty],
  ['NotAsShown', () => S.report.notAsShown],
  ['Other', () => S.report.other],
]
const MAX_PHOTOS = 3

interface Values {
  reason: string
  pieces: number
  note: string
}

/** "What went wrong?" One tap away from a delivered order. Photos are shrunk on the phone before upload. */
export default function ReportPage() {
  const { id = '' } = useParams()
  const [params] = useSearchParams()
  const nav = useNavigate()
  const qc = useQueryClient()
  const toast = useToast()
  const lang = useLang()
  const [photos, setPhotos] = useState<Array<{ file: File; url: string }>>([])
  const [photoError, setPhotoError] = useState('')
  const [sent, setSent] = useState(false)
  const order = useQuery({ queryKey: qk.order(Number(id)), queryFn: () => orderApi.get(Number(id)) })

  const so = order.data?.sellerOrders.find((s) => String(s.id) === params.get('s')) ?? order.data?.sellerOrders.find((s) => s.status === 'ReachedKashmir' || s.status === 'Delivered')
  const max = so?.items.reduce((a, i) => a + i.quantity, 0) ?? 1

  const draftKey = `report-${id}`
  const saved = useMemo(() => loadDraft<Values>(draftKey), [draftKey])
  const schema = useMemo(
    () =>
      z.object({
        reason: z.string().min(1, S.report.needReason),
        pieces: z.number().int().min(1, fmt(S.report.badPieces, { max })).max(max, fmt(S.report.badPieces, { max })),
        note: z.string().max(500),
      }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [lang, max],
  )
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { reason: '', pieces: 1, note: '', ...saved } })
  useSaveDraft(draftKey, form.watch())

  const send = useMutation({
    mutationFn: async (v: Values) => {
      if (!so) throw new Error(S.report.noOrder)
      const claim = await claimApi.create(so.id, { reason: v.reason as ClaimReason, pieces: v.pieces, note: v.note.trim() || undefined })
      for (const p of photos) await claimApi.addPhoto(claim.id, p.file)
    },
    onSuccess: async () => {
      clearDraft(draftKey)
      setSent(true)
      await qc.invalidateQueries({ queryKey: qk.orders })
    },
    onError: (e) => toast.error(e.message),
  })

  const addPhotos = async (files: FileList | null) => {
    if (!files) return
    setPhotoError('')
    const room = MAX_PHOTOS - photos.length
    if (files.length > room) setPhotoError(S.report.maxPhotos)
    const small = await Promise.all([...files].slice(0, room).map((f) => compressImage(f)))
    setPhotos((p) => [...p, ...small.map((file) => ({ file, url: URL.createObjectURL(file) }))])
  }
  const removePhoto = (i: number) => setPhotos((p) => p.filter((_, j) => j !== i))

  const Top = (
    <div className="flex items-center gap-2.5 px-4 pt-[18px]">
      <BackButton to={`/shop/orders/${id}`} />
    </div>
  )

  if (order.isLoading) {
    return (
      <div role="status" aria-label={S.app.loading}>
        {Top}
        <div className="space-y-3 px-5 pt-4">
          <Skeleton className="h-10 w-3/4" />
          <Skeleton className="h-16 !rounded-card" />
          <Skeleton className="h-24 !rounded-card" />
        </div>
      </div>
    )
  }
  if (order.error || !order.data) return <div>{Top}<ErrorState error={order.error} onRetry={() => void order.refetch()} /></div>
  if (!so) return <div>{Top}<EmptyState title={S.report.noOrder} /></div>
  if (sent) {
    return (
      <div className="flex min-h-[70dvh] flex-col items-center justify-center px-6 text-center">
        <span className="mb-4 flex size-16 items-center justify-center rounded-full bg-success text-white" aria-hidden>
          <Check className="size-8" />
        </span>
        <h1 className="text-2xl font-semibold">{S.report.sent}</h1>
        <p className="mt-2 max-w-sm text-mute">{S.report.sentSub}</p>
        <Button className="mt-6" onClick={() => nav('/shop/orders', { replace: true })}>{S.nav.orders}</Button>
      </div>
    )
  }

  return (
    <form onSubmit={form.handleSubmit((v) => send.mutate(v))} noValidate className="pb-44">
      {Top}
      <h1 className="mx-5 mt-3.5 text-[30px] font-semibold leading-[1.15] tracking-[-0.02em]">{S.report.title}</h1>
      <p className="mx-5 mt-1.5 text-[15px] text-mute">{fmt(S.report.forOrder, { n: order.data.orderNumber, seller: so.manufacturerName })}</p>

      <div className="space-y-6 px-5 pt-5">
        <div>
          <span id="reason-label" className="mb-2 block text-sm font-medium">{S.report.problem}</span>
          <Controller
            control={form.control}
            name="reason"
            render={({ field }) => (
              <div role="radiogroup" aria-labelledby="reason-label" className="flex flex-wrap gap-2">
                {REASONS.map(([v, label]) => (
                  <Chip key={v} role="radio" aria-checked={field.value === v} active={field.value === v} onClick={() => field.onChange(v)}>
                    {label()}
                  </Chip>
                ))}
              </div>
            )}
          />
          {form.formState.errors.reason && <p role="alert" className="mt-2 text-sm font-medium text-error">{form.formState.errors.reason.message}</p>}
        </div>

        <div>
          <span className="mb-2 block text-sm font-medium">{S.report.howMany}</span>
          <Controller
            control={form.control}
            name="pieces"
            render={({ field }) => (
              <div className="flex items-center gap-3">
                <QtyStepper label={S.report.howMany} min={1} max={max} value={field.value} onChange={field.onChange} />
                <span className="text-sm text-mute">/ {max}</span>
              </div>
            )}
          />
          {form.formState.errors.pieces && <p role="alert" className="mt-2 text-sm font-medium text-error">{form.formState.errors.pieces.message}</p>}
        </div>

        <div>
          <span className="mb-2 block text-sm font-medium">{S.report.photos} <span className="font-normal text-mute">{S.report.photosHint}</span></span>
          <div className="grid grid-cols-3 gap-2">
            {photos.map((p, i) => (
              <div key={p.url} className="relative h-[92px] overflow-hidden rounded-2xl bg-soft">
                <img src={p.url} alt="" className="size-full object-cover" />
                <button type="button" aria-label={S.report.removePhoto} onClick={() => removePhoto(i)} className="press absolute right-1 top-1 flex size-9 items-center justify-center rounded-full bg-ink text-background">
                  <X className="size-4" aria-hidden />
                </button>
              </div>
            ))}
            {photos.length < MAX_PHOTOS && (
              <label className="press flex h-[92px] cursor-pointer flex-col items-center justify-center gap-1 rounded-2xl border border-dashed border-mute/50 bg-soft text-sm focus-within:outline-2 focus-within:outline-ink">
                <Camera className="size-5" aria-hidden />
                {S.report.addPhoto}
                <input type="file" accept="image/*" capture="environment" multiple className="sr-only" onChange={(e) => void addPhotos(e.target.files)} />
              </label>
            )}
          </div>
          {photoError && <p role="alert" className="mt-2 text-sm font-medium text-error">{photoError}</p>}
        </div>

        <Field label={`${S.report.more} ${S.report.optional}`}>
          {(fid) => <Textarea id={fid} placeholder={S.report.moreHint} maxLength={500} {...form.register('note')} />}
        </Field>

        <div className="rounded-2xl border border-line bg-surface p-4 text-sm leading-relaxed text-mute">
          <b className="font-semibold text-ink">{S.report.nextTitle}</b>
          <br />
          {S.report.nextBody}
        </div>
      </div>

      <StickyActionBar>
        <Button type="submit" size="lg" block loading={send.isPending}>
          {S.report.send}
        </Button>
      </StickyActionBar>
    </form>
  )
}
