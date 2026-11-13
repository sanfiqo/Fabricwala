import { useSyncExternalStore, type ReactNode } from 'react'
import { Drawer, DrawerContent, DrawerHeader, DrawerTitle } from './drawer'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from './dialog'

const WIDE = '(min-width: 768px)'
const subscribe = (cb: () => void) => {
  const mq = window.matchMedia(WIDE)
  mq.addEventListener('change', cb)
  return () => mq.removeEventListener('change', cb)
}
const useWide = () =>
  useSyncExternalStore(
    subscribe,
    () => window.matchMedia(WIDE).matches,
    () => false,
  )

interface Props {
  open: boolean
  onClose: () => void
  title: string
  children: ReactNode
}

/** Drawer (swipe down to close) on phones, Dialog on wide screens. Lives in its own chunk. */
export default function SheetImpl({ open, onClose, title, children }: Props) {
  const wide = useWide()
  const onOpenChange = (o: boolean) => {
    if (!o) onClose()
  }

  if (wide) {
    return (
      <Dialog open={open} onOpenChange={onOpenChange}>
        <DialogContent className="max-h-[85dvh] overflow-y-auto rounded-card sm:max-w-md" showCloseButton>
          <DialogHeader>
            <DialogTitle className="text-lg font-semibold">{title}</DialogTitle>
          </DialogHeader>
          {children}
        </DialogContent>
      </Dialog>
    )
  }

  return (
    <Drawer open={open} onOpenChange={onOpenChange} swipeDirection="down" showSwipeHandle>
      <DrawerContent className="rounded-t-[28px] bg-surface">
        <DrawerHeader className="pb-2 pt-1 text-left">
          <DrawerTitle className="text-lg font-semibold">{title}</DrawerTitle>
        </DrawerHeader>
        <div className="safe-b max-h-[78dvh] overflow-y-auto px-5 pb-6">{children}</div>
      </DrawerContent>
    </Drawer>
  )
}
