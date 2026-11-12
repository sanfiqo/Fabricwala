import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from 'react'
import { CheckCircle2, XCircle } from 'lucide-react'
import { cn } from '@/lib/utils'

interface ToastItem {
  id: number
  text: string
  kind: 'ok' | 'error'
}
interface ToastApi {
  success: (text: string) => void
  error: (text: string) => void
}

const Ctx = createContext<ToastApi | null>(null)

export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([])
  const seq = useRef(0)
  const push = useCallback((text: string, kind: ToastItem['kind']) => {
    const id = ++seq.current
    setItems((x) => [...x, { id, text, kind }])
    setTimeout(() => setItems((x) => x.filter((i) => i.id !== id)), 3500)
  }, [])
  const api = useMemo<ToastApi>(() => ({ success: (t) => push(t, 'ok'), error: (t) => push(t, 'error') }), [push])
  return (
    <Ctx.Provider value={api}>
      {children}
      <div className="pointer-events-none fixed inset-x-0 bottom-24 z-[60] flex flex-col items-center gap-2 px-4" aria-live="polite">
        {items.map((i) => (
          <div key={i.id} className={cn('pointer-events-auto flex max-w-md items-center gap-2 rounded-xl px-4 py-3 text-sm font-semibold text-white shadow-lg', i.kind === 'ok' ? 'bg-navy-900' : 'bg-bad')}>
            {i.kind === 'ok' ? <CheckCircle2 className="size-5 text-leaf-100" /> : <XCircle className="size-5" />}
            {i.text}
          </div>
        ))}
      </div>
    </Ctx.Provider>
  )
}

export function useToast(): ToastApi {
  const v = useContext(Ctx)
  if (!v) throw new Error('ToastProvider missing')
  return v
}
