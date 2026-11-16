import type { CSSProperties } from 'react'
import { Link, useViewTransitionState } from 'react-router-dom'
import type { ProductSummary } from '@/api/types'
import { ProductImage } from '@/components/ProductImage'
import { Appear } from '@/motion'
import { fmt, S } from '@/i18n'
import { usePhoto } from '@/lib/photos'
import { money } from '@/lib/utils'

/** Soft grey card: rounded photo with a soft shadow under it, seller, name, price and minimum order. */
export function ProductCard({ p, index = 0 }: { p: ProductSummary; index?: number }) {
  const to = `/shop/p/${p.slug}`
  // Only the card being opened shares its photo with the product page (names must be unique on a page).
  const morphing = useViewTransitionState(to)
  const photo = usePhoto(p.id, 1, p.imageUrl)
  return (
    <Appear index={index}>
      <Link to={to} viewTransition className="press @container flex flex-col gap-0.5 rounded-card bg-soft px-3 pb-3.5 pt-3 text-ink">
        <span className="photo-slot mb-2 h-[138px] items-start" style={{ '--shadow-bottom': '3px', '--shadow-width': '56%' } as CSSProperties}>
          <ProductImage
            src={photo}
            lqip={p.imageLqip}
            alt=""
            width={400}
            height={400}
            sizes="(min-width: 576px) 270px, 45vw"
            className="relative z-[1] h-[122px] w-full rounded-[18px]"
            style={morphing ? { viewTransitionName: 'product-photo' } : undefined}
          />
        </span>
        <span className="truncate text-xs text-mute">{p.sellerName}</span>
        <span className="line-clamp-2 min-h-10 break-words text-sm font-medium leading-5">{p.title}</span>
        <span className="flex flex-wrap items-baseline justify-between gap-x-2">
          <span className="text-[15px] font-semibold tabular-nums">{money(p.priceFrom)}</span>
          <span className="text-xs text-mute">{fmt(S.shop.moqShort, { n: p.moq, unit: S.unitsShort[p.unit] })}</span>
        </span>
      </Link>
    </Appear>
  )
}

/** Same size and spacing as the real card, so nothing jumps when the data arrives. */
export function ProductCardSkeleton() {
  return (
    <div aria-hidden className="rounded-card bg-soft px-3 pb-3.5 pt-3">
      <div className="skeleton mb-2 h-[122px] rounded-[18px] !bg-line/60" />
      <div className="h-4" />
      <div className="skeleton mb-1.5 h-3 w-1/2 rounded-full !bg-line/60" />
      <div className="skeleton mb-1.5 h-4 w-4/5 rounded-full !bg-line/60" />
      <div className="skeleton h-4 w-2/5 rounded-full !bg-line/60" />
    </div>
  )
}
