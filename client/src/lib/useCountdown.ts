import { useEffect, useState } from 'react'

/** Time left until `deadline` as "3h 40m" (or "12m"). `text` is null when there is no deadline. `over` is true once it has passed. */
export function useCountdown(deadline: string | null | undefined) {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const t = setInterval(() => setNow(Date.now()), 30_000)
    return () => clearInterval(t)
  }, [])
  if (!deadline) return { text: null as string | null, over: false }
  const ms = new Date(deadline).getTime() - now
  if (ms <= 0) return { text: null as string | null, over: true }
  const m = Math.floor(ms / 60_000)
  return { text: m >= 60 ? `${Math.floor(m / 60)}h ${m % 60}m` : `${m}m`, over: false }
}
