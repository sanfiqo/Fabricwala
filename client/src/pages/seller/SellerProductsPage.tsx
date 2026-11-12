import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Plus, Shirt } from 'lucide-react'
import { sellerProductApi } from '@/api/endpoints'
import type { ProductStatus } from '@/api/types'
import { Badge, Card, EmptyState, ErrorState, LinkButton, PageHeader, PageLoading, type Tone } from '@/components/ui/misc'
import { S } from '@/i18n'
import { money } from '@/lib/utils'

export const statusLabel: Record<ProductStatus, string> = {
  Draft: S.seller.statusDraft, Active: S.seller.statusActive, Inactive: S.seller.statusInactive, PendingReview: S.seller.statusPendingReview,
}
const statusTone: Record<ProductStatus, Tone> = { Draft: 'neutral', Active: 'good', Inactive: 'warn', PendingReview: 'warn' }

export default function SellerProductsPage() {
  const list = useQuery({ queryKey: ['seller-products'], queryFn: sellerProductApi.list })
  const add = (
    <LinkButton to="/seller/products/new" className="!min-h-10 px-3 text-sm">
      <Plus className="mr-1 size-4" />
      {S.seller.addProduct}
    </LinkButton>
  )
  return (
    <div>
      <PageHeader title={S.seller.productsTitle} action={add} />
      {list.isLoading ? (
        <PageLoading />
      ) : list.error ? (
        <ErrorState error={list.error} onRetry={() => void list.refetch()} />
      ) : (list.data ?? []).length === 0 ? (
        <EmptyState icon={<Shirt className="size-8" />} title={S.seller.noProducts} sub={S.seller.noProductsSub} action={add} />
      ) : (
        <div className="space-y-3 px-4 pt-4">
          {list.data!.map((p) => (
            <Link key={p.id} to={`/seller/products/${p.id}`} className="block">
              <Card className="flex gap-3 p-3 active:bg-navy-50">
                <div className="size-20 shrink-0 overflow-hidden rounded-xl bg-navy-50">{p.imageUrl && <img src={p.imageUrl} alt="" className="size-full object-cover" />}</div>
                <div className="min-w-0 flex-1">
                  <p className="line-clamp-2 font-bold leading-5 text-navy-900">{p.title}</p>
                  <p className="mt-1 text-sm text-mute">
                    {p.priceFrom > 0 ? `${money(p.priceFrom)}${p.priceTo !== p.priceFrom ? ` – ${money(p.priceTo)}` : ''}` : '—'} · {S.shop.moq} {p.moq}
                  </p>
                  <div className="mt-2"><Badge tone={statusTone[p.status ?? 'Draft']}>{statusLabel[p.status ?? 'Draft']}</Badge></div>
                </div>
              </Card>
            </Link>
          ))}
        </div>
      )}
    </div>
  )
}
