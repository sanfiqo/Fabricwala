# 20 — Screen-to-Code Implementation Matrix

**Source:** `sanfiqo/Fabricwala / main / 35d0f0e318617e1f8cae2bed82275bbcb7902f24`. Routes below are from inspected `client/src/App.tsx`, not runtime verified. Proposed paths are candidates, not current routes.

|Area|Current/proposed route|Screen or mapping|File path|Action|Protection/notes|
|---|---|---|---|---|---|
|Shared|`/`|RootRedirect|`client/src/App.tsx`|Reuse|Keep role/auth redirect; anonymous marketing home separate proposal|
|Auth|`/login`|LoginPage|`client/src/pages/LoginPage.tsx`|Restyle|OTP only; no password UI|
|Auth|`/register`|RegisterPage|`client/src/pages/RegisterPage.tsx`|Restyle|Business onboarding and verification intact|
|Auth|`/pending`|PendingPage|`client/src/pages/PendingPage.tsx`|Restyle|Approval gating intact|
|Auth|`/session-expired`|SessionExpiredPage|`client/src/pages/SessionExpiredPage.tsx`|Restyle|401/session recovery intact|
|Buyer|`/shop`|ShopHome|`client/src/pages/shop/ShopHome.tsx`|Restyle+Extend|Current authenticated home; desktop shell work|
|Buyer|`/shop/products`|ProductListPage|`client/src/pages/shop/ProductListPage.tsx`|Restyle|Filters, paging, category and scroll preserve|
|Buyer|`/shop/p/:slug`|ProductPage|`client/src/pages/shop/ProductPage.tsx`|Restyle+Extend|Retain Add to Cart; RFQ optional if enabled|
|Buyer|`/shop/cart`|CartPage|`client/src/pages/shop/CartPage.tsx`|Protect+Restyle|Price, quantity, checkout unaffected|
|Buyer|`/shop/checkout`|CheckoutPage|`client/src/pages/shop/CheckoutPage.tsx`|Protect+Restyle|Idempotency, payment and tax guards|
|Buyer|`/shop/orders`|OrdersPage|`client/src/pages/shop/OrdersPage.tsx`|Protect+Restyle|Order status and reporting|
|Buyer|`/shop/orders/:id`|OrderDetailPage|`client/src/pages/shop/OrdersPage.tsx`|Protect+Restyle|Tracking and delivery confirm|
|Buyer|`/shop/orders/:id/report`|ReportPage|`client/src/pages/shop/ReportPage.tsx`|Protect+Restyle|Claim and payout consequences|
|Buyer|`/shop/account`|AccountPage|`client/src/pages/shop/AccountPage.tsx`|Restyle|Business profile and addresses|
|Buyer|`/shop/notifications`|NotificationsPage|`client/src/pages/NotificationsPage.tsx`|Restyle|Link RFQ notices only once adapter exists|
|Supplier|`/seller`|SellerHome|`client/src/pages/seller/SellerHome.tsx`|Restyle+Extend|Enquiry cards only with actual RFQ integration|
|Supplier|`/seller/products`|SellerProductsPage|`client/src/pages/seller/SellerProductsPage.tsx`|Protect+Restyle|Listings, activation and stock|
|Supplier|`/seller/products/new`|ProductEditorPage|`client/src/pages/seller/ProductEditorPage.tsx`|Protect+Restyle|Server validation, media, variants, slab tiers|
|Supplier|`/seller/products/:id`|ProductEditorPage|`client/src/pages/seller/ProductEditorPage.tsx`|Protect+Restyle|Existing product management|
|Supplier|`/seller/orders`|SellerOrdersPage|`client/src/pages/seller/SellerOrdersPage.tsx`|Protect+Restyle|Accept/reject/dispatch safe|
|Supplier|`/seller/orders/:id`|SellerOrderDetailPage|`client/src/pages/seller/SellerOrdersPage.tsx`|Protect+Restyle|Status machine and shipment rights|
|Supplier|`/seller/payouts`|PayoutsPage|`client/src/pages/seller/PayoutsPage.tsx`|Protect+Restyle|Payout hold/release|
|Supplier|`/seller/account`|AccountPage|`client/src/pages/shop/AccountPage.tsx`|Restyle|Role-guarded shared account|
|Supplier|`/seller/notifications`|NotificationsPage|`client/src/pages/NotificationsPage.tsx`|Restyle|Role-guarded shared notifications|
|Admin|`/admin`|AdminHome|`client/src/pages/admin/AdminHome.tsx`|Restyle|Metrics from server, no fake numbers|
|Admin|`/admin/businesses`|AdminBusinessesPage|`client/src/pages/admin/AdminBusinessesPage.tsx`|Protect+Restyle|Approval and verification|
|Admin|`/admin/businesses/:id`|AdminReviewPage|`client/src/pages/admin/AdminReviewPage.tsx`|Protect+Restyle|Document/privacy restrictions|
|Admin|`/admin/settings`|AdminSettingsPage|`client/src/pages/admin/AdminSettingsPage.tsx`|Protect+Extend|Existing commission settings, theme separate|
|Admin|`/admin/shipping`|AdminShippingPage|`client/src/pages/admin/AdminShippingPage.tsx`|Protect+Restyle|Freight rules|
|Future|`/shop/rfqs, /shop/rfqs/new, /shop/rfqs/:id`|Not routed|`new files after API DTO review`|Backend only|Use existing RFQ REST contract; no paid order|
|Future|`/seller/rfqs, /seller/rfqs/:id, /seller/quotations`|Not routed|`new files after API DTO review`|Backend only|Own quotation only, buyer identity hidden|
|Future|`/admin/rfqs`|Not routed|`new files after API DTO review`|Backend only|List/close only; moderation|
|Future|`/suppliers, /suppliers/:slug`|Not routed|`new feature & API`|Proposed|Publishable public business data policy required|
|Future|`/collections, /categories/:slug`|Not routed|`new or existing catalog reuse`|Proposed|Avoid duplicate data flows|
|Future|`/messages`|Not routed|`new API & UI`|Proposed|Privacy/moderation policy before coding|
|Future|`/admin/appearance`|Not routed|`theme persistence + UI`|Proposed|Admin-only preview/publish/rollback|

## Component reuse (inspect before modification)
| Existing unit | Reuse contract |
|---|---|
| `client/src/components/AppShell.tsx` | Introduce responsive frame while preserving `Gate`, nav guards and safe-area tab rules; current non-admin `max-w-xl` is desktop blocker |
| `client/src/components/ProductCard.tsx`, `ProductImage.tsx` | Image-first Ivory visual skin; preserve link/viewTransition, skeleton and product IDs |
| `components/product/PhotoGallery.tsx`, `VariantPicker.tsx`, `SlabPriceTable.tsx` | Retain real variant/price functionality, preserve unit and MOQ |
| `client/src/components/FloatingTabBar.tsx`, `StickyActionBar.tsx` | Preserve bottom interactions, avoid sticky CTA overlaps |
| `client/src/components/ui/*` | Restyle once using semantic tokens; keep form errors, focus, loading, keyboard |
| `client/src/motion/*` | Extend existing motion durations/reduced motion, no duplicate animation engine |
| `client/src/api/areas/*` | Reuse current adapters; add dedicated RFQ feature adapter only after DTO proof |
| `client/src/lib/colours.ts` | Product color swatches are data, **not** global theme colors |

## Per-screen acceptance template
For every screen: route/role+authorization; existing API; target UI state; preserved user actions; loading/empty/error; mobile 360/390 and desktop 1280/1440; EN/HI; keyboard and contrast; regression cases; screenshots with sample/real mode clearly labeled.

**Scope:** 29 existing routes plus proposed paths in the matrix; not all routes are separate page files. Counts are planning only.
