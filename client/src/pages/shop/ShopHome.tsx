import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Bell, MapPin, Package, SlidersHorizontal } from 'lucide-react'
import { cartApi, productApi } from '@/api/endpoints'
import { useMe } from '@/auth'
import { AddressSheet } from '@/components/AddressSheet'
import { CartLink, roundIcon } from '@/components/HeaderButtons'
import { ProductCard, ProductCardSkeleton } from '@/components/ProductCard'
import { SearchBox } from '@/components/SearchBox'
import { Skeleton, EmptyState, ErrorState } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { useCategories, useDeliveryAddress, useLastOrder, useUnreadCount } from '@/hooks'
import { fmt, S } from '@/i18n'
import { saveReorderPrices } from '@/lib/reorder'
import { haptic } from '@/motion'
import { InstallPrompt } from '@/components/InstallPrompt'
import { PullToRefresh } from '@/components/PullToRefresh'
import { cn, formatShortDate } from '@/lib/utils'

const initials = (name: string) =>
  name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w.charAt(0).toUpperCase())
    .join('') || '·'

/** One-tap reorder: puts the last order's items in the cart, remembers the old prices, opens the cart. */
function OrderAgainCard() {
  const { last } = useLastOrder()
  const qc = useQueryClient()
  const nav = useNavigate()
  const toast = useToast()
  const again = useMutation({
    mutationFn: async () => {
      const items = (last?.sellerOrders ?? []).flatMap((s) => s.items)
      const prices: Record<number, number> = {}
      let failed = 0
      for (const it of items) {
        if (!it.variantId) {
          failed++
          continue
        }
        try {
          await cartApi.add(it.variantId, it.quantity)
          prices[it.variantId] = it.unitPrice
        } catch {
          failed++
        }
      }
      return { prices, failed }
    },
    onSuccess: async ({ prices, failed }) => {
      haptic()
      saveReorderPrices(prices)
      await qc.invalidateQueries({ queryKey: ['cart'] })
      if (failed > 0) toast.error(S.home.orderAgainFail)
      nav('/shop/cart', { viewTransition: true })
    },
    onError: (e) => toast.error(e.message),
  })

  const so = last?.sellerOrders[0]
  const item = so?.items[0]
  if (!last || !so || !item) return null
  const more = last.sellerOrders.reduce((a, s) => a + s.items.length, 0) - 1

  return (
    <div className="mx-5 mt-3 flex items-center gap-3 rounded-[20px] border border-line bg-surface p-3.5">
      <span className="flex size-14 shrink-0 items-center justify-center rounded-[14px] bg-soft text-mute" aria-hidden>
        <Package className="size-7" />
      </span>
      <span className="flex min-w-0 flex-1 flex-col gap-0.5">
        <span className="text-xs text-mute">{fmt(S.home.lastOrder, { d: formatShortDate(last.placedAt) })}</span>
        <span className="line-clamp-2 text-sm font-medium">
          {item.quantity} {item.title}
          {more > 0 ? ` +${more}` : ''}
        </span>
        <span className="truncate text-xs text-mute">{so.manufacturerName}</span>
      </span>
      <button
        type="button"
        onClick={() => again.mutate()}
        disabled={again.isPending}
        className="press min-h-11 shrink-0 rounded-chip bg-ink px-4 text-[13px] font-medium text-background disabled:opacity-60"
      >
        {S.home.orderAgain}
      </button>
    </div>
  )
}

export default function ShopHome() {
  const nav = useNavigate()
  const qc = useQueryClient()
  const { data: me } = useMe()
  const cats = useCategories()
  const { addr } = useDeliveryAddress()
  const unread = useUnreadCount()
  const [catId, setCatId] = useState<number | null>(null)
  const [changing, setChanging] = useState(false)
  const products = useQuery({
    queryKey: ['products', 'home', catId],
    queryFn: () => productApi.list({ sort: 'new', pageSize: 8, categoryId: catId ?? undefined }),
  })
  const name = me?.business?.displayName ?? me?.fullName ?? ''
  const top = (cats.data ?? []).filter((c) => c.parentId === null)
  const tabs = top.length > 0 ? top : (cats.data ?? [])

  return (
    <div className="pb-4">
      <PullToRefresh onRefresh={() => Promise.all([qc.refetchQueries({ queryKey: ['products'] }), qc.refetchQueries({ queryKey: ['categories'] }), qc.refetchQueries({ queryKey: ['orders'] }), qc.refetchQueries({ queryKey: ['cart'] })])} />
      <header className="flex items-center justify-between px-5 pt-[22px]">
        <Link to="/shop/account" viewTransition aria-label={S.home.menu} className="press flex size-[46px] items-center justify-center rounded-full bg-ink text-[15px] font-medium text-background">
          {initials(name)}
        </Link>
        <div className="flex gap-2">
          <Link to="/shop/notifications" viewTransition aria-label={unread > 0 ? `${S.notif.bell}, ${fmt(S.notif.newCount, { n: unread })}` : S.notif.bell} className={roundIcon}>
            <Bell className="size-5" aria-hidden />
            {unread > 0 && <span className="absolute right-[11px] top-2.5 size-2 rounded-full border-2 border-surface bg-dot" aria-hidden />}
          </Link>
          <CartLink />
        </div>
      </header>

      {me?.business && me.business.verificationStatus !== 'Approved' && (
        <Link to="/pending" viewTransition className="mx-5 mt-4 block rounded-2xl bg-saving-bg p-3.5 text-sm leading-snug text-saving">
          {S.pending.browseBanner}
        </Link>
      )}

      <div className="px-5 pt-[18px]">
        <p className="truncate text-[15px] text-mute">{fmt(S.home.greet, { name })}</p>
        <h1 className="mt-0.5 text-balance text-[30px] font-semibold leading-[1.2] tracking-[-0.02em]">{S.home.title}</h1>
      </div>

      <div className="mx-5 mt-4 flex gap-2.5">
        <SearchBox className="min-w-0 flex-1" placeholder={S.home.searchPlaceholder} onSearch={(q) => nav(`/shop/products${q ? `?q=${encodeURIComponent(q)}` : ''}`, { viewTransition: true })} />
        <Link to="/shop/products" viewTransition aria-label={S.home.filters} className="press flex size-[52px] shrink-0 items-center justify-center rounded-full bg-ink text-background">
          <SlidersHorizontal className="size-5" aria-hidden />
        </Link>
      </div>

      {addr && (
        <button
          type="button"
          onClick={() => setChanging(true)}
          className="press mx-5 mt-3 flex min-h-[52px] w-[calc(100%-40px)] items-center gap-2.5 rounded-[26px] border border-line bg-surface py-1 pl-4 pr-1.5 text-left"
        >
          <MapPin className="size-[18px] shrink-0" aria-hidden />
          <span className="flex min-w-0 flex-1 flex-col leading-tight">
            <span className="text-xs text-mute">{S.home.landedIn}</span>
            <span className="truncate text-sm font-medium">{addr.cityName} {addr.pincode}</span>
          </span>
          <span className="flex min-h-11 shrink-0 items-center rounded-chip bg-soft px-4 text-[13px] font-medium">{S.app.change}</span>
        </button>
      )}

      <OrderAgainCard />
      <InstallPrompt />

      <nav aria-label={S.shop.categories} className="mt-5 flex gap-[22px] overflow-x-auto whitespace-nowrap border-b border-line px-5 text-[15px] [scrollbar-width:none]">
        {cats.isLoading ? (
          <>
            <Skeleton className="mb-3 h-5 w-14" />
            <Skeleton className="mb-3 h-5 w-16" />
            <Skeleton className="mb-3 h-5 w-14" />
          </>
        ) : (
          [{ id: null as number | null, name: S.shop.all }, ...tabs].map((c) => {
            const on = c.id === catId
            return (
              <button
                key={c.id ?? 'all'}
                type="button"
                aria-current={on ? 'page' : undefined}
                onClick={() => setCatId(c.id)}
                className={cn('press min-h-11 shrink-0 border-b-2 pb-2.5 pt-1.5', on ? 'border-ink font-semibold' : 'border-transparent text-mute')}
              >
                {c.name}
              </button>
            )
          })
        )}
      </nav>

      <section aria-label={S.home.freshTitle} className="px-5 pt-4">
        {products.isLoading ? (
          <div className="grid grid-cols-2 gap-3" role="status" aria-label={S.app.loading}>
            {Array.from({ length: 4 }, (_, i) => <ProductCardSkeleton key={i} />)}
          </div>
        ) : products.error ? (
          <ErrorState error={products.error} onRetry={() => void products.refetch()} />
        ) : (products.data?.items ?? []).length === 0 ? (
          <EmptyState icon={<Package className="size-8" />} title={S.home.noProducts} sub={S.home.noProductsSub} />
        ) : (
          <>
            <div className="grid grid-cols-2 gap-3">{products.data!.items.map((p, i) => <ProductCard key={p.id} p={p} index={i} />)}</div>
            <Link to={catId ? `/shop/products?categoryId=${catId}` : '/shop/products'} viewTransition className="press mt-4 flex min-h-12 items-center justify-center rounded-pill bg-soft font-medium">
              {S.product.seeAll}
            </Link>
          </>
        )}
      </section>

      <AddressSheet open={changing} onClose={() => setChanging(false)} />
    </div>
  )
}

