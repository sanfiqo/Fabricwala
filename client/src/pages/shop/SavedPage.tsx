import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Heart } from 'lucide-react'
import { savedApi } from '@/api/endpoints'
import { qk } from '@/api/keys'
import type { Page, SavedProduct } from '@/api/types'
import { Badge, EmptyState, ErrorState, LinkButton, PageHeader, Skeleton } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { fmt, S } from '@/i18n'
import { cn, money } from '@/lib/utils'

function Row({ p, onRemove, busy }: { p: SavedProduct; onRemove: (id: number) => void; busy: boolean }) {
  const body = (
    <>
      <span className="truncate text-xs text-mute">{p.sellerName}</span>
      <span className="line-clamp-2 break-words text-[15px] font-medium leading-5">{p.title}</span>
      <span className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm">
        {p.available && p.priceFrom !== null ? (
          <span className="font-semibold tabular-nums">{fmt(S.saved.from, { price: money(p.priceFrom) })}</span>
        ) : (
          <Badge tone="warn">{S.saved.unavailable}</Badge>
        )}
        <span className="text-xs text-mute">{fmt(S.saved.moq, { n: p.moq })}</span>
      </span>
    </>
  )
  return (
    <li className="flex items-center gap-3 rounded-card border border-line bg-surface p-3 pl-4">
      {p.available ? (
        <Link to={`/shop/p/${p.slug}`} viewTransition className="press flex min-w-0 flex-1 flex-col gap-0.5">
          {body}
        </Link>
      ) : (
        <div className={cn('flex min-w-0 flex-1 flex-col gap-0.5 opacity-70')}>{body}</div>
      )}
      <button
        type="button"
        aria-label={S.saved.unsave}
        disabled={busy}
        onClick={() => onRemove(p.id)}
        className="press flex size-12 shrink-0 items-center justify-center rounded-full bg-peach-soft text-brand"
      >
        <Heart className="size-5 fill-current" aria-hidden />
      </button>
    </li>
  )
}

/** Products the shop kept for later. Removing a row is instant; the server catches up. */
export default function SavedPage() {
  const qc = useQueryClient()
  const toast = useToast()
  const list = useQuery({ queryKey: qk.saved, queryFn: () => savedApi.list() })

  const remove = useMutation({
    mutationFn: (id: number) => savedApi.remove(id),
    onMutate: (id) => {
      qc.setQueryData<Page<SavedProduct>>(qk.saved, (old) => (old ? { ...old, items: old.items.filter((x) => x.id !== id), total: Math.max(0, old.total - 1) } : old))
    },
    onSuccess: () => toast.success(S.saved.removed),
    onError: (e) => toast.error(e.message),
    onSettled: () => void qc.invalidateQueries({ queryKey: qk.saved }),
  })

  const items = list.data?.items ?? []

  return (
    <div className="pb-6">
      <PageHeader title={S.saved.title} back="/shop/account" />
      <div className="px-4 pt-4">
        {list.isLoading ? (
          <div role="status" aria-label={S.app.loading} className="space-y-3">
            <Skeleton className="h-[92px] !rounded-card" />
            <Skeleton className="h-[92px] !rounded-card" />
            <Skeleton className="h-[92px] !rounded-card" />
          </div>
        ) : list.error ? (
          <ErrorState error={list.error} onRetry={() => void list.refetch()} />
        ) : items.length === 0 ? (
          <EmptyState icon={<Heart className="size-7" aria-hidden />} title={S.saved.empty} sub={S.saved.emptySub} action={<LinkButton to="/shop/products">{S.saved.browse}</LinkButton>} />
        ) : (
          <ul className="space-y-3">
            {items.map((p) => (
              <Row key={p.id} p={p} busy={remove.isPending} onRemove={(id) => remove.mutate(id)} />
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}
