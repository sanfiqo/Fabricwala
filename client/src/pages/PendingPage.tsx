import { Navigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { Clock, ShieldAlert, XCircle } from 'lucide-react'
import { homeFor, useMe } from '@/auth'
import { Badge, Card, KeyValue, PageLoading } from '@/components/ui/misc'
import { Button } from '@/components/ui/button'
import { S, label } from '@/i18n'
import { useState } from 'react'

export default function PendingPage() {
  const { data: me, isLoading } = useMe()
  const qc = useQueryClient()
  const [busy, setBusy] = useState(false)
  if (isLoading) return <PageLoading />
  const b = me?.business
  if (!me || !b || b.verificationStatus === 'Approved') return <Navigate to={homeFor(me)} replace />

  const rejected = b.verificationStatus === 'Rejected'
  const suspended = b.verificationStatus === 'Suspended'
  const Icon = rejected ? XCircle : suspended ? ShieldAlert : Clock
  const title = rejected ? S.pending.rejectedTitle : suspended ? S.pending.suspendedTitle : S.pending.title
  const body = rejected ? S.pending.rejectedBody : suspended ? S.pending.suspendedBody : S.pending.body
  const refresh = async () => {
    setBusy(true)
    await qc.invalidateQueries({ queryKey: ['me'] })
    setBusy(false)
  }

  return (
    <div className="px-4 py-10 text-center">
      <div className={`mx-auto flex size-24 items-center justify-center rounded-full ${rejected ? 'bg-bad-bg text-bad' : suspended ? 'bg-warn-bg text-warn' : 'bg-leaf-50 text-leaf-700'}`}>
        <Icon className="size-12" />
      </div>
      <h1 className="mt-5 text-2xl font-extrabold text-navy-900">{title}</h1>
      <p className="mx-auto mt-2 max-w-sm text-mute">{body}</p>

      {rejected && b.rejectionNote && (
        <Card className="mx-auto mt-5 max-w-sm border-bad/30 bg-bad-bg p-4 text-left">
          <p className="text-sm font-bold text-bad">{S.pending.note}</p>
          <p className="mt-1 text-ink">{b.rejectionNote}</p>
        </Card>
      )}

      <Card className="mx-auto mt-6 max-w-sm p-4 text-left">
        <KeyValue label={S.pending.business} value={b.displayName} />
        <KeyValue label={S.reg.type} value={b.type === 'Retailer' ? S.reg.retailer : S.reg.manufacturer} />
        <KeyValue
          label={S.pending.status}
          value={<Badge tone={rejected || suspended ? 'bad' : 'warn'}>{label(S.status, b.verificationStatus)}</Badge>}
        />
      </Card>

      <Button variant="outline" className="mt-6" onClick={() => void refresh()} loading={busy}>
        {S.pending.refresh}
      </Button>
    </div>
  )
}
