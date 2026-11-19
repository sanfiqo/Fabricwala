import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Heart } from 'lucide-react'
import { savedApi } from '@/api/endpoints'
import { qk } from '@/api/keys'
import { useToast } from '@/components/ui/toast'
import { S } from '@/i18n'
import { cn } from '@/lib/utils'

/** Heart toggle for one product. Reads the saved list (one shared request) and saves or removes with the server. */
export function SaveButton({ productId, className }: { productId: number; className?: string }) {
  const qc = useQueryClient()
  const toast = useToast()
  const saved = useQuery({ queryKey: qk.saved, queryFn: () => savedApi.list(), staleTime: 30_000, retry: false })
  const isSaved = saved.data?.items.some((x) => x.id === productId) ?? false

  const toggle = useMutation({
    mutationFn: (next: boolean) => (next ? savedApi.save(productId) : savedApi.remove(productId)),
    onSuccess: (_data, next) => toast.success(next ? S.saved.added : S.saved.removed),
    onError: (e) => toast.error(e.message),
    onSettled: () => void qc.invalidateQueries({ queryKey: qk.saved }),
  })

  return (
    <button
      type="button"
      aria-pressed={isSaved}
      aria-label={isSaved ? S.saved.unsave : S.saved.save}
      disabled={toggle.isPending}
      onClick={() => toggle.mutate(!isSaved)}
      className={cn('press flex size-12 shrink-0 items-center justify-center rounded-full bg-surface shadow-[0_2px_10px_rgb(60_40_20/0.08)]', isSaved ? 'text-brand' : 'text-ink', className)}
    >
      <Heart className={cn('size-[22px]', isSaved && 'fill-current')} aria-hidden />
    </button>
  )
}
