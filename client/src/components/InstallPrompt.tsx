import { useEffect, useState } from 'react'
import { Download } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { S } from '@/i18n'

interface InstallEvent extends Event {
  prompt: () => Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}
const KEY = 'tc_install_dismissed'
const WEEK = 7 * 24 * 60 * 60 * 1000

const recentlyDismissed = () => {
  try {
    return Date.now() - Number(localStorage.getItem(KEY) ?? 0) < WEEK
  } catch {
    return false
  }
}
const installed = () => window.matchMedia('(display-mode: standalone)').matches

/** "Add to home screen" card. Appears only when the browser says the app can be installed (Chrome/Edge on Android). */
export function InstallPrompt() {
  const [evt, setEvt] = useState<InstallEvent | null>(null)

  useEffect(() => {
    if (installed() || recentlyDismissed()) return
    const on = (e: Event) => {
      e.preventDefault()
      setEvt(e as InstallEvent)
    }
    window.addEventListener('beforeinstallprompt', on)
    return () => window.removeEventListener('beforeinstallprompt', on)
  }, [])

  if (!evt) return null
  const later = () => {
    try {
      localStorage.setItem(KEY, String(Date.now()))
    } catch {
      /* storage blocked */
    }
    setEvt(null)
  }
  const install = async () => {
    await evt.prompt()
    await evt.userChoice.catch(() => undefined)
    setEvt(null)
  }

  return (
    <div className="mx-5 mt-3 rounded-[20px] bg-soft p-4">
      <div className="flex items-start gap-3">
        <span className="flex size-11 shrink-0 items-center justify-center rounded-full bg-surface" aria-hidden>
          <Download className="size-5" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="font-medium">{S.install.title}</p>
          <p className="text-sm text-mute">{S.install.sub}</p>
        </div>
      </div>
      <div className="mt-3 flex gap-2">
        <Button size="sm" onClick={() => void install()}>{S.install.action}</Button>
        <Button size="sm" variant="ghost" onClick={later}>{S.install.later}</Button>
      </div>
    </div>
  )
}
