import { useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { MapPin, PackageSearch, Star } from 'lucide-react'
import { storefrontApi } from '@/api/endpoints'
import { ApiError } from '@/api/client'
import { qk } from '@/api/keys'
import { ProductCard, ProductCardSkeleton } from '@/components/ProductCard'
import { EmptyState, ErrorState, LinkButton, PageHeader, SectionTitle } from '@/components/ui/misc'
import { fmt, S } from '@/i18n'

/** Public seller page: who the seller is, how buyers rated them, and every live product. */
export default function StorefrontPage() {
  const { id } = useParams()
  const businessId = Number(id)
  const valid = Number.isInteger(businessId) && businessId > 0
  const q = useQuery({ queryKey: qk.storefront(businessId), queryFn: () => storefrontApi.get(businessId), enabled: valid })

  if (!valid || (q.error instanceof ApiError && q.error.status === 404)) {
    return (
      <div>
        <PageHeader title={S.store.title} back />
        <EmptyState icon={<PackageSearch className="size-7" aria-hidden />} title={S.store.notFound} action={<LinkButton to="/shop/products">{S.saved.browse}</LinkButton>} />
      </div>
    )
  }

  const s = q.data
  const initial = s?.business.displayName.trim().charAt(0).toUpperCase() ?? ''

  return (
    <div className="pb-6">
      <PageHeader title={S.store.title} back />

      <div className="px-4 pt-4">
        {q.error ? (
          <ErrorState error={q.error} onRetry={() => void q.refetch()} />
        ) : !s ? (
          <div role="status" aria-label={S.app.loading} className="skeleton h-[150px] rounded-card" />
        ) : (
          <section className="card-emerald relative overflow-hidden rounded-card p-5">
            <span aria-hidden className="float-slow absolute -right-6 -top-6 size-28 rounded-full bg-white/10" />
            <div className="relative flex items-center gap-4">
              <span aria-hidden className="flex size-16 shrink-0 items-center justify-center rounded-full bg-white/15 text-2xl font-semibold">{initial}</span>
              <div className="min-w-0">
                <h2 className="break-words text-xl font-semibold leading-tight">{s.business.displayName}</h2>
                {s.business.city && (
                  <p className="mt-1 flex items-center gap-1.5 text-sm text-white/80">
                    <MapPin className="size-4 shrink-0" aria-hidden />
                    <span className="truncate">{fmt(S.store.city, { city: s.business.city })}</span>
                  </p>
                )}
                <p className="mt-1 flex items-center gap-1.5 text-sm text-white/80">
                  <Star className="size-4 shrink-0 fill-current" aria-hidden />
                  <span>{s.reviewCount > 0 ? fmt(S.store.rating, { rating: s.rating.toFixed(1), count: s.reviewCount }) : S.store.noRating}</span>
                </p>
              </div>
            </div>
          </section>
        )}
      </div>

      <div className="px-4 pt-6">
        <SectionTitle>{S.store.products}</SectionTitle>
        {q.isLoading ? (
          <div className="mt-3 grid grid-cols-2 gap-3" aria-hidden>
            {[0, 1, 2, 3].map((i) => (
              <ProductCardSkeleton key={i} />
            ))}
          </div>
        ) : s && s.products.length === 0 ? (
          <EmptyState icon={<PackageSearch className="size-7" aria-hidden />} title={S.store.noProducts} />
        ) : (
          <div className="mt-3 grid grid-cols-2 gap-3">
            {s?.products.map((p, i) => (
              <ProductCard key={p.id} p={p} index={i} />
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
