import { useEffect, useMemo, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { keepPreviousData, useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { PackageSearch, SlidersHorizontal, X } from 'lucide-react'
import { productApi } from '@/api/endpoints'
import type { ProductQuery } from '@/api/types'
import { ProductCard, ProductCardSkeleton } from '@/components/ProductCard'
import { SearchBox } from '@/components/SearchBox'
import { Button } from '@/components/ui/button'
import { Field, Input } from '@/components/ui/field'
import { BackButton, Chip, EmptyState, ErrorState, Sheet } from '@/components/ui/misc'
import { useCategories, useDebounced, useDeliveryCity } from '@/hooks'
import { fmt, S } from '@/i18n'
import { MATERIALS, SEASONS } from '@/lib/constants'
import { cn, money } from '@/lib/utils'

const PAGE = 12
const FILTER_KEYS = ['material', 'season', 'minPrice', 'maxPrice', 'maxMoq'] as const
type FilterKey = (typeof FILTER_KEYS)[number]
type Draft = Record<FilterKey, string> & { sort: string }

const PRICE_PRESETS: Array<[string, string]> = [['', '300'], ['300', '600'], ['600', '1000'], ['1000', '']]
const MOQ_PRESETS = ['30', '50', '100', '200']

export default function ProductListPage() {
  const [params, setParams] = useSearchParams()
  const [open, setOpen] = useState(false)
  const [draft, setDraft] = useState<Draft>({ material: '', season: '', minPrice: '', maxPrice: '', maxMoq: '', sort: '' })
  const cats = useCategories()
  const city = useDeliveryCity()

  const num = (k: string) => (params.get(k) ? Number(params.get(k)) : undefined)
  const query: ProductQuery = {
    q: params.get('q') ?? undefined,
    categoryId: num('categoryId'),
    material: params.get('material') ?? undefined,
    season: params.get('season') ?? undefined,
    minPrice: num('minPrice'),
    maxPrice: num('maxPrice'),
    maxMoq: num('maxMoq'),
    sort: (params.get('sort') as ProductQuery['sort']) ?? 'new',
  }
  const list = useInfiniteQuery({
    queryKey: ['products', query],
    queryFn: ({ pageParam }) => productApi.list({ ...query, page: pageParam, pageSize: PAGE }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page * last.pageSize < last.total ? last.page + 1 : undefined),
  })

  const patch = (changes: Record<string, string | undefined>) => {
    const next = new URLSearchParams(params)
    for (const [k, v] of Object.entries(changes)) {
      if (v) next.set(k, v)
      else next.delete(k)
    }
    setParams(next, { replace: true })
  }
  const activeFilters = FILTER_KEYS.filter((k) => params.get(k)).length
  const items = list.data?.pages.flatMap((p) => p.items) ?? []
  const total = list.data?.pages[0]?.total ?? 0

  // Load the next page when the end of the list scrolls into view.
  const sentinel = useRef<HTMLDivElement>(null)
  const { hasNextPage, isFetchingNextPage, fetchNextPage } = list
  useEffect(() => {
    const el = sentinel.current
    if (!el || !hasNextPage) return
    const io = new IntersectionObserver((e) => e[0].isIntersecting && !isFetchingNextPage && void fetchNextPage(), { rootMargin: '300px' })
    io.observe(el)
    return () => io.disconnect()
  }, [hasNextPage, isFetchingNextPage, fetchNextPage, items.length])

  const openFilters = () => {
    setDraft({ ...(Object.fromEntries(FILTER_KEYS.map((k) => [k, params.get(k) ?? ''])) as Record<FilterKey, string>), sort: params.get('sort') ?? '' })
    setOpen(true)
  }
  const setD = (k: keyof Draft, v: string) => setDraft((x) => ({ ...x, [k]: x[k] === v && k !== 'minPrice' && k !== 'maxPrice' ? '' : v }))

  // Live count: how many products the filters in the sheet would show, before the person applies them.
  const draftQuery = useDebounced(
    useMemo<ProductQuery>(
      () => ({
        q: query.q,
        categoryId: query.categoryId,
        material: draft.material || undefined,
        season: draft.season || undefined,
        minPrice: draft.minPrice ? Number(draft.minPrice) : undefined,
        maxPrice: draft.maxPrice ? Number(draft.maxPrice) : undefined,
        maxMoq: draft.maxMoq ? Number(draft.maxMoq) : undefined,
      }),
      [query.q, query.categoryId, draft.material, draft.season, draft.minPrice, draft.maxPrice, draft.maxMoq],
    ),
    250,
  )
  const live = useQuery({ queryKey: ['products', 'count', draftQuery], queryFn: () => productApi.list({ ...draftQuery, pageSize: 1 }), enabled: open, placeholderData: keepPreviousData })

  const apply = () => {
    patch({ ...(Object.fromEntries(FILTER_KEYS.map((k) => [k, draft[k] || undefined])) as Record<string, string | undefined>), sort: draft.sort || undefined })
    setOpen(false)
  }
  const clearAll = () => patch(Object.fromEntries([...FILTER_KEYS, 'q', 'categoryId'].map((k) => [k, undefined])))
  const catName = cats.data?.find((c) => c.id === query.categoryId)?.name

  const chips: Array<{ key: string; label: string; clear: Record<string, undefined> }> = []
  if (query.categoryId && catName) chips.push({ key: 'cat', label: catName, clear: { categoryId: undefined } })
  if (query.material) chips.push({ key: 'material', label: query.material, clear: { material: undefined } })
  if (query.season) chips.push({ key: 'season', label: S.seasons[query.season as keyof typeof S.seasons] ?? query.season, clear: { season: undefined } })
  if (query.minPrice || query.maxPrice)
    chips.push({ key: 'price', label: `${query.minPrice ? money(query.minPrice) : '₹0'}–${query.maxPrice ? money(query.maxPrice) : '∞'}`, clear: { minPrice: undefined, maxPrice: undefined } })
  if (query.maxMoq) chips.push({ key: 'moq', label: fmt(S.shop.moqUpTo, { n: query.maxMoq }), clear: { maxMoq: undefined } })

  return (
    <div className="pb-4">
      <div className="flex items-center gap-2 px-4 pt-5">
        <BackButton to="/shop" className="!bg-transparent" />
        <SearchBox key={query.q ?? ''} initial={query.q ?? ''} onSearch={(q) => patch({ q: q || undefined })} className="min-w-0 flex-1" />
      </div>

      <div className="mt-3 flex gap-2 overflow-x-auto px-5 pb-1 [scrollbar-width:none]">
        <button
          type="button"
          onClick={openFilters}
          className={cn('press inline-flex min-h-11 shrink-0 items-center gap-1.5 rounded-chip border px-4 text-sm', activeFilters > 0 ? 'border-ink bg-ink text-background' : 'border-line bg-surface')}
        >
          <SlidersHorizontal className="size-4" aria-hidden />
          {S.shop.filters}
          {activeFilters > 0 && ` · ${activeFilters}`}
        </button>
        {chips.map((c) => (
          <button key={c.key} type="button" onClick={() => patch(c.clear)} aria-label={`${c.label} — ${S.app.close}`} className="press inline-flex min-h-11 shrink-0 items-center gap-1.5 rounded-chip border border-line bg-surface px-3.5 text-sm">
            {c.label}
            <X className="size-3.5 text-mute" aria-hidden />
          </button>
        ))}
        {[{ id: undefined, name: S.shop.all }, ...(cats.data ?? [])].filter((c) => c.id !== query.categoryId && c.id !== undefined).map((c) => (
          <Chip key={c.id} onClick={() => patch({ categoryId: String(c.id) })} className="shrink-0">
            {c.name}
          </Chip>
        ))}
      </div>

      <p className="px-5 pb-2.5 pt-3.5 text-[13px] text-mute" aria-live="polite">
        {list.isLoading ? ' ' : fmt(S.shop.results, { n: total })}
        {city.cityName ? ` · ${fmt(S.shop.landedIn, { city: city.cityName })}` : ''}
      </p>

      <section aria-label={S.shop.resultsLabel} className="px-5">
        {list.isLoading ? (
          <div className="grid grid-cols-2 gap-3" role="status" aria-label={S.app.loading}>
            {Array.from({ length: 6 }, (_, i) => <ProductCardSkeleton key={i} />)}
          </div>
        ) : list.error ? (
          <ErrorState error={list.error} onRetry={() => void list.refetch()} />
        ) : items.length === 0 ? (
          <EmptyState
            icon={<PackageSearch className="size-8" />}
            title={S.shop.noResults}
            sub={S.shop.noResultsSub}
            action={activeFilters > 0 || query.q || query.categoryId ? <Button variant="soft" onClick={clearAll}>{S.shop.clear}</Button> : undefined}
          />
        ) : (
          <>
            <div className="grid grid-cols-2 gap-3">{items.map((p, i) => <ProductCard key={p.id} p={p} index={i} />)}</div>
            <div ref={sentinel} className="h-px" />
            {isFetchingNextPage && (
              <div className="mt-3 grid grid-cols-2 gap-3" aria-hidden>
                <ProductCardSkeleton />
                <ProductCardSkeleton />
              </div>
            )}
            {hasNextPage && !isFetchingNextPage && (
              <Button variant="soft" block className="mt-4" onClick={() => void fetchNextPage()}>
                {S.shop.loadMore}
              </Button>
            )}
          </>
        )}
      </section>

      <Sheet open={open} onClose={() => setOpen(false)} title={S.shop.filters}>
        <div className="space-y-6">
          <div>
            <p className="mb-2 font-medium">{S.shop.sort}</p>
            <div className="flex flex-wrap gap-2">
              {([['', S.shop.sortNew], ['price-asc', S.shop.sortPriceLow], ['price-desc', S.shop.sortPriceHigh], ['moq-asc', S.shop.sortMoq]] as const).map(([v, l]) => (
                <Chip key={v} active={draft.sort === v} onClick={() => setDraft((x) => ({ ...x, sort: v }))}>{l}</Chip>
              ))}
            </div>
          </div>

          <div>
            <p className="mb-2 font-medium">{S.shop.pricePerPiece}</p>
            <div className="mb-3 flex flex-wrap gap-2">
              {PRICE_PRESETS.map(([lo, hi]) => (
                <Chip key={lo + hi} active={draft.minPrice === lo && draft.maxPrice === hi} onClick={() => setDraft((x) => ({ ...x, minPrice: lo, maxPrice: hi }))}>
                  {lo && hi ? `₹${lo}–₹${hi}` : hi ? fmt(S.shop.under, { n: `₹${hi}` }) : fmt(S.shop.above, { n: `₹${lo}` })}
                </Chip>
              ))}
            </div>
            <div className="grid grid-cols-2 gap-3">
              <Field label={S.shop.minPrice}>{(id) => <Input id={id} inputMode="numeric" value={draft.minPrice} onChange={(e) => setD('minPrice', e.target.value.replace(/\D/g, ''))} />}</Field>
              <Field label={S.shop.maxPrice}>{(id) => <Input id={id} inputMode="numeric" value={draft.maxPrice} onChange={(e) => setD('maxPrice', e.target.value.replace(/\D/g, ''))} />}</Field>
            </div>
          </div>

          <div>
            <p className="mb-2 font-medium">{S.shop.maxMoq}</p>
            <div className="flex flex-wrap gap-2">
              {MOQ_PRESETS.map((m) => (
                <Chip key={m} active={draft.maxMoq === m} onClick={() => setD('maxMoq', m)}>{fmt(S.shop.moqUpTo, { n: m })}</Chip>
              ))}
            </div>
          </div>

          <div>
            <p className="mb-2 font-medium">{S.shop.material}</p>
            <div className="flex flex-wrap gap-2">
              {MATERIALS.map((m) => (
                <Chip key={m} active={draft.material === m} onClick={() => setD('material', m)}>{m}</Chip>
              ))}
            </div>
          </div>

          <div>
            <p className="mb-2 font-medium">{S.shop.season}</p>
            <div className="flex flex-wrap gap-2">
              {SEASONS.map((m) => (
                <Chip key={m} active={draft.season === m} onClick={() => setD('season', m)}>{S.seasons[m]}</Chip>
              ))}
            </div>
          </div>

          <div className="sticky bottom-0 z-10 -mx-5 -mb-6 flex gap-3 border-t border-line bg-surface px-5 pb-5 pt-3">
            <Button variant="soft" onClick={() => setDraft({ material: '', season: '', minPrice: '', maxPrice: '', maxMoq: '', sort: '' })}>{S.shop.clear}</Button>
            <Button className="flex-1" onClick={apply} aria-live="polite">
              {live.data ? fmt(S.shop.showN, { n: live.data.total }) : S.shop.apply}
            </Button>
          </div>
        </div>
      </Sheet>
    </div>
  )
}
