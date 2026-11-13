import { Check } from 'lucide-react'
import type { Variant } from '@/api/types'
import { fmt, S } from '@/i18n'
import { swatchFor } from '@/lib/colours'
import { cn } from '@/lib/utils'

interface Props {
  variants: Variant[]
  qtys: Record<number, number>
  activeId: number | undefined
  onPick: (id: number) => void
}

/** Colour swatches, then the sizes of that colour. Tapping a size makes it the one the quantity bar edits. */
export function VariantPicker({ variants, qtys, activeId, onPick }: Props) {
  const active = variants.find((v) => v.id === activeId) ?? variants[0]
  if (!active) return null
  const colours = [...new Set(variants.map((v) => v.colour))]
  const sizes = variants.filter((v) => v.colour === active.colour)
  const colourQty = (c: string) => variants.filter((v) => v.colour === c).reduce((a, v) => a + (qtys[v.id] ?? 0), 0)

  return (
    <div className="space-y-5">
      <div>
        <p className="mb-2 text-[13px] text-mute">{fmt(S.product.colourLabel, { name: active.colour })}</p>
        <div className="flex flex-wrap gap-3">
          {colours.map((c) => {
            const on = c === active.colour
            const hex = swatchFor(c)
            const qty = colourQty(c)
            const first = variants.find((v) => v.colour === c && v.stockQty > 0) ?? variants.find((v) => v.colour === c)
            return (
              <button
                key={c}
                type="button"
                aria-label={qty ? `${c} · ${fmt(S.product.inCart, { n: qty })}` : c}
                aria-pressed={on}
                onClick={() => first && onPick(first.id)}
                style={hex ? { backgroundColor: hex } : undefined}
                className={cn(
                  'press relative flex size-11 items-center justify-center rounded-full text-sm font-medium',
                  !hex && 'bg-soft text-ink',
                  on && 'ring-2 ring-ink ring-offset-2 ring-offset-background',
                )}
              >
                {!hex && c.charAt(0).toUpperCase()}
                {qty > 0 && (
                  <span className="absolute -right-1 -top-1 flex size-5 items-center justify-center rounded-full bg-ink text-background">
                    <Check className="size-3" aria-hidden />
                  </span>
                )}
              </button>
            )
          })}
        </div>
      </div>

      <div>
        <p className="mb-2 text-[13px] text-mute">{S.product.sizeLabel}</p>
        <div className="flex flex-wrap gap-2">
          {sizes.map((v) => {
            const on = v.id === active.id
            const q = qtys[v.id] ?? 0
            return (
              <button
                key={v.id}
                type="button"
                disabled={v.stockQty <= 0}
                aria-pressed={on}
                onClick={() => onPick(v.id)}
                className={cn(
                  'press inline-flex min-h-11 min-w-12 items-center justify-center gap-1.5 rounded-chip border px-4 text-sm disabled:opacity-40',
                  on ? 'border-ink bg-ink text-background' : 'border-line bg-surface text-ink',
                )}
              >
                {v.size}
                {q > 0 && <span className={cn('rounded-full px-1.5 text-xs', on ? 'bg-background/20' : 'bg-soft')}>{q}</span>}
              </button>
            )
          })}
        </div>
        <p className={cn('mt-2 text-sm', active.stockQty > 0 ? 'text-mute' : 'text-error')}>
          {active.stockQty > 0 ? fmt(S.product.inStock, { n: active.stockQty }) : S.product.outOfStock}
        </p>
      </div>
    </div>
  )
}
