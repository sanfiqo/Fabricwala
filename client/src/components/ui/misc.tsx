import { lazy, Suspense, useState, type HTMLAttributes, type ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ArrowLeft, Loader2 } from 'lucide-react'
import { cn } from '@/lib/utils'
import { S } from '@/i18n'
import { Button } from './button'

export function Card({ className, ...p }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('rounded-card border border-line bg-surface', className)} {...p} />
}

/** Grey card, used for product tiles and cart rows. */
export function SoftCard({ className, ...p }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('rounded-card bg-soft', className)} {...p} />
}

const tones = {
  neutral: 'bg-soft text-ink',
  good: 'bg-leaf-100 text-success',
  warn: 'bg-saving-bg text-saving',
  bad: 'bg-error-bg text-error',
} as const
export type Tone = keyof typeof tones
export function Badge({ tone = 'neutral', className, ...p }: HTMLAttributes<HTMLSpanElement> & { tone?: Tone }) {
  return <span className={cn('inline-flex items-center rounded-full px-3 py-1 text-xs font-medium', tones[tone], className)} {...p} />
}

/** Amber chip. Only for savings and warnings, never decoration. */
export function SavingChip({ className, ...p }: HTMLAttributes<HTMLSpanElement>) {
  return <span className={cn('inline-flex w-fit max-w-full items-center rounded-xl bg-saving-bg px-2.5 py-1 text-xs leading-snug text-saving', className)} {...p} />
}

/** Small tappable pill. 44px high so a thumb can hit it. */
export function Chip({ className, active, ...p }: React.ButtonHTMLAttributes<HTMLButtonElement> & { active?: boolean }) {
  return (
    <button
      type="button"
      aria-pressed={active}
      className={cn(
        'press inline-flex min-h-11 min-w-12 items-center justify-center rounded-chip px-4 text-sm disabled:opacity-40',
        active ? 'bg-ink text-background' : 'border border-line bg-surface text-ink',
        className,
      )}
      {...p}
    />
  )
}

export function Spinner({ className }: { className?: string }) {
  return <Loader2 aria-label={S.app.loading} className={cn('size-6 animate-spin text-mute', className)} />
}

/** Placeholder block. Use several of these instead of a spinner. */
export function Skeleton({ className }: { className?: string }) {
  return <div aria-hidden className={cn('skeleton rounded-2xl', className)} />
}

/** Generic page skeleton. Screens with a known shape should build their own from <Skeleton />. */
export function PageLoading() {
  return (
    <div role="status" aria-label={S.app.loading} className="space-y-3 px-5 pt-6">
      <Skeleton className="h-8 w-2/3" />
      <Skeleton className="h-24" />
      <Skeleton className="h-24" />
      <Skeleton className="h-24" />
    </div>
  )
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <div role="alert" className="px-5 py-12 text-center">
      <p className="font-medium text-error">{error instanceof Error ? error.message : S.app.error}</p>
      {onRetry && (
        <Button variant="soft" size="sm" className="mt-4" onClick={onRetry}>
          {S.app.retry}
        </Button>
      )}
    </div>
  )
}

export function EmptyState({ icon, title, sub, action }: { icon?: ReactNode; title: string; sub?: string; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-center px-6 py-14 text-center">
      {icon && <div className="mb-4 flex size-16 items-center justify-center rounded-full bg-soft text-ink">{icon}</div>}
      <p className="text-lg font-semibold">{title}</p>
      {sub && <p className="mt-1 max-w-xs text-mute">{sub}</p>}
      {action && <div className="mt-5">{action}</div>}
    </div>
  )
}

/** Round back button (48px). aria-label is required because it has no text. */
export function BackButton({ to, className }: { to?: string; className?: string }) {
  const nav = useNavigate()
  const cls = cn('press flex size-12 shrink-0 items-center justify-center rounded-full bg-soft text-ink', className)
  if (to) {
    return (
      <Link to={to} viewTransition aria-label={S.app.back} className={cls}>
        <ArrowLeft className="size-[22px]" aria-hidden />
      </Link>
    )
  }
  return (
    <button type="button" aria-label={S.app.back} onClick={() => nav(-1)} className={cls}>
      <ArrowLeft className="size-[22px]" aria-hidden />
    </button>
  )
}

/** Page header: optional round back button, quiet title, optional action on the right. */
export function PageHeader({ title, back, action }: { title: string; back?: string | true; action?: ReactNode }) {
  return (
    <div className="px-4 pt-4">
      <div className="flex min-h-12 items-center gap-3">
        {back && <BackButton to={back === true ? undefined : back} />}
        <h1 className="min-w-0 flex-1 text-balance text-xl font-semibold tracking-tight">{title}</h1>
        {action}
      </div>
    </div>
  )
}

export function SectionTitle({ children, action }: { children: ReactNode; action?: ReactNode }) {
  return (
    <div className="mb-2 mt-6 flex items-center justify-between gap-3">
      <h2 className="text-base font-semibold">{children}</h2>
      {action}
    </div>
  )
}

interface SheetProps {
  open: boolean
  onClose: () => void
  title: string
  children: ReactNode
}
const SheetImpl = lazy(() => import('./sheet-impl'))

/** Bottom sheet on phones (swipe down to close), dialog on wide screens. Loaded only when first opened. */
export function Sheet(props: SheetProps) {
  const [mounted, setMounted] = useState(props.open)
  if (props.open && !mounted) setMounted(true)
  if (!mounted) return null
  return (
    <Suspense fallback={null}>
      <SheetImpl {...props} />
    </Suspense>
  )
}

/** Pill tabs / segmented control. */
export function Segmented<T extends string>({ value, onChange, options }: { value: T; onChange: (v: T) => void; options: Array<{ value: T; label: string; count?: number }> }) {
  return (
    <div role="tablist" className="flex gap-1 rounded-pill bg-soft p-1">
      {options.map((o) => (
        <button
          key={o.value}
          role="tab"
          aria-selected={o.value === value}
          type="button"
          onClick={() => onChange(o.value)}
          className={cn('press min-h-11 flex-1 rounded-pill px-2 text-sm font-medium', o.value === value ? 'bg-surface shadow-sm' : 'text-mute')}
        >
          {o.label}
          {o.count ? <span className="ml-1 text-mute">({o.count})</span> : null}
        </button>
      ))}
    </div>
  )
}

export function LinkButton({ to, children, className }: { to: string; children: ReactNode; className?: string }) {
  return (
    <Link to={to} viewTransition className={cn('press inline-flex min-h-12 items-center justify-center rounded-pill bg-ink px-6 font-medium text-background', className)}>
      {children}
    </Link>
  )
}

export function KeyValue({ label, value, strong }: { label: ReactNode; value: ReactNode; strong?: boolean }) {
  return (
    <div className={cn('flex items-baseline justify-between gap-4 py-2', strong && 'text-base font-semibold')}>
      <span className={strong ? '' : 'text-mute'}>{label}</span>
      <span className={cn('text-right', !strong && 'font-medium')}>{value}</span>
    </div>
  )
}
