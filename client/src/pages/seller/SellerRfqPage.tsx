import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { FileQuestion } from 'lucide-react'
import { sellerRfqApi } from '@/api/endpoints'
import { qk } from '@/api/keys'
import { RfqCard, unitLabel } from '@/components/RfqParts'
import { SearchBox } from '@/components/SearchBox'
import { Badge, EmptyState, ErrorState, PageHeader, Segmented, Skeleton } from '@/components/ui/misc'
import { fmt, S } from '@/i18n'
import { money } from '@/lib/utils'

type Tab = 'feed' | 'mine'

/** Manufacturer: open requests from shops, and the quotations this manufacturer has sent. */
export default function SellerRfqPage() {
  const [tab, setTab] = useState<Tab>('feed')
  const [term, setTerm] = useState('')
  const feed = useQuery({ queryKey: qk.sellerRfqs(term), queryFn: () => sellerRfqApi.feed(term || undefined), enabled: tab === 'feed' })
  const mine = useQuery({ queryKey: qk.sellerQuotes, queryFn: () => sellerRfqApi.mine(), enabled: tab === 'mine' })
  const active = tab === 'feed' ? feed : mine

  return (
    <div className="pb-6">
      <PageHeader title={S.rfq.sTitle} back="/seller/account" />
      <div className="space-y-3 px-4 pt-4">
        <Segmented
          value={tab}
          onChange={setTab}
          options={[
            { value: 'feed', label: S.rfq.sTabFeed },
            { value: 'mine', label: S.rfq.sTabMine },
          ]}
        />
        {tab === 'feed' && <SearchBox key="rfq-search" initial={term} onSearch={setTerm} placeholder={S.rfq.sSearch} className="w-full" />}

        {active.isLoading ? (
          <div role="status" aria-label={S.app.loading} className="space-y-3">
            <Skeleton className="h-[112px] !rounded-card" />
            <Skeleton className="h-[112px] !rounded-card" />
          </div>
        ) : active.error ? (
          <ErrorState error={active.error} onRetry={() => void active.refetch()} />
        ) : tab === 'feed' ? (
          (feed.data?.items.length ?? 0) === 0 ? (
            <EmptyState icon={<FileQuestion className="size-7" aria-hidden />} title={S.rfq.sEmptyFeed} sub={S.rfq.sEmptyFeedSub} />
          ) : (
            <ul className="space-y-3">
              {feed.data?.items.map((r) => (
                <li key={r.id}>
                  <RfqCard r={r} to={`/seller/rfq/${r.id}`} footer={r.iHaveQuoted ? <Badge tone="good">{S.rfq.iQuoted}</Badge> : undefined} />
                </li>
              ))}
            </ul>
          )
        ) : (mine.data?.items.length ?? 0) === 0 ? (
          <EmptyState icon={<FileQuestion className="size-7" aria-hidden />} title={S.rfq.sEmptyMine} sub={S.rfq.sEmptyMineSub} />
        ) : (
          <ul className="space-y-3">
            {mine.data?.items.map(({ rfq, quotation }) => (
              <li key={quotation.id}>
                <RfqCard
                  r={rfq}
                  to={`/seller/rfq/${rfq.id}`}
                  footer={
                    <span className="flex flex-wrap items-center justify-between gap-2">
                      <span className="font-medium tabular-nums">{fmt(S.rfq.perUnit, { price: money(quotation.pricePerUnit), unit: unitLabel(rfq.unit) })}</span>
                      <Badge tone={quotation.status === 'Accepted' ? 'good' : quotation.status === 'Rejected' ? 'warn' : 'neutral'}>{S.rfq.quoteStatus[quotation.status]}</Badge>
                    </span>
                  }
                />
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}
