import { useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Bell, CreditCard, Package, UserCheck } from 'lucide-react'
import { notificationApi } from '@/api/endpoints'
import { qk } from '@/api/keys'
import type { NotificationItem } from '@/api/types'
import { Button } from '@/components/ui/button'
import { BackButton, EmptyState, ErrorState, Skeleton } from '@/components/ui/misc'
import { fmt, S } from '@/i18n'
import { cn, formatShortDate } from '@/lib/utils'

const ICON = { order: Package, payment: CreditCard, account: UserCheck, system: Bell } as const
const sameDay = (iso: string) => new Date(iso).toDateString() === new Date().toDateString()
const time = (iso: string) => (sameDay(iso) ? new Date(iso).toLocaleTimeString('en-IN', { hour: 'numeric', minute: '2-digit' }) : formatShortDate(iso))

function Row({ n, onOpen }: { n: NotificationItem; onOpen: (n: NotificationItem) => void }) {
  const Icon = ICON[n.type]
  return (
    <li>
      <button type="button" onClick={() => onOpen(n)} className="press flex min-h-[72px] w-full items-start gap-3 border-t border-line px-4 py-3.5 text-left first:border-t-0">
        <span className={cn('mt-3 size-2 shrink-0 rounded-full', n.isRead ? 'bg-transparent' : 'bg-ink')} aria-hidden />
        <span className="flex size-[42px] shrink-0 items-center justify-center rounded-full bg-soft" aria-hidden>
          <Icon className="size-5" />
        </span>
        <span className="flex min-w-0 flex-1 flex-col gap-0.5">
          <span className={cn('break-words text-[15px]', n.isRead ? '' : 'font-semibold')}>
            {!n.isRead && <span className="sr-only">{fmt(S.notif.newCount, { n: 1 })}. </span>}
            {n.title}
          </span>
          <span className="break-words text-[13px] text-mute">{n.body}</span>
        </span>
        <span className="shrink-0 pt-0.5 text-xs text-mute">{time(n.createdAt)}</span>
      </button>
    </li>
  )
}

/** "What is new": order updates and messages. Opens from the bell. Same screen for every role. */
export default function NotificationsPage() {
  const qc = useQueryClient()
  const nav = useNavigate()
  const list = useQuery({ queryKey: qk.notifications, queryFn: notificationApi.list })

  // Optimistic: the dot disappears at once; the server catches up.
  const setRead = (ids: number[] | 'all') => {
    qc.setQueryData<NotificationItem[]>(qk.notifications, (old) => old?.map((n) => (ids === 'all' || ids.includes(n.id) ? { ...n, isRead: true } : n)))
    qc.setQueryData<{ count: number }>(qk.unread, (old) => (old ? { count: ids === 'all' ? 0 : Math.max(0, old.count - ids.length) } : old))
  }
  const read = useMutation({
    mutationFn: (id: number) => notificationApi.read(id),
    onMutate: (id) => setRead([id]),
    onSettled: () => void qc.invalidateQueries({ queryKey: qk.unread }),
  })
  const readAll = useMutation({
    mutationFn: () => notificationApi.readAll(),
    onMutate: () => setRead('all'),
    onSettled: () => void qc.invalidateQueries({ queryKey: qk.notifications }),
  })

  const open = (n: NotificationItem) => {
    if (!n.isRead) read.mutate(n.id)
    if (n.link) nav(n.link, { viewTransition: true })
  }
  const items = list.data ?? []
  const today = items.filter((n) => sameDay(n.createdAt))
  const earlier = items.filter((n) => !sameDay(n.createdAt))
  const unread = items.filter((n) => !n.isRead).length

  return (
    <div className="pb-6">
      <div className="px-4 pt-[18px]">
        <BackButton />
      </div>
      <div className="mx-5 mt-3.5 flex items-end justify-between gap-3">
        <h1 className="text-[30px] font-semibold leading-[1.15] tracking-[-0.02em]">{S.notif.title}</h1>
        {unread > 0 && (
          <Button variant="soft" size="sm" onClick={() => readAll.mutate()} className="shrink-0">
            {S.notif.markAll}
          </Button>
        )}
      </div>

      <div className="pt-4">
        {list.isLoading ? (
          <div role="status" aria-label={S.app.loading} className="space-y-3 px-5">
            <Skeleton className="h-20 !rounded-card" />
            <Skeleton className="h-20 !rounded-card" />
            <Skeleton className="h-20 !rounded-card" />
          </div>
        ) : list.error && !list.data ? (
          <ErrorState error={list.error} onRetry={() => void list.refetch()} />
        ) : items.length === 0 ? (
          <EmptyState icon={<Bell className="size-8" />} title={S.notif.empty} sub={S.notif.emptySub} />
        ) : (
          <div className="space-y-5 px-5">
            {today.length > 0 && (
              <section aria-label={S.notif.today}>
                <h2 className="mb-2 text-[13px] text-mute">{S.notif.today}</h2>
                <ul className="overflow-hidden rounded-card border border-line bg-surface">{today.map((n) => <Row key={n.id} n={n} onOpen={open} />)}</ul>
              </section>
            )}
            {earlier.length > 0 && (
              <section aria-label={S.notif.earlier}>
                <h2 className="mb-2 text-[13px] text-mute">{S.notif.earlier}</h2>
                <ul className="overflow-hidden rounded-card border border-line bg-surface">{earlier.map((n) => <Row key={n.id} n={n} onOpen={open} />)}</ul>
              </section>
            )}
          </div>
        )}
      </div>
    </div>
  )
}
