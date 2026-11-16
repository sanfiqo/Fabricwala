import { Suspense } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Home, Inbox, LayoutDashboard, LogOut, Package, Search, Settings, Truck, User, UserCheck, Wallet } from 'lucide-react'
import { authApi, cartApi } from '@/api/endpoints'
import { USE_MOCKS } from '@/api/client'
import { areaOf, useMe, type Area } from '@/auth'
import { FloatingTabBar, type TabItem } from '@/components/FloatingTabBar'
import { OfflineBanner } from '@/components/OfflineBanner'
import { PageLoading } from '@/components/ui/misc'
import { clearSavedData } from '@/queryClient'
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
      { to: '/shop/orders', label: S.nav.orders, icon: Package },
      { to: '/shop/account', label: S.nav.account, icon: User },
    ]
  }
  if (area === 'Manufacturer') {
    return [
      { to: '/seller', label: S.nav.home, icon: Home, end: true },
      { to: '/seller/orders', label: S.nav.orders, icon: Inbox },
      { to: '/seller/products', label: S.nav.products, icon: Package },
      { to: '/seller/payouts', label: S.nav.payouts, icon: Wallet },
    ]
  }
  if (area === 'Admin') {
    return [
      { to: '/admin', label: S.nav.home, icon: LayoutDashboard, end: true },
      { to: '/admin/businesses', label: S.nav.approvals, icon: UserCheck },
      { to: '/admin/shipping', label: S.nav.shipping, icon: Truck },
      { to: '/admin/settings', label: S.nav.settings, icon: Settings },
    ]
  }
  return []
}

/** Screens that have their own bottom action bar hide the floating menu (as in the design files). */
const NO_TABS = [
  /^\/seller\/orders\/\d+/,
  /^\/seller\/products\/(new|\d+)/,
  /^\/seller\/notifications/,
  /^\/admin\/businesses\/\d+/,
  /^\/shop\/p\//,
  /^\/shop\/cart/,
  /^\/shop\/checkout/,
  /^\/shop\/orders\/\d+\/report/,
]

/** Laptop navigation for admins: a left column, keyboard friendly. On phones the floating tab bar is used instead. */
function AdminSidebar({ items, onLogout, name }: { items: TabItem[]; onLogout: () => void; name: string }) {
  return (
    <aside className="sticky top-0 hidden h-dvh w-60 shrink-0 flex-col border-r border-line bg-surface p-4 lg:flex">
      <div className="flex items-center gap-3 px-2 pb-6 pt-2">
        <BrandMark />
        <div className="min-w-0 leading-tight">
          <p className="truncate font-semibold">{S.app.name}</p>
          <p className="truncate text-xs text-mute">{name}</p>
        </div>
      </div>
      <nav aria-label={S.ad.menu} className="flex flex-1 flex-col gap-1">
        {items.map((i) => (
          <NavLink
            key={i.to}
            to={i.to}
            end={i.end}
            className={({ isActive }) => cn('press flex min-h-12 items-center gap-3 rounded-pill px-4 text-[15px]', isActive ? 'bg-ink font-medium text-background' : 'text-ink hover:bg-soft')}
          >
            <i.icon className="size-5" aria-hidden />
            {i.label}
          </NavLink>
        ))}
      </nav>
      <button type="button" onClick={onLogout} className="press flex min-h-12 items-center gap-3 rounded-pill px-4 text-[15px] text-error hover:bg-soft">
        <LogOut className="size-5" aria-hidden />
        {S.nav.logout}
      </button>
    </aside>
  )
}

export function AppShell() {
  const { data: me } = useMe()
  const qc = useQueryClient()
  const nav = useNavigate()
  const { pathname } = useLocation()
  const area = me ? areaOf(me) : null
  const items = tabsFor(area)
  const admin = area === 'Admin'
  const showTabs = items.length > 0 && !NO_TABS.some((r) => r.test(pathname))
  const name = me?.business?.displayName ?? me?.fullName ?? me?.phone ?? ''

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
    <div className={cn('mx-auto min-h-dvh bg-background', admin ? 'max-w-6xl lg:flex' : 'max-w-xl')}>
      {admin && <AdminSidebar items={items} onLogout={() => void logout()} name={name} />}
      <div className="min-w-0 flex-1">
        <a href="#main" className="sr-only focus:not-sr-only focus:absolute focus:left-3 focus:top-3 focus:z-50 focus:rounded-full focus:bg-ink focus:px-4 focus:py-2 focus:text-background">
          {S.app.skip}
        </a>
        <OfflineBanner />
        {USE_MOCKS && admin && (
          <Link to="/" className="block bg-saving-bg px-4 py-1.5 text-center text-xs text-saving">
            {S.app.demo}
          </Link>
        )}
        {/* Phones: slim header for admins (laptops have the sidebar). Shopkeepers and sellers draw their own headers. */}
        {admin && (
          <header className="flex items-center gap-3 px-4 pt-4 lg:hidden">
            <BrandMark />
            <div className="min-w-0 flex-1 leading-tight">
              <p className="truncate font-semibold">{S.app.name}</p>
              <p className="truncate text-sm text-mute">{name}</p>
            </div>
            <button type="button" onClick={() => void logout()} className="press flex min-h-12 items-center gap-2 rounded-full bg-soft px-4 text-sm font-medium">
              <LogOut className="size-4" aria-hidden />
              {S.nav.logout}
            </button>
          </header>
        )}
        <main id="main" className={cn(showTabs ? 'pb-32 lg:pb-8' : 'pb-6', admin && 'lg:px-4')}>
          <Suspense fallback={<PageLoading />}>
            <Outlet />
          </Suspense>
        </main>
      </div>
      {showTabs && <FloatingTabBar items={items} className={admin ? 'lg:hidden' : undefined} />}
    </div>
  )
}
