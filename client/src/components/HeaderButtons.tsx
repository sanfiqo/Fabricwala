import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ShoppingBag } from 'lucide-react'
import { useCartCount } from '@/components/AppShell'
import { S } from '@/i18n'
import { cn } from '@/lib/utils'

/** Round outlined icon button used in screen headers. */
export const roundIcon = 'press relative flex size-[46px] items-center justify-center rounded-full border border-line bg-surface text-ink'

export const CART_ADDED_EVENT = 'tc:cart-added'

/** Cart icon with item count. It bumps once whenever something is added to the cart. */
export function CartLink({ className }: { className?: string }) {
  const count = useCartCount()
  const [bumps, setBumps] = useState(0)
  useEffect(() => {
    const on = () => setBumps((n) => n + 1)
    window.addEventListener(CART_ADDED_EVENT, on)
    return () => window.removeEventListener(CART_ADDED_EVENT, on)
  }, [])
  return (
    <Link to="/shop/cart" viewTransition aria-label={count > 0 ? `${S.nav.cart} (${count})` : S.nav.cart} className={cn(roundIcon, className)}>
      <span key={bumps} className={cn('flex', bumps > 0 && 'bump')}>
        <ShoppingBag className="size-5" aria-hidden />
      </span>
      {count > 0 && <span className="absolute -right-0.5 -top-0.5 flex min-w-5 items-center justify-center rounded-full bg-ink px-1 text-[11px] font-semibold leading-5 text-surface">{count}</span>}
    </Link>
  )
}
