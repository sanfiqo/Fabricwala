import { useEffect, useRef, type ReactNode } from 'react'
import { LazyMotion, MotionConfig, m, useReducedMotion } from 'motion/react'
import { duration, fade, rise, spring, staggerMax, staggerStep } from './presets'
import { isLiteDevice } from './profile'

export * from './presets'
export { initPerfProfile } from './profile'

const loadFeatures = () => import('./features').then((r) => r.default)

export function MotionRoot({ children }: { children: ReactNode }) {
  return (
    <LazyMotion features={loadFeatures}>
      <MotionConfig reducedMotion="user" transition={spring.soft}>
        {children}
      </MotionConfig>
    </LazyMotion>
  )
}

/** reduced = user asked for less motion (opacity only). lite = weak device (no stagger, shorter). */
export function useMotion() {
  const reduced = !!useReducedMotion()
  const lite = isLiteDevice()
  return { reduced, lite, still: reduced || lite }
}

/** Fade-and-rise on first load. Pass the item index; only the first few are staggered. */
export function Appear({ children, index = 0, className }: { children: ReactNode; index?: number; className?: string }) {
  const { reduced, lite } = useMotion()
  const delay = lite || reduced ? 0 : Math.min(index, staggerMax) * staggerStep
  return (
    <m.div
      className={className}
      variants={reduced ? fade : rise}
      initial="hidden"
      animate="show"
      transition={{ duration: lite ? duration.tap : duration.small, delay, ease: 'easeOut' }}
    >
      {children}
    </m.div>
  )
}

/** Number that counts to its new value in 200ms. Plain rAF tween, so it adds nothing to the bundle. */
export function CountUp({ value, format }: { value: number; format: (n: number) => string }) {
  const ref = useRef<HTMLSpanElement>(null)
  const from = useRef(value)
  const { still } = useMotion()
  useEffect(() => {
    const el = ref.current
    const start = from.current
    from.current = value
    if (!el) return
    if (still || start === value) {
      el.textContent = format(value)
      return
    }
    const t0 = performance.now()
    let raf = 0
    const tick = (now: number) => {
      const p = Math.min(1, (now - t0) / (duration.small * 1000))
      const eased = 1 - (1 - p) * (1 - p)
      el.textContent = format(Math.round(start + (value - start) * eased))
      if (p < 1) raf = requestAnimationFrame(tick)
    }
    raf = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(raf)
  }, [value, format, still])
  return <span ref={ref}>{format(value)}</span>
}

/** Small haptic tick on Android. No-op elsewhere. */
export function haptic(ms = 10) {
  try {
    navigator.vibrate?.(ms)
  } catch {
    /* not supported */
  }
}
