import { Suspense } from 'react'
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ClipboardList, Home, Inbox, LogOut, Package, Search, Settings, Truck, User, UserCheck } from 'lucide-react'
import { authApi, cartApi } from '@/api/endpoints'
import { USE_MOCKS } from '@/api/client'
import { areaOf, useMe, type Area } from '@/auth'
import { FloatingTabBar, type TabItem } from '@/components/FloatingTabBar'
import { OfflineBanner } from '@/components/OfflineBanner'
import { PageLoading } from '@/components/ui/misc'
import { S } from '@/i18n'
import { cn } from '@/lib/utils'

export function BrandMark({ className }: { className?: string }) {
  return (
    <span className={cn('flex size-10 items-center justify-center rounded-full bg-ink', className)} aria-hidden>
      <svg viewBox="0 0 24 24" className="size-5">
        <path d="M4 5h16v3h-6.5v11h-3V8H4z" fill="var(--color-background)" />
      </svg>
    </span>
  )
}

/** Number of items in the cart (for the badge). Shared by the header and the shell. */
export function useCartCount(enabled = true) {
  const cart = useQuery({ queryKey: ['cart'], queryFn: cartApi.get, enabled })
  return cart.data?.groups.reduce((a, g) => a + g.items.length, 0) ?? 0
}

function tabsFor(area: Area | null): TabItem[] {
  if (area === 'Retailer') {
    return [
      { to: '/shop', label: S.nav.home, icon: Home, end: true },
      { to: '/shop/products', label: S.nav.search, icon: Search },
      { to: '/shop/orders', label: S.nav.orders, icon: ClipboardList },
      { to: '/shop/account', label: S.nav.account, icon: User },
    ]
  }
  if (area === 'Manufacturer') {
    return [
      { to: '/seller/products', label: S.nav.products, icon: Package },
      { to: '/seller/orders', label: S.nav.inbox, icon: Inbox },
    ]
  }
  if (area === 'Admin') {
    return [
      { to: '/admin/businesses', label: S.nav.approvals, icon: UserCheck },
      { to: '/admin/shipping', label: S.nav.shipping, icon: Truck },
      { to: '/admin/settings', label: S.nav.settings, icon: Settings },
    ]
  }
  return []
}

/** Screens that have their own bottom action bar hide the floating menu (as in the design files). */
const NO_TABS = [/^\/shop\/p\//, /^\/shop\/cart/, /^\/shop\/checkout/]

export function AppShell() {
  const { data: me } = useMe()
  const qc = useQueryClient()
  const nav = useNavigate()
  const { pathname } = useLocation()
  const area = me ? areaOf(me) : null
  const items = tabsFor(area)
  const showTabs = items.length > 0 && !NO_TABS.some((r) => r.test(pathname))

  const logout = async () => {
    try {
      await authApi.logout()
    } finally {
      qc.clear()
      nav('/login', { replace: true })
    }
  }

  return (
    <div className="mx-auto min-h-dvh max-w-xl bg-background">
      <a href="#main" className="sr-only focus:not-sr-only focus:absolute focus:left-3 focus:top-3 focus:z-50 focus:rounded-full focus:bg-ink focus:px-4 focus:py-2 focus:text-background">
        {S.app.skip}
      </a>
      <OfflineBanner />
      {USE_MOCKS && (
        <Link to={area === 'Retailer' ? '/shop/account' : '/'} className="block bg-saving-bg px-4 py-1.5 text-center text-xs text-saving">
          {S.app.demo}
        </Link>
      )}
      {/* Shopkeeper screens draw their own header (see design files). Other roles keep a slim one until their screens are migrated. */}
      {area !== 'Retailer' && (
        <header className="flex items-center gap-3 px-4 pt-4">
          <BrandMark />
          <div className="min-w-0 flex-1 leading-tight">
            <p className="truncate font-semibold">{S.app.name}</p>
            <p className="truncate text-sm text-mute">{me?.business?.displayName ?? me?.fullName ?? me?.phone}</p>
          </div>
          <button type="button" onClick={() => void logout()} className="press flex min-h-12 items-center gap-2 rounded-full bg-soft px-4 text-sm font-medium">
            <LogOut className="size-4" aria-hidden />
            {S.nav.logout}
          </button>
        </header>
      )}
      <main id="main" className={cn(showTabs ? 'pb-32' : 'pb-6')}>
        <Suspense fallback={<PageLoading />}>
          <Outlet />
        </Suspense>
      </main>
      {showTabs && <FloatingTabBar items={items} />}
    </div>
  )
}
