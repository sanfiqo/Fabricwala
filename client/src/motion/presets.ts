// The only place where durations and springs are defined. Screens must not invent their own.
// Animate transform and opacity only.
export const duration = {
  tap: 0.12,
  small: 0.2,
  sheet: 0.28,
  max: 0.35,
} as const

export const spring = {
  /** Buttons, chips, stepper. */
  snappy: { type: 'spring', stiffness: 520, damping: 34, mass: 0.8 },
  /** Sheets and cards. */
  soft: { type: 'spring', stiffness: 300, damping: 30, mass: 0.9 },
} as const

export const tapScale = 0.97
export const staggerStep = 0.04
export const staggerMax = 6

/** Fade and rise used for list items on first load. */
export const rise = {
  hidden: { opacity: 0, y: 10 },
  show: { opacity: 1, y: 0 },
}
/** Reduced motion: opacity only. */
export const fade = {
  hidden: { opacity: 0 },
  show: { opacity: 1 },
}
