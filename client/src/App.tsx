import { lazy, Suspense, useEffect } from 'react'
import { preloadShopRoutes } from '@/preload'
import { Navigate, Route, Routes } from 'react-router-dom'
import { useMe, homeFor, useUnauthorizedListener, Gate } from '@/auth'
import { AppShell } from '@/components/AppShell'
import { PageLoading, LinkButton, EmptyState } from '@/components/ui/misc'
import { S } from '@/i18n'
const LoginPage = lazy(() => import('@/pages/LoginPage'))
const RegisterPage = lazy(() => import('@/pages/RegisterPage'))
const PendingPage = lazy(() => import('@/pages/PendingPage'))
const ShopHome = lazy(() => import('@/pages/shop/ShopHome'))
const ProductListPage = lazy(() => import('@/pages/shop/ProductListPage'))
const ProductPage = lazy(() => import('@/pages/shop/ProductPage'))
const CartPage = lazy(() => import('@/pages/shop/CartPage'))
const CheckoutPage = lazy(() => import('@/pages/shop/CheckoutPage'))
const SessionExpiredPage = lazy(() => import('@/pages/SessionExpiredPage'))
const AccountPage = lazy(() => import('@/pages/shop/AccountPage'))
const OrdersPage = lazy(() => import('@/pages/shop/OrdersPage').then((m) => ({ default: m.OrdersPage })))
const OrderDetailPage = lazy(() => import('@/pages/shop/OrdersPage').then((m) => ({ default: m.OrderDetailPage })))
const SellerProductsPage = lazy(() => import('@/pages/seller/SellerProductsPage'))
const ProductEditorPage = lazy(() => import('@/pages/seller/ProductEditorPage'))
const AdminBusinessesPage = lazy(() => import('@/pages/admin/AdminBusinessesPage'))
const AdminSettingsPage = lazy(() => import('@/pages/admin/AdminSettingsPage'))
const AdminShippingPage = lazy(() => import('@/pages/admin/AdminShippingPage'))
const SellerOrdersPage = lazy(() => import('@/pages/seller/SellerOrdersPage').then((m) => ({ default: m.SellerOrdersPage })))
const SellerOrderDetailPage = lazy(() => import('@/pages/seller/SellerOrdersPage').then((m) => ({ default: m.SellerOrderDetailPage })))

function RootRedirect() {
  const { data: me, isLoading } = useMe()
  if (isLoading) return <PageLoading />
  return <Navigate to={homeFor(me)} replace />
}

export default function App() {
  useUnauthorizedListener()
  useEffect(preloadShopRoutes, [])
  return (
    <Suspense fallback={<PageLoading />}>
    <Routes>
      <Route path="/" element={<RootRedirect />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/session-expired" element={<SessionExpiredPage />} />

      <Route element={<Gate />}>
        <Route element={<AppShell />}>
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/pending" element={<PendingPage />} />
        </Route>
      </Route>

      <Route element={<Gate area="Retailer" />}>
        <Route element={<AppShell />}>
          <Route path="/shop" element={<ShopHome />} />
          <Route path="/shop/products" element={<ProductListPage />} />
          <Route path="/shop/p/:slug" element={<ProductPage />} />
          <Route path="/shop/cart" element={<CartPage />} />
          <Route path="/shop/checkout" element={<CheckoutPage />} />
          <Route path="/shop/orders" element={<OrdersPage />} />
          <Route path="/shop/orders/:id" element={<OrderDetailPage />} />
          <Route path="/shop/account" element={<AccountPage />} />
        </Route>
      </Route>

      <Route element={<Gate area="Manufacturer" />}>
        <Route element={<AppShell />}>
          <Route path="/seller/products" element={<SellerProductsPage />} />
          <Route path="/seller/products/new" element={<ProductEditorPage />} />
          <Route path="/seller/products/:id" element={<ProductEditorPage />} />
          <Route path="/seller/orders" element={<SellerOrdersPage />} />
          <Route path="/seller/orders/:id" element={<SellerOrderDetailPage />} />
        </Route>
      </Route>

      <Route element={<Gate area="Admin" />}>
        <Route element={<AppShell />}>
          <Route path="/admin/businesses" element={<AdminBusinessesPage />} />
          <Route path="/admin/settings" element={<AdminSettingsPage />} />
          <Route path="/admin/shipping" element={<AdminShippingPage />} />
        </Route>
      </Route>

      <Route path="*" element={<EmptyState title={S.app.notFound} action={<LinkButton to="/">{S.app.goHome}</LinkButton>} />} />
    </Routes>
    </Suspense>
  )
}
