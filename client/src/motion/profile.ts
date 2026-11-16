// Device profile: weak phones get the "lite" motion profile (no stagger, no blur, shorter durations).
interface NavigatorHints {
  deviceMemory?: number
  hardwareConcurrency?: number
}

export function isLiteDevice(): boolean {
  if (typeof navigator === 'undefined') return false
  const n = navigator as Navigator & NavigatorHints
  if (n.deviceMemory !== undefined && n.deviceMemory <= 2) return true
  if (n.hardwareConcurrency !== undefined && n.hardwareConcurrency <= 4) return true
  return false
}

/** Call once at start. CSS reads `html[data-perf="lite"]`. */
export function initPerfProfile() {
  const forced = new URLSearchParams(window.location.search).get('perf') // ?perf=lite to test
  const lite = forced ? forced === 'lite' : isLiteDevice()
  document.documentElement.dataset.perf = lite ? 'lite' : 'full'
}

export const prefersReducedMotion = () => typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches
