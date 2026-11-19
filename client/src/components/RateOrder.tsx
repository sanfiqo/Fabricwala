import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Star } from 'lucide-react'
import { ratingApi } from '@/api/endpoints'
import { qk } from '@/api/keys'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/field'
import { Sheet } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { fmt, S } from '@/i18n'
import { haptic } from '@/motion'
import { cn } from '@/lib/utils'

function Stars({ value, size = 'size-4' }: { value: number; size?: string }) {
  return (
    <span className="inline-flex gap-0.5 text-peach" aria-hidden>
      {[1, 2, 3, 4, 5].map((n) => (
        <Star key={n} className={cn(size, n <= value && 'fill-current', n > value && 'text-line')} />
      ))}
    </span>
  )
}

/** The form in the sheet: five stars and an optional comment. Starts from the earlier rating when there is one. */
function RateForm({ orderId, initialStars, initialComment, onDone }: { orderId: number; initialStars: number; initialComment: string; onDone: () => void }) {
  const qc = useQueryClient()
  const toast = useToast()
  const [stars, setStars] = useState(initialStars)
  const [comment, setComment] = useState(initialComment)
  const [error, setError] = useState('')

  const send = useMutation({
    mutationFn: () => ratingApi.set(orderId, stars, comment.trim()),
    onSuccess: (r) => {
      haptic()
      qc.setQueryData(qk.rating(orderId), r)
      toast.success(S.rate.thanks)
      onDone()
    },
    onError: (e) => toast.error(e.message),
  })

  return (
    <div className="space-y-4">
      <div className="text-center">
        <p className="text-lg font-semibold">{S.rate.question}</p>
        <p className="mt-1 text-sm text-mute">{S.rate.sub}</p>
      </div>
      <div role="radiogroup" aria-label={S.rate.question} className="flex justify-center gap-2">
        {[1, 2, 3, 4, 5].map((n) => (
          <button
            key={n}
            type="button"
            role="radio"
            aria-checked={stars === n}
            aria-label={fmt(S.rate.star, { n })}
            onClick={() => {
              setStars(n)
              setError('')
            }}
            className="press flex size-12 items-center justify-center rounded-full"
          >
            <Star className={cn('size-9 text-peach', n <= stars ? 'fill-current' : 'text-line')} aria-hidden />
          </button>
        ))}
      </div>
      {error && (
        <p role="alert" className="text-center text-sm font-medium text-error">
          {error}
        </p>
      )}
      <Textarea aria-label={S.rate.comment} placeholder={S.rate.comment} maxLength={1000} value={comment} onChange={(e) => setComment(e.target.value)} />
      <div className="flex gap-2">
        <Button variant="outline" onClick={onDone}>
          {S.rate.cancel}
        </Button>
        <Button
          className="flex-1"
          loading={send.isPending}
          onClick={() => {
            if (stars < 1) return setError(S.rate.needStars)
            send.mutate()
          }}
        >
          {S.rate.submit}
        </Button>
      </div>
    </div>
  )
}

/** A row on the order card: "Rate this order", or the stars already given with a Change button. Opens the review sheet. */
export function RateOrderRow({ orderId }: { orderId: number }) {
  const [open, setOpen] = useState(false)
  const rating = useQuery({ queryKey: qk.rating(orderId), queryFn: () => ratingApi.get(orderId), retry: false })
  const r = rating.data
  return (
    <>
      <button type="button" onClick={() => setOpen(true)} className="press flex min-h-[52px] w-full items-center gap-3 border-t border-line px-4 text-left text-sm">
        <Star className="size-5 shrink-0" aria-hidden />
        {r ? (
          <>
            <span className="flex flex-1 items-center gap-2">
              <span>{S.rate.rowDone}</span>
              <Stars value={r.stars} />
            </span>
            <span className="font-medium text-brand">{S.rate.change}</span>
          </>
        ) : (
          <span className="flex-1">{S.rate.row}</span>
        )}
      </button>
      <Sheet open={open} onClose={() => setOpen(false)} title={S.rate.title}>
        <RateForm key={r?.updatedAt ?? 'new'} orderId={orderId} initialStars={r?.stars ?? 0} initialComment={r?.comment ?? ''} onDone={() => setOpen(false)} />
      </Sheet>
    </>
  )
}
