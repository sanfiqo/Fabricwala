import type { PriceSlab } from '@/api/types'
import { fmt, S } from '@/i18n'

/** A slab row as typed in the editor (strings, so empty fields are possible). */
export interface SlabRow {
  minQty: string
  maxQty: string
  price: string
}

export interface SlabCheck {
  /** Messages to show under the table. */
  errors: string[]
  /** Indexes (in `sorted`) of rows that have a problem, for highlighting. */
  badRows: Set<number>
  /** Rows sorted by minQty. */
  sorted: SlabRow[]
}

const num = (s: string) => (s.trim() === '' ? null : Number(s))

/**
 * Checks price slabs: first row starts at the MOQ, no overlaps, no gaps,
 * only the last row is open ended, every row has a price.
 */
export function checkSlabs(moq: number, rows: SlabRow[]): SlabCheck {
  const errors: string[] = []
  const badRows = new Set<number>()
  const sorted = [...rows].sort((a, b) => (num(a.minQty) ?? 0) - (num(b.minQty) ?? 0))
  const bad = (i: number, msg: string) => {
    badRows.add(i)
    if (!errors.includes(msg)) errors.push(msg)
  }

  if (sorted.length === 0) return { errors: [S.seller.slabNone], badRows, sorted }

  sorted.forEach((r, i) => {
    const min = num(r.minQty)
    const max = num(r.maxQty)
    const price = num(r.price)
    const n = i + 1
    if (price === null || !(price > 0)) bad(i, fmt(S.seller.slabPrice, { a: n }))
    if (min === null || min < 1) bad(i, `${S.reg.required} (${n})`)
    if (min !== null && max !== null && max < min) bad(i, fmt(S.seller.slabMax, { a: n }))
  })

  if (num(sorted[0].minQty) !== moq) bad(0, fmt(S.seller.slabFirst, { moq }))

  for (let i = 0; i < sorted.length - 1; i++) {
    const max = num(sorted[i].maxQty)
    const nextMin = num(sorted[i + 1].minQty)
    if (max === null) bad(i, S.seller.slabOpen)
    else if (nextMin !== null) {
      if (nextMin <= max) {
        bad(i, fmt(S.seller.slabOverlap, { a: i + 1, b: i + 2 }))
        bad(i + 1, fmt(S.seller.slabOverlap, { a: i + 1, b: i + 2 }))
      } else if (nextMin > max + 1) {
        bad(i, fmt(S.seller.slabGap, { a: i + 1, b: i + 2 }))
        bad(i + 1, fmt(S.seller.slabGap, { a: i + 1, b: i + 2 }))
      }
    }
  }
  const last = sorted.length - 1
  if (num(sorted[last].maxQty) !== null) bad(last, S.seller.slabLast)

  return { errors, badRows, sorted }
}

export const rowsFromSlabs = (slabs: PriceSlab[]): SlabRow[] =>
  slabs.map((s) => ({ minQty: String(s.minQty), maxQty: s.maxQty === null ? '' : String(s.maxQty), price: String(s.pricePerUnit) }))

export const slabsFromRows = (rows: SlabRow[]): PriceSlab[] =>
  rows.map((r) => ({ minQty: Number(r.minQty), maxQty: r.maxQty.trim() === '' ? null : Number(r.maxQty), pricePerUnit: Number(r.price) }))
