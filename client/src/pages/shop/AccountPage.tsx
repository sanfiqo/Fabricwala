import type { ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { Bell, ChevronRight, FileQuestion, FileSpreadsheet, FileText, Heart, LogOut, MapPin, MessageCircle, Store } from 'lucide-react'
import { authApi } from '@/api/endpoints'
import { setUseMocks, USE_MOCKS } from '@/api/client'
import { useMe } from '@/auth'
import { Badge, Segmented } from '@/components/ui/misc'
import { useMyBusiness, useUnreadCount } from '@/hooks'
import { clearSavedData } from '@/queryClient'
import { useFeature } from '@/lib/useFeature'
import { fmt, label, LANGS, S, setLang, useLang } from '@/i18n'
import { waLink } from '@/lib/whatsapp'

const initials = (name: string) =>
  name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w.charAt(0).toUpperCase())
    .join('') || '·'

function Card({ children, title }: { children: ReactNode; title?: string }) {
  return (
    <section aria-label={title} className="mx-5 mt-4 overflow-hidden rounded-card border border-line bg-surface">
      {children}
    </section>
  )
}
const rowCls = 'press flex min-h-[60px] w-full items-center gap-3.5 border-t border-line px-4 py-3 text-left first:border-t-0'
const iconCls = 'flex size-10 shrink-0 items-center justify-center rounded-full bg-soft'

/** Profile, language, help, demo switch and log out. */
export default function AccountPage() {
  const { data: me } = useMe()
  const biz = useMyBusiness()
  const unread = useUnreadCount()
  const rfqOn = useFeature('Rfq')
  const lang = useLang()
  const qc = useQueryClient()
  const nav = useNavigate()
  const status = me?.business?.verificationStatus
  const name = me?.business?.displayName ?? me?.fullName ?? '—'
  const isSeller = me?.business?.type === 'Manufacturer'
  const addrs = biz.data?.addresses.filter((a) => a.addressType === (isSeller ? 'Pickup' : 'Delivery')) ?? []

  const logout = async () => {
    try {
      await authApi.logout()
    } finally {
      qc.clear()
      clearSavedData()
      nav('/login', { replace: true })
    }
  }

  return (
    <div className="pb-4">
      <div className="flex items-center gap-3.5 px-5 pt-[26px]">
        <span className="flex size-16 shrink-0 items-center justify-center rounded-full bg-ink text-xl font-medium text-background" aria-hidden>{initials(name)}</span>
        <div className="min-w-0 flex-1">
          <h1 className="break-words text-[22px] font-semibold leading-tight">{name}</h1>
          <p className="text-[13px] text-mute">+91 {me?.phone}</p>
          {status && (
            <Badge tone={status === 'Approved' ? 'good' : status === 'Pending' ? 'warn' : 'bad'} className="mt-1.5">
              {label(S.status, status)}
            </Badge>
          )}
        </div>
      </div>

      <Card title={S.account.businessInfo}>
        <div className={rowCls.replace('press ', '')}>
          <span className={iconCls} aria-hidden><Store className="size-5" /></span>
          <span className="min-w-0 flex-1">
            <span className="block text-[15px] font-medium">{S.account.businessInfo}</span>
            <span className="block break-words text-[13px] text-mute">{biz.data?.legalName ?? name}{biz.data?.gstin ? ` · ${biz.data.gstin}` : ''}</span>
          </span>
        </div>
        <Link to={isSeller ? '/seller/addresses' : '/shop/addresses'} viewTransition className={rowCls}>
          <span className={iconCls} aria-hidden><MapPin className="size-5" /></span>
          <span className="min-w-0 flex-1">
            <span className="block text-[15px] font-medium">{isSeller ? S.account.pickupTitle : S.account.addressesTitle}</span>
            <span className="block text-[13px] text-mute">{addrs.length === 0 ? S.address.none : addrs.map((a) => `${a.cityName} ${a.pincode}`).join(' · ')}</span>
          </span>
          <ChevronRight className="size-4 text-mute" aria-hidden />
        </Link>
        <Link to={isSeller ? '/seller/notifications' : '/shop/notifications'} viewTransition className={rowCls}>
          <span className={iconCls} aria-hidden><Bell className="size-5" /></span>
          <span className="min-w-0 flex-1 text-[15px] font-medium">{S.account.notifications}</span>
          {unread > 0 && <Badge tone="warn">{fmt(S.notif.newCount, { n: unread })}</Badge>}
          <ChevronRight className="size-4 text-mute" aria-hidden />
        </Link>
        {!isSeller && (
          <Link to="/shop/saved" viewTransition className={rowCls}>
            <span className={iconCls} aria-hidden><Heart className="size-5" /></span>
            <span className="min-w-0 flex-1">
              <span className="block text-[15px] font-medium">{S.saved.title}</span>
              <span className="block text-[13px] text-mute">{S.saved.rowSub}</span>
            </span>
            <ChevronRight className="size-4 text-mute" aria-hidden />
          </Link>
        )}
        {rfqOn && (
          <Link to={isSeller ? '/seller/rfq' : '/shop/rfq'} viewTransition className={rowCls}>
            <span className={iconCls} aria-hidden><FileQuestion className="size-5" /></span>
            <span className="min-w-0 flex-1">
              <span className="block text-[15px] font-medium">{isSeller ? S.rfq.sTitle : S.rfq.title}</span>
              <span className="block text-[13px] text-mute">{isSeller ? S.rfq.sRowSub : S.rfq.rowSub}</span>
            </span>
            <ChevronRight className="size-4 text-mute" aria-hidden />
          </Link>
        )}
        {isSeller && (
          <Link to="/seller/products/bulk" viewTransition className={rowCls}>
            <span className={iconCls} aria-hidden><FileSpreadsheet className="size-5" /></span>
            <span className="min-w-0 flex-1">
              <span className="block text-[15px] font-medium">{S.bulk.open}</span>
              <span className="block text-[13px] text-mute">{S.bulk.sub}</span>
            </span>
            <ChevronRight className="size-4 text-mute" aria-hidden />
          </Link>
        )}
        <Link to="/legal/privacy" viewTransition className={rowCls}>
          <span className={iconCls} aria-hidden><FileText className="size-5" /></span>
          <span className="min-w-0 flex-1 text-[15px] font-medium">{S.legal.title}</span>
          <ChevronRight className="size-4 text-mute" aria-hidden />
        </Link>
      </Card>

      <Card title={S.account.language}>
        <div className="px-4 py-3.5">
          <h2 className="font-medium">{S.account.language}</h2>
          <p className="mb-2.5 text-[13px] text-mute">{S.account.languageSub}</p>
          <Segmented value={lang} onChange={setLang} options={LANGS.map((l) => ({ value: l, label: S.lang[l] }))} />
        </div>
      </Card>

      <Card title={S.account.helpTitle}>
        <a href={waLink(S.cart.waHelpMsg)} target="_blank" rel="noreferrer" className={rowCls}>
          <span className={iconCls} aria-hidden><MessageCircle className="size-5" /></span>
          <span className="flex-1 text-[15px] font-medium">{S.account.helpTitle}: {S.account.helpWa}</span>
          <ChevronRight className="size-4 text-mute" aria-hidden />
        </a>
        <div className="px-4 py-3.5">
          <h2 className="font-medium">{S.account.demo}</h2>
          <p className="mb-2.5 text-[13px] text-mute">{S.account.demoSub}</p>
          <Segmented
            value={USE_MOCKS ? 'on' : 'off'}
            onChange={(v) => {
              if ((v === 'on') !== USE_MOCKS) setUseMocks(v === 'on')
            }}
            options={[
              { value: 'on', label: S.account.demoOn },
              { value: 'off', label: S.account.demoOff },
            ]}
          />
        </div>
      </Card>

      <button type="button" onClick={() => void logout()} className="press mx-5 mt-4 flex min-h-12 w-[calc(100%-40px)] items-center justify-center gap-2 rounded-pill border border-line bg-surface font-medium text-error">
        <LogOut className="size-5" aria-hidden />
        {S.account.logout}
      </button>
      <p className="mt-3.5 text-center text-xs text-mute">{fmt(S.account.appVersion, { v: '1.0' })}</p>
    </div>
  )
}
