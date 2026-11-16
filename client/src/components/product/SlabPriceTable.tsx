import type { PriceSlab, Unit } from '@/api/types'
import { S } from '@/i18n'
import { cn, money } from '@/lib/utils'

const slabLabel = (s: PriceSlab, unit: Unit) => (s.maxQty === null ? `${s.minQty}+ ${S.unitsShort[unit]}` : `${s.minQty}–${s.maxQty} ${S.unitsShort[unit]}`)

/** "Buy more, pay less". The slab for the current quantity is dark. Wraps to more rows when there are many slabs. */
export function SlabPriceTable({ slabs, unit, qty }: { slabs: PriceSlab[]; unit: Unit; qty: number }) {
  return (
    <div>
      <p className="mb-2 text-[13px] text-mute">{S.product.slabsHint}</p>
      <ul className="grid grid-cols-3 gap-px overflow-hidden rounded-[14px] border border-line bg-line text-center">
        {slabs.map((s) => {
          const on = qty > 0 && qty >= s.minQty && (s.maxQty === null || qty <= s.maxQty)
          return (
            <li key={s.minQty} aria-current={on || undefined} className={cn('flex min-w-0 flex-col px-1 py-2.5', on ? 'bg-ink text-background' : 'bg-surface')}>
              <span className={cn('break-words text-xs', on ? 'text-line' : 'text-mute')}>{slabLabel(s, unit)}</span>
              <span className="text-base font-semibold tabular-nums">{money(s.pricePerUnit)}</span>
            </li>
          )
        })}
      </ul>
    </div>
  )
}
