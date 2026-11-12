import { Minus, Plus } from 'lucide-react'
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

/** Big +/- buttons with a typeable number in the middle. */
export function QtyStepper({ value, onChange, min = 0, max, step = 1, label, className }: Props) {
  const clamp = (n: number) => Math.max(min, max === undefined ? n : Math.min(max, n))
  return (
    <div className={cn('inline-flex items-center rounded-xl border border-line', className)}>
      <button type="button" aria-label={`${label} −`} disabled={value <= min} onClick={() => onChange(clamp(value - step))} className="flex size-12 items-center justify-center rounded-l-xl text-navy-900 disabled:opacity-30 active:bg-navy-50">
        <Minus className="size-5" />
      </button>
      <input
        aria-label={label}
        inputMode="numeric"
        value={value}
        onChange={(e) => onChange(clamp(Number(e.target.value.replace(/\D/g, '')) || 0))}
        onFocus={(e) => e.target.select()}
        className="h-12 w-16 border-x border-line bg-white text-center text-base font-bold text-ink focus:outline-2 focus:outline-leaf-600"
      />
      <button type="button" aria-label={`${label} +`} disabled={max !== undefined && value >= max} onClick={() => onChange(clamp(value + step))} className="flex size-12 items-center justify-center rounded-r-xl text-navy-900 disabled:opacity-30 active:bg-navy-50">
        <Plus className="size-5" />
      </button>
    </div>
  )
}
