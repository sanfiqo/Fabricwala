import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, Shirt } from 'lucide-react'
import { sellerHomeApi, sellerProductApi } from '@/api/endpoints'
import { qk } from '@/api/keys'
import type { ProductStatus, ProductSummary } from '@/api/types'
import { ProductImage } from '@/components/ProductImage'
import { SearchBox } from '@/components/SearchBox'
import { Badge, Chip, EmptyState, ErrorState, LinkButton, Skeleton, type Tone } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { fmt, S } from '@/i18n'
import { haptic } from '@/motion'
import { cn, money } from '@/lib/utils'

const statusTone: Record<ProductStatus, Tone> = { Draft: 'neutral', Active: 'good', Inactive: 'warn', PendingReview: 'warn' }
export const statusLabel = (s: ProductStatus) => ({ Draft: S.sp.draft, Active: S.sp.live, Inactive: S.sp.paused, PendingReview: S.sp.review })[s]

type Filter = 'all' | 'low' | 'draft'

/** A switch that is big enough for a thumb (44px high hit area). */
function Switch({ on, label, onChange, disabled }: { on: boolean; label: string; onChange: (v: boolean) => void; disabled?: boolean }) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={on}
      aria-label={label}
      disabled={disabled}
      onClick={() => onChange(!on)}
      className="press flex size-11 shrink-0 items-center justify-center rounded-full disabled:opacity-40"
    >
      <span className={cn('flex h-7 w-12 items-center rounded-full p-0.5 transition-colors', on ? 'bg-success' : 'bg-line')}>
        <span className={cn('size-6 rounded-full bg-surface shadow-sm transition-transform', on ? 'translate-x-5' : 'translate-x-0')} />
      </span>
    </button>
  )
}

export default function SellerProductsPage() {
  const qc = useQueryClient()
  const toast = useToast()
  const [filter, setFilter] = useState<Filter>('all')
  const [q, setQ] = useState('')
  const list = useQuery({ queryKey: qk.sellerProducts, queryFn: sellerProductApi.list })
  const dash = useQuery({ queryKey: qk.sellerDashboard, queryFn: sellerHomeApi.dashboard })
  const lowIds = useMemo(() => new Set((dash.data?.lowStock ?? []).map((l) => l.productId)), [dash.data])

  // Optimistic on/off. Undo puts it back.
  const toggle = useMutation({
    mutationFn: (v: { p: ProductSummary; status: 'Active' | 'Inactive' }) => sellerProductApi.setStatus(v.p.id, v.status),
    onMutate: async (v) => {
      await qc.cancelQueries({ queryKey: qk.sellerProducts })
      const before = qc.getQueryData<ProductSummary[]>(qk.sellerProducts)
      qc.setQueryData<ProductSummary[]>(qk.sellerProducts, (old) => old?.map((x) => (x.id === v.p.id ? { ...x, status: v.status } : x)))
      return { before }
    },
    onError: (e, _v, ctx) => {
      if (ctx?.before) qc.setQueryData(qk.sellerProducts, ctx.before)
      toast.error(e.message)
    },
    onSuccess: (_d, v) => {
      haptic()
      const back = v.status === 'Active' ? 'Inactive' : 'Active'
      const text = fmt(v.status === 'Active' ? S.sp.liveToast : S.sp.pausedToast, { name: v.p.title })
      if (v.status === 'Inactive') toast.undo(text, S.app.undo, () => toggle.mutate({ p: v.p, status: back }))
      else toast.success(text)
    },
    onSettled: () => void qc.invalidateQueries({ queryKey: qk.sellerProducts }),
  })

  const all = list.data ?? []
  const needle = q.trim().toLowerCase()
  const shown = all.filter((p) => (!needle || p.title.toLowerCase().includes(needle)) && (filter === 'all' || (filter === 'low' ? lowIds.has(p.id) : p.status === 'Draft')))
  const count = (f: Filter) => (f === 'all' ? all.length : f === 'low' ? all.filter((p) => lowIds.has(p.id)).length : all.filter((p) => p.status === 'Draft').length)
  const add = (
    <LinkButton to="/seller/products/new" className="min-h-11 px-4 text-sm">
      <Plus className="mr-1 size-4" aria-hidden />
      {S.sp.add}
    </LinkButton>
  )

  return (
    <div className="pb-4">
      <div className="flex items-start justify-between gap-3 px-5 pt-7">
        <h1 className="text-[30px] font-semibold leading-[1.15] tracking-[-0.02em]">{S.sp.title}</h1>
        <div className="shrink-0 pt-1.5">{add}</div>
      </div>
      <SearchBox key="seller-search" onSearch={setQ} placeholder={S.sp.search} className="mx-5 mt-4" />
      <nav aria-label={S.sp.title} className="mx-5 mt-3 flex gap-2 overflow-x-auto [scrollbar-width:none]">
        {([['all', S.sp.all], ['low', S.sp.lowStock], ['draft', S.sp.draft]] as const).map(([f, l]) => (
          <Chip key={f} active={filter === f} onClick={() => setFilter(f)} className="shrink-0">
            {l} {count(f)}
          </Chip>
        ))}
      </nav>

      <div className="pt-3.5">
        {list.isLoading ? (
          <div role="status" aria-label={S.app.loading} className="space-y-2 px-5">
            {Array.from({ length: 4 }, (_, i) => <Skeleton key={i} className="h-[84px] !rounded-card" />)}
          </div>
        ) : list.error && !list.data ? (
          <ErrorState error={list.error} onRetry={() => void list.refetch()} />
        ) : all.length === 0 ? (
          <EmptyState icon={<Shirt className="size-8" />} title={S.seller.noProducts} sub={S.seller.noProductsSub} action={add} />
        ) : shown.length === 0 ? (
          <EmptyState icon={<Shirt className="size-8" />} title={S.sp.noMatch} sub={S.sp.noMatchSub} />
        ) : (
          <ul className="mx-5 overflow-hidden rounded-card border border-line bg-surface">
            {shown.map((p) => {
              const status = p.status ?? 'Draft'
              const canToggle = status === 'Active' || status === 'Inactive'
              return (
                <li key={p.id} className="flex items-center gap-3 border-t border-line p-3 first:border-t-0">
                  <Link to={`/seller/products/${p.id}`} viewTransition className="press flex min-w-0 flex-1 items-center gap-3">
                    <ProductImage src={p.imageUrl} lqip={p.imageLqip} alt="" width={120} height={120} className="size-14 shrink-0 rounded-xl" />
                    <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                      <span className="line-clamp-2 break-words text-[15px] font-medium leading-5">{p.title}</span>
                      <span className="text-[13px] text-mute">
                        {p.priceFrom > 0 ? `${money(p.priceFrom)}${p.priceTo !== p.priceFrom ? `–${money(p.priceTo)}` : ''}` : '—'} · {fmt(S.shop.moqShort, { n: p.moq, unit: S.unitsShort[p.unit] })}
                      </span>
                      <span className="flex flex-wrap items-center gap-1.5">
                        <Badge tone={statusTone[status]}>{statusLabel(status)}</Badge>
                        {lowIds.has(p.id) && <Badge tone="warn">{S.sp.lowStock}</Badge>}
                      </span>
                    </span>
                  </Link>
                  {canToggle && <Switch on={status === 'Active'} label={`${S.sp.showToShops}: ${p.title}`} disabled={toggle.isPending} onChange={(v) => toggle.mutate({ p, status: v ? 'Active' : 'Inactive' })} />}
                </li>
              )
            })}
          </ul>
        )}
      </div>
    </div>
  )
}
