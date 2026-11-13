import { useSyncExternalStore } from 'react'
import { en, type Dict } from './en'
import { hi } from './hi'

export type Lang = 'en' | 'hi'
export const LANGS: Lang[] = ['en', 'hi']
const dicts: Record<Lang, Dict> = { en, hi }
const KEY = 'tc_lang'

function initial(): Lang {
  try {
    const saved = localStorage.getItem(KEY)
    if (saved === 'en' || saved === 'hi') return saved
  } catch {
    /* storage blocked */
  }
  return typeof navigator !== 'undefined' && navigator.language?.toLowerCase().startsWith('hi') ? 'hi' : 'en'
}

let current: Lang = initial()
const listeners = new Set<() => void>()
if (typeof document !== 'undefined') document.documentElement.lang = current

/** Current text. Always reads the active language, so `S.group.key` keeps working everywhere. */
export const S: Dict = new Proxy({} as Dict, {
  get: (_t, key) => dicts[current][key as keyof Dict],
  has: (_t, key) => key in dicts[current],
  ownKeys: () => Reflect.ownKeys(dicts[current]),
  getOwnPropertyDescriptor: (_t, key) => ({ enumerable: true, configurable: true, value: dicts[current][key as keyof Dict] }),
})

export function getLang(): Lang {
  return current
}

export function setLang(lang: Lang) {
  if (lang === current) return
  current = lang
  try {
    localStorage.setItem(KEY, lang)
  } catch {
    /* storage blocked */
  }
  document.documentElement.lang = lang
  listeners.forEach((l) => l())
}

/** Re-renders when the language changes. The app root also re-mounts on change (see main.tsx). */
export function useLang(): Lang {
  return useSyncExternalStore(
    (cb) => {
      listeners.add(cb)
      return () => listeners.delete(cb)
    },
    () => current,
  )
}

/** Replace {name} placeholders. */
export function fmt(template: string, vars: Record<string, string | number>): string {
  return template.replace(/\{(\w+)\}/g, (_, k: string) => String(vars[k] ?? ''))
}

/** Translated label for an enum-like server value; falls back to the raw value. */
export function label(group: Record<string, string>, value: string | undefined | null): string {
  if (!value) return ''
  return group[value] ?? value
}
