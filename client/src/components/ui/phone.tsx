import { forwardRef, type InputHTMLAttributes } from 'react'
import { cn } from '@/lib/utils'

/** +91 and a 10 digit number, grouped as 98765 43210 for reading. Number pad on phones. */
export const PhoneInput = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement> & { invalid?: boolean }>(({ className, invalid, ...p }, ref) => (
  <div className={cn('flex min-h-[58px] items-center overflow-hidden rounded-2xl border-2 bg-surface focus-within:border-ink', invalid ? 'border-error' : 'border-line')}>
    <span className="flex items-center self-stretch border-r border-line px-3.5 text-[17px]" aria-hidden>
      +91
    </span>
    <input
      ref={ref}
      type="tel"
      inputMode="numeric"
      autoComplete="tel-national"
      maxLength={10}
      placeholder="98765 43210"
      aria-invalid={invalid || undefined}
      className={cn('min-w-0 flex-1 bg-transparent px-3.5 text-lg tracking-[0.04em] tabular-nums outline-none placeholder:text-mute/60', className)}
      {...p}
    />
  </div>
))
PhoneInput.displayName = 'PhoneInput'
