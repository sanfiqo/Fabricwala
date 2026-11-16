import { WifiOff } from 'lucide-react'
import { useOnline } from '@/hooks'
import { S } from '@/i18n'

/** Shown at the top whenever the phone has no internet. Cached screens keep working underneath. */
export function OfflineBanner() {
  const online = useOnline()
  if (online) return null
  return (
    <div role="status" className="sticky top-0 z-40 flex items-center justify-center gap-2 bg-ink px-4 py-2 text-center text-sm text-background">
      <WifiOff className="size-4 shrink-0" aria-hidden />
      {S.app.offline}
    </div>
  )
}
