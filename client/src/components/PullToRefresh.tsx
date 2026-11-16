import { useEffect, useRef, useState } from 'react'
import { Loader2 } from 'lucide-react'
import { S } from '@/i18n'
import { haptic } from '@/motion'

const THRESHOLD = 72

/**
 * Pull down at the top of the page to refresh. Touch only. The pill moves with `transform`, nothing else animates.
 * The page itself does not move, so nothing jumps.
 */
export function PullToRefresh({ onRefresh }: { onRefresh: () => Promise<unknown> }) {
  const [dist, setDist] = useState(0)
  const [busy, setBusy] = useState(false)
  const start = useRef<number | null>(null)
  const latest = useRef(0)
  const refresh = useRef(onRefresh)
  useEffect(() => {
    refresh.current = onRefresh
  })

  useEffect(() => {
    const down = (e: TouchEvent) => {
      start.current = window.scrollY <= 0 ? e.touches[0].clientY : null
    }
    const move = (e: TouchEvent) => {
      if (start.current === null) return
      const dy = e.touches[0].clientY - start.current
      if (dy <= 0 || window.scrollY > 0) {
        latest.current = 0
        setDist(0)
        return
      }
      latest.current = Math.min(dy * 0.5, 110)
      setDist(latest.current)
    }
    const up = () => {
      const pulled = latest.current
      start.current = null
      latest.current = 0
      if (pulled >= THRESHOLD) {
        haptic()
        setBusy(true)
        setDist(THRESHOLD * 0.7)
        const started = Date.now()
        void Promise.resolve(refresh.current()).finally(() => {
          window.setTimeout(() => {
            setBusy(false)
            setDist(0)
          }, Math.max(0, 600 - (Date.now() - started)))
        })
      } else {
        setDist(0)
      }
    }
    document.addEventListener('touchstart', down, { passive: true })
    document.addEventListener('touchmove', move, { passive: true })
    document.addEventListener('touchend', up)
    document.addEventListener('touchcancel', up)
    return () => {
      document.removeEventListener('touchstart', down)
      document.removeEventListener('touchmove', move)
      document.removeEventListener('touchend', up)
      document.removeEventListener('touchcancel', up)
    }
  }, [])

  const ready = dist >= THRESHOLD
  return (
    <div
      aria-live="polite"
      className="pointer-events-none fixed inset-x-0 top-0 z-40 flex justify-center"
      style={{ transform: `translate3d(0, ${dist - 56}px, 0)`, opacity: dist > 4 || busy ? 1 : 0, transition: dist === 0 || busy ? 'transform 200ms ease-out, opacity 200ms' : 'none' }}
    >
      <span className="mt-[max(12px,env(safe-area-inset-top))] flex items-center gap-2 rounded-full bg-ink px-4 py-2 text-sm text-background shadow-nav">
        {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
        {busy ? S.pull.loading : ready ? S.pull.release : S.pull.pull}
      </span>
    </div>
  )
}
