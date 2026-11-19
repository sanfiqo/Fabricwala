import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { FileQuestion } from 'lucide-react'
import { rfqApi } from '@/api/endpoints'
import { qk } from '@/api/keys'
import { RfqCard } from '@/components/RfqParts'
import { EmptyState, ErrorState, LinkButton, PageHeader, Segmented, Skeleton } from '@/components/ui/misc'
import { S } from '@/i18n'

type Tab = 'open' | 'done' | 'old'

/** "My requests": what the shop asked for, with how many quotations came. Tabs: open, awarded, closed. */
export default function RfqListPage() {
  const [tab, setTab] = useState<Tab>('open')
  const list = useQuery({ queryKey: qk.rfqs(), queryFn: () => rfqApi.list() })
  const all = list.data?.items ?? []
  const pick = (t: Tab) => all.filter((r) => (t === 'open' ? r.status === 'Open' : t === 'done' ? r.status === 'Awarded' : r.status !== 'Open' && r.status !== 'Awarded'))
  const items = pick(tab)

  return (
    <div className="pb-6">
      <PageHeader title={S.rfq.mineTitle} back="/shop/account" action={<LinkButton to="/shop/rfq/new" className="!min-h-11 px-4 text-sm">{S.rfq.newAction}</LinkButton>} />
      <div className="px-4 pt-4">
        <Segmented
          value={tab}
          onChange={setTab}
          options={[
            { value: 'open', label: S.rfq.tabOpen, count: pick('open').length },
            { value: 'done', label: S.rfq.tabDone, count: pick('done').length },
            { value: 'old', label: S.rfq.tabOld, count: pick('old').length },
          ]}
        />
        <div className="mt-4">
          {list.isLoading ? (
            <div role="status" aria-label={S.app.loading} className="space-y-3">
              <Skeleton className="h-[112px] !rounded-card" />
              <Skeleton className="h-[112px] !rounded-card" />
            </div>
          ) : list.error ? (
            <ErrorState error={list.error} onRetry={() => void list.refetch()} />
          ) : items.length === 0 ? (
            <EmptyState
              icon={<FileQuestion className="size-7" aria-hidden />}
              title={S.rfq.emptyMine}
              sub={S.rfq.emptyMineSub}
              action={tab === 'open' ? <LinkButton to="/shop/rfq/new">{S.rfq.newAction}</LinkButton> : undefined}
            />
          ) : (
            <ul className="space-y-3">
              {items.map((r) => (
                <li key={r.id}>
                  <RfqCard r={r} to={`/shop/rfq/${r.id}`} />
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  )
}
