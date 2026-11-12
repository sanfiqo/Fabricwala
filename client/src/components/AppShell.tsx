import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ClipboardList, Home, LogOut, Package, Settings, ShoppingCart, Truck, UserCheck, Inbox, type LucideIcon } from 'lucide-react'
import { authApi, cartApi } from '@/api/endpoints'
import { USE_MOCKS } from '@/api/client'
import { areaOf, useMe, type Area } from '@/auth'
import { S } from '@/i18n'
import { cn } from '@/lib/utils'

export function BrandMark({ className }: { className?: string }) {
  return (
    <span className={cn('flex size-9 items-center justify-center rounded-xl bg-navy-900', className)} aria-hidden>
      <svg viewBox="0 0 24 24" className="size-5">
        <path d="M4 5h16v3h-6.5v11h-3V8H4z" fill="#fff" />
        <rect x="4" y="20" width="16" height="1.6" rx=".8" fill="#2f9e6b" />
      </svg>
    </span>
  )
}

interface NavItem {
  to: string
  label: string
  icon: LucideIcon
  end?: boolean
  badge?: number
}

function useNav(area: Area | null): NavItem[] {
  const cart = useQuery({ queryKey: ['cart'], queryFn: cartApi.get, enabled: area === 'Retailer' })
  if (area === 'Retailer') {
    const count = cart.data?.groups.reduce((a, g) => a + g.items.length, 0) ?? 0
    return [
      { to: '/shop', label: S.nav.home, icon: Home, end: true },
      { to: '/shop/cart', label: S.nav.cart, icon: ShoppingCart, badge: count },
      { to: '/shop/orders', label: S.nav.orders, icon: ClipboardList },
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

export function AppShell() {
  const { data: me } = useMe()
  const qc = useQueryClient()
  const nav = useNavigate()
  const area = me ? areaOf(me) : null
  const items = useNav(area)

  const logout = async () => {
    try {
      await authApi.logout()
    } finally {
      qc.clear()
      nav('/login', { replace: true })
    }
  }

  return (
    <div className="mx-auto flex min-h-dvh max-w-2xl flex-col bg-white">
      <header className="sticky top-0 z-30 flex items-center gap-3 bg-navy-900 px-4 py-2.5 text-white">
        <BrandMark className="bg-white/10" />
        <div className="min-w-0 flex-1 leading-tight">
          <p className="truncate text-base font-extrabold">{S.app.name}</p>
          <p className="truncate text-xs text-white/70">{me?.business?.displayName ?? me?.fullName ?? me?.phone}</p>
        </div>
        <button type="button" onClick={() => void logout()} aria-label={S.nav.logout} className="flex size-11 items-center justify-center rounded-full hover:bg-white/10">
          <LogOut className="size-5" />
        </button>
      </header>
      {USE_MOCKS && <p className="bg-warn-bg px-4 py-1 text-center text-xs font-semibold text-warn">{S.app.demo}</p>}
      <main className="flex-1 pb-28">
        <Outlet />
      </main>
      {items.length > 0 && (
        <nav className="safe-b fixed inset-x-0 bottom-0 z-30 border-t border-line bg-white" aria-label="Main">
          <div className="mx-auto flex max-w-2xl">
            {items.map((i) => (
              <NavLink
                key={i.to}
                to={i.to}
                end={i.end}
                className={({ isActive }) =>
                  cn('relative flex min-h-16 flex-1 flex-col items-center justify-center gap-0.5 text-xs font-bold', isActive ? 'text-navy-900' : 'text-mute')
                }
              >
                {({ isActive }) => (
                  <>
                    <span className={cn('absolute top-0 h-1 w-10 rounded-b-full', isActive ? 'bg-leaf-600' : 'bg-transparent')} />
                    <span className="relative">
                      <i.icon className="size-6" />
                      {!!i.badge && (
                        <span className="absolute -right-2.5 -top-1.5 flex min-w-5 items-center justify-center rounded-full bg-leaf-600 px-1 text-[11px] leading-5 text-white">{i.badge}</span>
                      )}
                    </span>
                    {i.label}
                  </>
                )}
              </NavLink>
            ))}
          </div>
        </nav>
      )}
    </div>
  )
}
