import { useEffect, useState } from 'react'
import { registerSW } from 'virtual:pwa-register'
import { RefreshCw } from 'lucide-react'
import { S } from '@/i18n'

/** "New version ready. Tap to refresh." Shown when the service worker has a newer app waiting. Never reloads by itself. */
export function UpdatePrompt() {
  const [update, setUpdate] = useState<null | (() => Promise<void>)>(null)
  useEffect(() => {
    const updateSW = registerSW({ onNeedRefresh: () => setUpdate(() => () => updateSW(true)) })
  }, [])
  if (!update) return null
  return (
    <button
      type="button"
      onClick={() => void update()}
      className="press fixed inset-x-4 top-3 z-[70] mx-auto flex min-h-12 max-w-md items-center gap-3 rounded-2xl bg-ink px-4 py-2 text-left text-background shadow-nav"
    >
      <RefreshCw className="size-5 shrink-0" aria-hidden />
      <span className="flex min-w-0 flex-1 flex-col leading-tight">
        <span className="font-medium">{S.app.updateReady}</span>
        <span className="text-sm text-line">{S.app.updateTap}</span>
      </span>
    </button>
  )
}
