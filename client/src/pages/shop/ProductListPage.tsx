import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useInfiniteQuery } from '@tanstack/react-query'
import { PackageSearch, SlidersHorizontal } from 'lucide-react'
import { productApi } from '@/api/endpoints'
import type { ProductQuery } from '@/api/types'
import { ProductCard } from '@/components/ProductCard'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/field'
import { EmptyState, ErrorState, PageHeader, PageLoading, Sheet } from '@/components/ui/misc'
import { useCategories } from '@/hooks'
import { fmt, S } from '@/i18n'
import { MATERIALS, SEASONS } from '@/lib/constants'
import { cn } from '@/lib/utils'
import { SearchBox as SearchBar } from '@/components/SearchBox'

const PAGE = 12
const FILTER_KEYS = ['material', 'season', 'minPrice', 'maxPrice', 'maxMoq'] as const

export default function ProductListPage() {
  const [params, setParams] = useSearchParams()
  const [open, setOpen] = useState(false)
  const [draft, setDraft] = useState<Record<string, string>>({})
  const cats = useCategories()

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

  const openFilters = () => {
    setDraft(Object.fromEntries(FILTER_KEYS.map((k) => [k, params.get(k) ?? ''])))
    setOpen(true)
  }
  const d = (k: string) => draft[k] ?? ''
  const setD = (k: string, v: string) => setDraft((x) => ({ ...x, [k]: v }))

  return (
    <div>
      <PageHeader title={cats.data?.find((c) => c.id === query.categoryId)?.name ?? S.nav.home} back="/shop" />
      <div className="space-y-3 px-4 pt-3">
        <SearchBar key={query.q ?? ''} initial={query.q ?? ''} onSearch={(q) => patch({ q: q || undefined })} />

        <div className="-mx-4 flex gap-2 overflow-x-auto px-4 pb-1">
          {[{ id: undefined, name: S.shop.all }, ...(cats.data ?? [])].map((c) => (
            <button
              key={c.id ?? 'all'}
              type="button"
              onClick={() => patch({ categoryId: c.id?.toString() })}
              className={cn('min-h-10 shrink-0 rounded-full border px-4 text-sm font-bold', query.categoryId === c.id ? 'border-navy-900 bg-navy-900 text-white' : 'border-line text-navy-900')}
            >
              {c.name}
            </button>
          ))}
        </div>

        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={openFilters}>
            <SlidersHorizontal className="size-4" />
            {S.shop.filters}
            {activeFilters > 0 && <span className="rounded-full bg-leaf-600 px-1.5 text-xs text-white">{activeFilters}</span>}
          </Button>
          <label className="sr-only" htmlFor="sort">{S.shop.sort}</label>
          <Select id="sort" value={query.sort} onChange={(e) => patch({ sort: e.target.value === 'new' ? undefined : e.target.value })} className="!min-h-10 flex-1 text-sm font-semibold">
            <option value="new">{S.shop.sortNew}</option>
            <option value="price-asc">{S.shop.sortPriceLow}</option>
            <option value="price-desc">{S.shop.sortPriceHigh}</option>
            <option value="moq-asc">{S.shop.sortMoq}</option>
          </Select>
        </div>
      </div>

      <div className="px-4 pt-4">
        {list.isLoading ? (
          <PageLoading />
        ) : list.error ? (
          <ErrorState error={list.error} onRetry={() => void list.refetch()} />
        ) : items.length === 0 ? (
          <EmptyState icon={<PackageSearch className="size-8" />} title={S.shop.noResults} sub={S.shop.noResultsSub} />
        ) : (
          <>
            <p className="mb-3 text-sm font-semibold text-mute">{fmt(S.shop.results, { n: total })}</p>
            <div className="grid grid-cols-2 gap-3">{items.map((p) => <ProductCard key={p.id} p={p} />)}</div>
            {list.hasNextPage && (
              <Button variant="outline" block className="mt-5" loading={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()}>
                {S.shop.all} +
              </Button>
            )}
          </>
        )}
      </div>

      <Sheet open={open} onClose={() => setOpen(false)} title={S.shop.filters}>
        <div className="space-y-4">
          <Field label={S.shop.material}>
            {(id) => (
              <Select id={id} value={d('material')} onChange={(e) => setD('material', e.target.value)}>
                <option value="">{S.shop.anyOption}</option>
                {MATERIALS.map((m) => <option key={m} value={m}>{m}</option>)}
              </Select>
            )}
          </Field>
          <Field label={S.shop.season}>
            {(id) => (
              <Select id={id} value={d('season')} onChange={(e) => setD('season', e.target.value)}>
                <option value="">{S.shop.anyOption}</option>
                {SEASONS.map((m) => <option key={m} value={m}>{S.seasons[m]}</option>)}
              </Select>
            )}
          </Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label={S.shop.minPrice}>{(id) => <Input id={id} inputMode="numeric" value={d('minPrice')} onChange={(e) => setD('minPrice', e.target.value.replace(/\D/g, ''))} />}</Field>
            <Field label={S.shop.maxPrice}>{(id) => <Input id={id} inputMode="numeric" value={d('maxPrice')} onChange={(e) => setD('maxPrice', e.target.value.replace(/\D/g, ''))} />}</Field>
          </div>
          <Field label={S.shop.maxMoq}>{(id) => <Input id={id} inputMode="numeric" value={d('maxMoq')} onChange={(e) => setD('maxMoq', e.target.value.replace(/\D/g, ''))} />}</Field>
          <div className="flex gap-3 pt-2">
            <Button variant="outline" onClick={() => { patch(Object.fromEntries(FILTER_KEYS.map((k) => [k, undefined]))); setOpen(false) }}>{S.shop.clear}</Button>
            <Button className="flex-1" onClick={() => { patch(Object.fromEntries(FILTER_KEYS.map((k) => [k, d(k) || undefined]))); setOpen(false) }}>{S.shop.apply}</Button>
          </div>
        </div>
      </Sheet>
    </div>
  )
}
