import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

/** Bottom bar with the one main action of a screen. White, hairline on top, sits above the content. */
export function StickyActionBar({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div className={cn('safe-b fixed inset-x-0 bottom-0 z-30 border-t border-line bg-surface shadow-bar', className)}>
      <div className="mx-auto flex max-w-xl flex-col gap-2.5 px-4 pb-4 pt-3">{children}</div>
    </div>
  )
}
