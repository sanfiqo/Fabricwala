import { useEffect } from 'react'

// Typed-in form data survives a language switch and an accidental reload.
// It lives in sessionStorage (this tab only) and is removed when the form is sent.
export function loadDraft<T extends object>(key: string): Partial<T> {
  try {
    return JSON.parse(sessionStorage.getItem(`tc_draft_${key}`) ?? '{}') as Partial<T>
  } catch {
    return {}
  }
}

export function clearDraft(key: string) {
  try {
    sessionStorage.removeItem(`tc_draft_${key}`)
  } catch {
    /* nothing to clear */
  }
}

/** Saves `values` whenever they change. Never pass passwords or one-time codes. */
export function useSaveDraft(key: string, values: object) {
  const json = JSON.stringify(values)
  useEffect(() => {
    try {
      sessionStorage.setItem(`tc_draft_${key}`, json)
    } catch {
      /* storage blocked: no draft */
    }
  }, [key, json])
}
