import { Link, useViewTransitionState } from 'react-router-dom'
import type { ProductSummary } from '@/api/types'
import { Appear } from '@/motion'
import { fmt, S } from '@/i18n'
import { money } from '@/lib/utils'

/** Soft grey card: photo with a soft shadow under it, seller, name, price and minimum order. */
export function ProductCard({ p, index = 0 }: { p: ProductSummary; index?: number }) {
  const to = `/shop/p/${p.slug}`
  // Only the card being opened shares its photo with the product page (names must be unique on a page).
  const morphing = useViewTransitionState(to)
  return (
    <Appear index={index}>
      <Link to={to} viewTransition className="press @container flex flex-col gap-0.5 rounded-card bg-soft px-3 pb-3.5 pt-3 text-ink">
        <span className="photo-shadow mb-2 flex h-[138px] items-center justify-center">
          {p.imageUrl ? (
            <img
              src={p.imageUrl}
              alt=""
              loading="lazy"
              decoding="async"
              width={138}
              height={138}
              style={morphing ? { viewTransitionName: 'product-photo' } : undefined}
              className="relative z-[1] size-full object-contain mix-blend-multiply"
            />
          ) : null}
        </span>
        <span className="truncate text-xs text-mute">{p.sellerName}</span>
        <span className="line-clamp-2 min-h-10 text-sm font-medium leading-5">{p.title}</span>
        <span className="flex flex-wrap items-baseline justify-between gap-x-2">
          <span className="text-[15px] font-semibold tabular-nums">{money(p.priceFrom)}</span>
          <span className="text-xs text-mute">{fmt(S.shop.moqShort, { n: p.moq, unit: S.units[p.unit] })}</span>
        </span>
      </Link>
    </Appear>
  )
}

export function ProductCardSkeleton() {
  return (
    <div aria-hidden className="rounded-card bg-soft px-3 pb-3.5 pt-3">
      <div className="skeleton mb-2 h-[138px] !bg-line/60" />
      <div className="skeleton mb-1.5 h-3 w-1/2 !bg-line/60" />
      <div className="skeleton mb-1.5 h-4 w-4/5 !bg-line/60" />
      <div className="skeleton h-4 w-2/5 !bg-line/60" />
    </div>
  )
}
