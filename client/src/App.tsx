import { Navigate, Route, Routes } from 'react-router-dom'
import { useMe, homeFor, useUnauthorizedListener, Gate } from '@/auth'
import { AppShell } from '@/components/AppShell'
import { PageLoading, LinkButton, EmptyState } from '@/components/ui/misc'
import { S } from '@/i18n'
import LoginPage from '@/pages/LoginPage'
import RegisterPage from '@/pages/RegisterPage'
import PendingPage from '@/pages/PendingPage'
import ShopHome from '@/pages/shop/ShopHome'
import ProductListPage from '@/pages/shop/ProductListPage'
import ProductPage from '@/pages/shop/ProductPage'
import CartPage from '@/pages/shop/CartPage'
import CheckoutPage from '@/pages/shop/CheckoutPage'
import { OrdersPage, OrderDetailPage } from '@/pages/shop/OrdersPage'
import SellerProductsPage from '@/pages/seller/SellerProductsPage'
import ProductEditorPage from '@/pages/seller/ProductEditorPage'
import { SellerOrdersPage, SellerOrderDetailPage } from '@/pages/seller/SellerOrdersPage'

function RootRedirect() {
  const { data: me, isLoading } = useMe()
  if (isLoading) return <PageLoading />
  return <Navigate to={homeFor(me)} replace />
}

export default function App() {
  useUnauthorizedListener()
  return (
    <Routes>
      <Route path="/" element={<RootRedirect />} />
      <Route path="/login" element={<LoginPage />} />

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

      <Route path="*" element={<EmptyState title={S.app.notFound} action={<LinkButton to="/">{S.app.goHome}</LinkButton>} />} />
    </Routes>
  )
}
