import { useEffect, type HTMLAttributes, type ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ArrowLeft, Loader2, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import { S } from '@/i18n'
import { Button } from './button'

export function Card({ className, ...p }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('rounded-2xl border border-line bg-white', className)} {...p} />
}

const tones = {
  neutral: 'bg-navy-50 text-navy-800',
  good: 'bg-leaf-100 text-leaf-700',
  warn: 'bg-warn-bg text-warn',
  bad: 'bg-bad-bg text-bad',
} as const
export type Tone = keyof typeof tones
export function Badge({ tone = 'neutral', className, ...p }: HTMLAttributes<HTMLSpanElement> & { tone?: Tone }) {
  return <span className={cn('inline-flex items-center rounded-full px-2.5 py-1 text-xs font-bold', tones[tone], className)} {...p} />
}

export function Spinner({ className }: { className?: string }) {
  return <Loader2 aria-label={S.app.loading} className={cn('size-6 animate-spin text-leaf-600', className)} />
}
export function PageLoading() {
  return (
    <div className="flex justify-center py-16">
      <Spinner className="size-8" />
    </div>
  )
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <div className="px-4 py-12 text-center">
      <p className="font-semibold text-bad">{error instanceof Error ? error.message : S.app.error}</p>
      {onRetry && (
        <Button variant="outline" size="sm" className="mt-4" onClick={onRetry}>
          {S.app.retry}
        </Button>
      )}
    </div>
  )
}

export function EmptyState({ icon, title, sub, action }: { icon?: ReactNode; title: string; sub?: string; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-center px-6 py-14 text-center">
      {icon && <div className="mb-3 flex size-16 items-center justify-center rounded-full bg-leaf-50 text-leaf-700">{icon}</div>}
      <p className="text-lg font-bold text-navy-900">{title}</p>
      {sub && <p className="mt-1 max-w-xs text-mute">{sub}</p>}
      {action && <div className="mt-5">{action}</div>}
    </div>
  )
}

/** Page title with optional back link. Followed by the app's stitched divider. */
export function PageHeader({ title, back, action }: { title: string; back?: string | true; action?: ReactNode }) {
  const nav = useNavigate()
  return (
    <div className="px-4 pt-4">
      <div className="flex min-h-12 items-center gap-2">
        {back && (
          <button type="button" aria-label={S.app.back} onClick={() => (back === true ? nav(-1) : nav(back))} className="-ml-2 flex size-12 items-center justify-center rounded-full text-navy-900 hover:bg-navy-50">
            <ArrowLeft className="size-6" />
          </button>
        )}
        <h1 className="flex-1 text-xl font-extrabold tracking-tight text-navy-900">{title}</h1>
        {action}
      </div>
      <hr className="stitch mt-2" />
    </div>
  )
}

export function SectionTitle({ children, action }: { children: ReactNode; action?: ReactNode }) {
  return (
    <div className="mb-2 mt-6 flex items-center justify-between">
      <h2 className="text-base font-extrabold text-navy-900">{children}</h2>
      {action}
    </div>
  )
}

/** Bottom sheet for filters, dialogs and confirmations. */
export function Sheet({ open, onClose, title, children }: { open: boolean; onClose: () => void; title: string; children: ReactNode }) {
  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    document.addEventListener('keydown', onKey)
    document.body.style.overflow = 'hidden'
    return () => {
      document.removeEventListener('keydown', onKey)
      document.body.style.overflow = ''
    }
  }, [open, onClose])
  if (!open) return null
  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center sm:items-center" role="dialog" aria-modal="true" aria-label={title}>
      <button type="button" aria-label={S.app.cancel} className="absolute inset-0 bg-navy-950/50" onClick={onClose} />
      <div className="relative max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-t-3xl bg-white p-5 pb-8 sm:rounded-3xl">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-extrabold text-navy-900">{title}</h2>
          <button type="button" onClick={onClose} aria-label={S.app.cancel} className="flex size-11 items-center justify-center rounded-full hover:bg-navy-50">
            <X className="size-5" />
          </button>
        </div>
        {children}
      </div>
    </div>
  )
}

/** Pill tabs / segmented control. */
export function Segmented<T extends string>({ value, onChange, options }: { value: T; onChange: (v: T) => void; options: Array<{ value: T; label: string; count?: number }> }) {
  return (
    <div role="tablist" className="flex gap-1 rounded-2xl bg-navy-50 p-1">
      {options.map((o) => (
        <button
          key={o.value}
          role="tab"
          aria-selected={o.value === value}
          type="button"
          onClick={() => onChange(o.value)}
          className={cn('min-h-11 flex-1 rounded-xl px-2 text-sm font-bold transition', o.value === value ? 'bg-white text-navy-900 shadow-sm' : 'text-mute')}
        >
          {o.label}
          {o.count ? <span className="ml-1 text-leaf-700">({o.count})</span> : null}
        </button>
      ))}
    </div>
  )
}

export function LinkButton({ to, children, className }: { to: string; children: ReactNode; className?: string }) {
  return (
    <Link to={to} className={cn('inline-flex min-h-12 items-center justify-center rounded-xl bg-navy-900 px-5 font-semibold text-white', className)}>
      {children}
    </Link>
  )
}

export function KeyValue({ label, value, strong }: { label: ReactNode; value: ReactNode; strong?: boolean }) {
  return (
    <div className={cn('flex items-baseline justify-between gap-4 py-1.5', strong && 'text-base font-extrabold text-navy-900')}>
      <span className={strong ? '' : 'text-mute'}>{label}</span>
      <span className={cn('text-right', !strong && 'font-semibold text-ink')}>{value}</span>
    </div>
  )
}
