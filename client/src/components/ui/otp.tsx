import { useEffect, useRef } from 'react'
import { fmt, S } from '@/i18n'
import { cn } from '@/lib/utils'

interface Props {
  value: string
  onChange: (v: string) => void
  onComplete?: (v: string) => void
  invalid?: boolean
  length?: number
  autoFocus?: boolean
}

/** Six boxes, one real input on top. Number pad on phones, one-time-code autofill, and the WebOTP API where the browser has it. */
export function OtpInput({ value, onChange, onComplete, invalid, length = 6, autoFocus }: Props) {
  const ref = useRef<HTMLInputElement>(null)

  const set = (raw: string) => {
    const v = raw.replace(/\D/g, '').slice(0, length)
    onChange(v)
    if (v.length === length) onComplete?.(v)
  }

  // Reads the SMS automatically on Chrome for Android (needs the backend SMS to end with "@host #code").
  useEffect(() => {
    if (!('OTPCredential' in window)) return
    const ctl = new AbortController()
    const creds = navigator.credentials as unknown as { get: (o: unknown) => Promise<{ code?: string } | null> }
    creds
      .get({ otp: { transport: ['sms'] }, signal: ctl.signal })
      .then((c) => c?.code && set(c.code))
      .catch(() => undefined)
    return () => ctl.abort()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return (
    <div className="relative" onClick={() => ref.current?.focus()}>
      <input
        ref={ref}
        value={value}
        onChange={(e) => set(e.target.value)}
        inputMode="numeric"
        pattern="[0-9]*"
        autoComplete="one-time-code"
        aria-label={fmt(S.login.otpAria, { n: value.length })}
        aria-invalid={invalid || undefined}
        autoFocus={autoFocus}
        maxLength={length}
        className="absolute inset-0 z-10 size-full cursor-text opacity-0"
      />
      <div className="grid gap-2" style={{ gridTemplateColumns: `repeat(${length}, minmax(0, 1fr))` }} aria-hidden>
        {Array.from({ length }, (_, i) => {
          const active = i === Math.min(value.length, length - 1)
          return (
            <span
              key={i}
              className={cn(
                'flex h-[58px] items-center justify-center rounded-2xl border bg-surface text-[22px] font-semibold tabular-nums',
                invalid ? 'border-error' : active ? 'border-2 border-ink' : 'border-line',
              )}
            >
              {value[i] ?? (active && <span className="h-[26px] w-0.5 animate-pulse bg-ink" />)}
            </span>
          )
        })}
      </div>
    </div>
  )
}
