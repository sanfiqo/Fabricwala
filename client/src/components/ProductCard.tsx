import { Link } from 'react-router-dom'
import type { ProductSummary } from '@/api/types'
import { fmt, S } from '@/i18n'
import { money } from '@/lib/utils'

export function ProductCard({ p }: { p: ProductSummary }) {
  return (
    <Link to={`/shop/p/${p.slug}`} className="group block overflow-hidden rounded-2xl border border-line bg-white active:bg-navy-50">
      <div className="aspect-square bg-navy-50">
        {p.imageUrl && <img src={p.imageUrl} alt={p.title} loading="lazy" className="size-full object-cover" />}
      </div>
      <div className="space-y-1 p-3">
        <p className="line-clamp-2 min-h-10 text-sm font-bold leading-5 text-navy-900">{p.title}</p>
        <p className="text-lg font-extrabold text-navy-900">
          <span className="text-xs font-semibold text-mute">{S.shop.from} </span>
          {money(p.priceFrom)}
          <span className="text-xs font-semibold text-mute"> {fmt(S.shop.perUnit, { unit: S.units[p.unit] })}</span>
        </p>
        <p className="text-xs font-semibold text-leaf-700">
          {S.shop.moq}: {p.moq} {S.units[p.unit]}
        </p>
        <p className="truncate text-xs text-mute">{p.sellerName} · {p.sellerCity}</p>
      </div>
    </Link>
  )
}
