import { Minus, Plus } from 'lucide-react'
import { S } from '@/i18n'
import { cn } from '@/lib/utils'

interface Props {
  value: number
  onChange: (v: number) => void
  min?: number
  max?: number
  step?: number
  label: string
  className?: string
}

/** Round − and + buttons with a typeable number in the middle (number pad on phones). */
export function QtyStepper({ value, onChange, min = 0, max, step = 1, label, className }: Props) {
  const clamp = (n: number) => Math.max(min, max === undefined ? n : Math.min(max, n))
  return (
    <div className={cn('inline-flex items-center gap-1 rounded-pill border border-line bg-surface p-1', className)}>
      <button
        type="button"
        aria-label={`${label}: ${S.product.lessQty}`}
        disabled={value <= min}
        onClick={() => onChange(clamp(value - step))}
        className="press flex size-11 items-center justify-center rounded-full text-ink disabled:opacity-30"
      >
        <Minus className="size-5" aria-hidden />
      </button>
      <input
        aria-label={label}
        inputMode="numeric"
        pattern="[0-9]*"
        value={value}
        onChange={(e) => onChange(clamp(Number(e.target.value.replace(/\D/g, '')) || 0))}
        onFocus={(e) => e.target.select()}
        className="h-11 w-14 bg-transparent text-center text-base font-semibold tabular-nums text-ink outline-none focus-visible:rounded-xl focus-visible:outline-2"
      />
      <button
        type="button"
        aria-label={`${label}: ${S.product.moreQty}`}
        disabled={max !== undefined && value >= max}
        onClick={() => onChange(clamp(value + step))}
        className="press flex size-11 items-center justify-center rounded-full bg-ink text-background disabled:opacity-30"
      >
        <Plus className="size-5" aria-hidden />
      </button>
    </div>
  )
}
