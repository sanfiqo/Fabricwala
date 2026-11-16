import { useMemo, type ReactNode } from 'react'
import { Toast } from '@base-ui/react/toast'
import { CheckCircle2, XCircle } from 'lucide-react'
import { cn } from '@/lib/utils'

interface ToastApi {
  success: (text: string) => void
  error: (text: string) => void
  /** Toast with an Undo button. Per UX_RULES: undo instead of a confirm dialog (except pay, reject order, delete product). */
  undo: (text: string, undoLabel: string, onUndo: () => void) => void
}

function Viewport() {
  const { toasts } = Toast.useToastManager()
  return (
    <Toast.Portal>
      <Toast.Viewport className="pointer-events-none fixed inset-x-0 bottom-28 z-[60] mx-auto flex w-full max-w-md flex-col items-center gap-2 px-4">
        {toasts.map((t) => (
          <Toast.Root
            key={t.id}
            toast={t}
            swipeDirection={['down', 'left', 'right']}
            className={cn(
              'pointer-events-auto flex w-full items-center gap-3 rounded-2xl px-4 py-3 text-sm text-white shadow-nav',
              'transition-[opacity,transform] duration-200 [transform:translate3d(var(--toast-swipe-movement-x,0px),var(--toast-swipe-movement-y,0px),0)]',
              'data-starting-style:opacity-0 data-starting-style:[transform:translate3d(0,16px,0)] data-ending-style:opacity-0 data-ending-style:[transform:translate3d(0,16px,0)]',
              t.type === 'error' ? 'bg-error' : 'bg-ink',
            )}
          >
            {t.type === 'error' ? <XCircle className="size-5 shrink-0" aria-hidden /> : <CheckCircle2 className="size-5 shrink-0 text-saving-bg" aria-hidden />}
            <Toast.Title className="min-w-0 flex-1 font-medium" />
            <Toast.Action className="press min-h-11 shrink-0 rounded-full bg-white/15 px-4 text-sm font-medium text-white" />
          </Toast.Root>
        ))}
      </Toast.Viewport>
    </Toast.Portal>
  )
}

export function ToastProvider({ children }: { children: ReactNode }) {
  return (
    <Toast.Provider timeout={4000} limit={3}>
      {children}
      <Viewport />
    </Toast.Provider>
  )
}

export function useToast(): ToastApi {
  const { add, close } = Toast.useToastManager()
  return useMemo<ToastApi>(
    () => ({
      success: (title) => void add({ title, type: 'ok' }),
      error: (title) => void add({ title, type: 'error', timeout: 6000 }),
      undo: (title, undoLabel, onUndo) => {
        const id = add({
          title,
          type: 'ok',
          timeout: 6000,
          actionProps: {
            children: undoLabel,
            onClick: () => {
              onUndo()
              close(id)
            },
          },
        })
      },
    }),
    [add, close],
  )
}
