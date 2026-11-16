import { Check, MapPin } from 'lucide-react'
import { Sheet } from '@/components/ui/misc'
import { useDeliveryAddress } from '@/hooks'
import { S } from '@/i18n'
import { cn } from '@/lib/utils'

/** "Prices landed in" picker. Choosing an address changes the city used for every landed price. */
export function AddressSheet({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { addr, addrs, choose } = useDeliveryAddress()
  return (
    <Sheet open={open} onClose={onClose} title={S.address.title}>
      <p className="mb-3 text-mute">{S.address.pick}</p>
      {addrs.length === 0 ? (
        <p className="py-4 text-center text-mute">{S.address.none}</p>
      ) : (
        <ul className="space-y-2">
          {addrs.map((a) => {
            const on = a.id === addr?.id
            return (
              <li key={a.id}>
                <button
                  type="button"
                  aria-pressed={on}
                  onClick={() => {
                    choose(a.id)
                    onClose()
                  }}
                  className={cn('press flex min-h-14 w-full items-center gap-3 rounded-2xl border p-3 text-left', on ? 'border-ink bg-soft' : 'border-line bg-surface')}
                >
                  <MapPin className="size-5 shrink-0" aria-hidden />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-medium">{a.cityName} {a.pincode}</span>
                    <span className="block truncate text-sm text-mute">{a.line1}</span>
                  </span>
                  {on && (
                    <span className="flex shrink-0 items-center gap-1 text-sm font-medium">
                      <Check className="size-4" aria-hidden />
                      {S.address.selected}
                    </span>
                  )}
                </button>
              </li>
            )
          })}
        </ul>
      )}
      <p className="mt-4 text-sm text-mute">{S.address.addNote}</p>
    </Sheet>
  )
}
