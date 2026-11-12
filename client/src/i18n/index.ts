import { en } from './en'

// Swap this for a locale lookup when Hindi/Urdu files exist (see DECISIONS.md).
export const S = en

/** Replace {name} placeholders. */
export function fmt(template: string, vars: Record<string, string | number>): string {
  return template.replace(/\{(\w+)\}/g, (_, k: string) => String(vars[k] ?? ''))
}

/** Translated label for an enum-like server value; falls back to the raw value. */
export function label(group: Record<string, string>, value: string | undefined | null): string {
  if (!value) return ''
  return group[value] ?? value
}
