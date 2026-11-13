import { NavLink } from 'react-router-dom'
import type { LucideIcon } from 'lucide-react'
import { S } from '@/i18n'
import { cn } from '@/lib/utils'

export interface TabItem {
  to: string
  label: string
  icon: LucideIcon
  end?: boolean
  badge?: number
}

/** Floating pill menu. Content scrolls underneath it, so pages need bottom padding (pb-32). */
export function FloatingTabBar({ items, className }: { items: TabItem[]; className?: string }) {
  return (
    <nav
      aria-label={S.nav.main}
      className={cn('glass fixed bottom-[max(22px,env(safe-area-inset-bottom))] left-1/2 z-30 w-[min(calc(100%-48px),480px)] -translate-x-1/2 rounded-[34px] border border-white/90 p-1.5 shadow-nav', className)}
    >
      <ul className="grid gap-1" style={{ gridTemplateColumns: `repeat(${items.length}, minmax(0, 1fr))` }}>
        {items.map((i) => (
          <li key={i.to} className="min-w-0">
            <NavLink
              to={i.to}
              end={i.end}
              viewTransition
              className={({ isActive }) =>
                cn(
                  'press flex min-h-[52px] flex-col items-center justify-center gap-0.5 rounded-pill px-1 text-xs',
                  isActive ? 'bg-ink font-semibold text-background' : 'text-mute',
                )
              }
            >
              <span className="relative">
                <i.icon className="size-[22px]" aria-hidden />
                {!!i.badge && <span className="absolute -right-2 -top-1.5 flex min-w-4 items-center justify-center rounded-full bg-dot px-1 text-[11px] font-semibold leading-4 text-ink">{i.badge}</span>}
              </span>
              <span className="max-w-full truncate">{i.label}</span>
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  )
}
