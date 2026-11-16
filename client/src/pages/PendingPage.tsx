import { useState } from 'react'
import { Navigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { Check, Clock, MessageCircle, ShieldAlert, XCircle } from 'lucide-react'
import { authApi } from '@/api/endpoints'
import { isReal } from '@/api/client'
import { homeFor, useMe } from '@/auth'
import { Button } from '@/components/ui/button'
import { LinkButton, PageLoading } from '@/components/ui/misc'
import { fmt, S } from '@/i18n'
import { cn } from '@/lib/utils'
import { waLink } from '@/lib/whatsapp'

function Row({ ok, title, sub }: { ok: boolean; title: string; sub: string }) {
  return (
    <li className="flex items-center gap-3 border-t border-line px-4 py-3.5 first:border-t-0">
      {ok ? (
        <span className="flex size-7 shrink-0 items-center justify-center rounded-full bg-success text-white" aria-hidden>
          <Check className="size-4" />
        </span>
      ) : (
        <span className="size-7 shrink-0 rounded-full border-2 border-dashed border-line" aria-hidden />
      )}
      <span className="min-w-0 flex-1">
        <span className="block text-[15px] font-medium">{title}</span>
        <span className="block text-[13px] text-mute">{sub}</span>
      </span>
    </li>
  )
}

/** Waiting for approval (also shows "not approved" and "on hold"). */
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
    // After an approval the session cookie must be renewed, or it still says "not approved".
    if (isReal('auth/refresh')) await authApi.refresh().catch(() => undefined)
    await qc.invalidateQueries({ queryKey: ['me'] })
    setBusy(false)
  }
  const waking = !rejected && !suspended

  return (
    <div className="flex min-h-[80dvh] flex-col px-5 pt-14">
      <span className={cn('flex size-[72px] items-center justify-center rounded-full border', rejected ? 'border-error/30 bg-error-bg text-error' : 'border-line bg-surface')} aria-hidden>
        <Icon className="size-8" />
      </span>
      <h1 className="mt-5 text-balance text-[32px] font-semibold leading-[1.15] tracking-[-0.025em]">{title}</h1>
      <p className="mt-2.5 text-base leading-relaxed text-mute">{body}</p>

      {rejected && b.rejectionNote && (
        <div role="note" className="mt-5 rounded-card bg-error-bg p-4">
          <p className="text-sm font-medium text-error">{S.pending.note}</p>
          <p className="mt-1">{b.rejectionNote}</p>
        </div>
      )}

      <ul className="mt-6 overflow-hidden rounded-card border border-line bg-surface">
        <Row ok title={S.pending.mobileVerified} sub={`+91 ${me.phone}`} />
        <Row ok title={S.pending.businessDetails} sub={b.displayName} />
        <Row ok={false} title={S.pending.teamReview} sub={rejected ? S.status.Rejected : suspended ? S.status.Suspended : S.pending.inProgress} />
      </ul>

      {waking && b.type === 'Retailer' && <p className="mt-4 rounded-2xl bg-saving-bg p-3.5 text-sm leading-snug text-saving">{S.pending.tillThen}</p>}

      <div className="mt-auto flex flex-col gap-2 pb-[max(22px,env(safe-area-inset-bottom))] pt-8">
        {waking && b.type === 'Retailer' && <LinkButton to="/shop" className="min-h-14 w-full text-base">{S.pending.startBrowsing}</LinkButton>}
        <Button variant={waking && b.type === 'Retailer' ? 'soft' : 'primary'} size="lg" block onClick={() => void refresh()} loading={busy}>
          {S.pending.refresh}
        </Button>
        <a href={waLink(fmt(S.pending.waMsg, { name: b.displayName }))} target="_blank" rel="noreferrer" className="press flex min-h-12 items-center justify-center gap-2 font-medium">
          <MessageCircle className="size-5" aria-hidden />
          {S.pending.askWa}
        </a>
      </div>
    </div>
  )
}
