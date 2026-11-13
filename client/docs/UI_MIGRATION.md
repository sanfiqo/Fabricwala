# UI migration inventory (Step 0, read-only)

Source: `client/CLAUDE_CODE_UI_PROMPT.md`. No code was changed for this file.
Design files are in `client/design-ref/` (27 `.dc.html`, `tokens.json`, `README.txt`). All V2 files named in the prompt exist.

Labels: **keep** (fits), **restyle** (same logic, new look), **rebuild UI** (keep hooks and data, replace markup), **new** (no page yet).
Rule for every row: keep the route URL, hooks, API calls, validators and mock data. Only presentation changes.

## Status

- Step 1 (tokens, fonts, motion, i18n en+hi, shadcn Drawer/Dialog, Toast, floating tab bar, mock switch): done.
- Stage A: Home, Product, Cart rebuilt. Account page added (minimal). Done.
- Not started: Stage B (Search + filter sheet, Orders, Track), C, D, admin, checkout restyle.

## Findings that need a decision or note

1. `docs/UX_RULES.md` does not exist in the repo (the prompt says to follow it). Only `AGENTS.md` exists. I will follow AGENTS.md and the prompt's own rules until you add the file.
2. There are no tests in `client/` (no test runner, no `*.test.*`). "Existing tests must still pass" is trivially true today. Verification stays `npm run build` + `npm run lint` + the headless smoke script.
3. No `components.json` yet. shadcn CLI setup (Base UI) is needed before Drawer, Dialog and Toast can be added. Current components are hand-written (cva, no Radix).
4. Old design files with no V2 (InkSearch, InkTrack, seller screens) use the old bottom bar. Use the floating pill menu from the V2 files everywhere (per design-ref README).
5. Admin screens were designed at phone width, but the real admin runs on a laptop. Admin pages stay usable at both widths.
6. Nothing will be deleted in the plan below.

## Routes (all stay working)

`/`, `/login`, `/register`, `/pending`, `/shop`, `/shop/products`, `/shop/p/:slug`, `/shop/cart`, `/shop/checkout`, `/shop/orders`, `/shop/orders/:id`, `/seller/products`, `/seller/products/new`, `/seller/products/:id`, `/seller/orders`, `/seller/orders/:id`, `/admin/businesses`, `/admin/settings`, `/admin/shipping`, `*` (not found).

## Pages

| Page (file) | Route | Matching design | Action | Notes |
| --- | --- | --- | --- | --- |
| `shop/ShopHome.tsx` | `/shop` | InkHomeV2 | rebuild UI | Add "Order again" card (needs last order, mock ok), search box with mic and camera (new, step later). |
| `shop/ProductListPage.tsx` | `/shop/products` | InkSearch | rebuild UI | Filters move into a bottom sheet. Keep sort, paging, all query params. Old design uses old bar: use pill bar. |
| `shop/ProductPage.tsx` | `/shop/p/:slug` | InkProductV2 | rebuild UI | Keep landed cost, slab table, variants, qty logic. Add gallery thumbnails, quantity chips and "add N more, save" hint (new, from slabs), share and help on WhatsApp. |
| `shop/CartPage.tsx` | `/shop/cart` | InkCartV2 | rebuild UI | Keep grouping by manufacturer and server totals. Add undo toast on remove, count-up total. |
| `shop/CheckoutPage.tsx` | `/shop/checkout` | none (closest: InkCartV2 summary) | restyle | No dedicated design. Keep payment note (Razorpay not wired). Needs address-change sheet. |
| `shop/OrdersPage.tsx` (list) | `/shop/orders` | InkOrdersV2 | rebuild UI | Add "Order again". Empty state per InkEmpty. |
| `shop/OrdersPage.tsx` (`OrderDetailPage`) | `/shop/orders/:id` | InkTrack | rebuild UI | Keep timeline data. Add step-fill animation, share and help on WhatsApp, confirm-delivery stays. |
| `LoginPage.tsx` | `/login` | InkLogin, InkOtp | rebuild UI | Keep two-step flow and demo numbers (mock only). Add language switch, `OtpInput`, number pad. |
| `RegisterPage.tsx` | `/register` | InkBusiness | restyle | 4-step wizard stays (type, names, address, documents). Design shows one screen, so keep steps and style them. Pincode check is new. |
| `PendingPage.tsx` | `/pending` | InkReview | restyle | Check match: InkReview may be the "waiting for approval" screen. Keep rejection note and retry. |
| `seller/SellerProductsPage.tsx` | `/seller/products` | InkSellerProducts | rebuild UI | Small page, keep list and status badges. |
| `seller/ProductEditorPage.tsx` | `/seller/products/new`, `/:id` | InkAddProduct | restyle | Largest logic page (variants, slab validation, photos). Keep every feature, restyle fields and sheets. |
| `seller/SellerOrdersPage.tsx` (list) | `/seller/orders` | InkSellerOrders | rebuild UI | Keep tabs, accept, reject with reason. |
| `seller/SellerOrdersPage.tsx` (`SellerOrderDetailPage`) | `/seller/orders/:id` | InkSellerOrders (partial) | restyle | Dispatch with LR number, status steps and commission lines have no design: keep and style. |
| `admin/AdminBusinessesPage.tsx` | `/admin/businesses` | InkAdminApprovals, InkAdminReview | rebuild UI | List maps to Approvals, the detail sheet to Review. Keep approve, reject-needs-note. |
| `admin/AdminSettingsPage.tsx` | `/admin/settings` | InkAdminHome (partial) | restyle | Design shows a home, not settings. Keep form, restyle. |
| `admin/AdminShippingPage.tsx` | `/admin/shipping` | none | restyle (later) | No design. Keep working, restyle with the new primitives at the end. |
| not-found, `RootRedirect` (in `App.tsx`) | `*`, `/` | InkEmpty (style only) | restyle | Trivial. |

## Designs with no page yet (new)

| Design | What it is | Note |
| --- | --- | --- |
| InkAccount | Account page, language switch, logout | New route (suggest `/shop/account`, `/seller/account`). |
| InkNotifications | Notifications list | New. Backend `GET notifications` exists in the spec but the client does not use it yet. Mock first. |
| InkReport | "Report a problem" sheet | New, a bottom sheet. Backend endpoint unknown: mark `TODO(BE)`. |
| InkOffline | Offline state | New: `OfflineBanner` plus cached screens. |
| InkSellerHome | Seller home (new route `/seller`) | New. Today `/seller` has no index page. |
| InkSellerPayouts | Seller payouts | New. Backend payouts endpoint is not in my contract: mock with `TODO(BE)`. |
| InkAdminHome | Admin home (new route `/admin`) | New, dashboard counts. Mock with `TODO(BE)`. |
| InkEmpty | Empty orders | Not a page, a state. Fold into `EmptyState`. |
| InkCart, InkHome, InkOrders, InkProduct | Older versions | Superseded by V2, ignore. |

## Shared components

| File | Role | Action | Becomes |
| --- | --- | --- | --- |
| `components/AppShell.tsx` | header, bottom nav per role, cart badge | rebuild UI | `Header` (round back) + `FloatingTabBar` (retailer: Home, Search, Orders, Account; seller: Home, Orders, Products, Payouts). Admin keeps a simple top nav. |
| `components/ProductCard.tsx` | product tile | rebuild UI | soft card with photo shadow, container query. |
| `components/QtyStepper.tsx` | quantity control | restyle | `Stepper` + `QuantityChips`. |
| `components/OrderParts.tsx` | status badge and timeline | rebuild UI | animated timeline, status chips. |
| `components/ui/button.tsx` | cva button | restyle | primary pill, soft, ghost. |
| `components/ui/field.tsx` | Field, Input, Select, Textarea | restyle | soft-fill inputs, plus `PhoneInput`, `OtpInput` (new). |
| `components/ui/misc.tsx` | Card, Badge, Segmented, Sheet, PageHeader, EmptyState, ErrorState, Skeleton and loading | restyle, then replace `Sheet` | `Sheet` becomes shadcn Drawer. Skeleton replaces spinners (`PageLoading`). |
| `components/ui/toast.tsx` | toast context | rebuild UI | shadcn Toast with Undo action. |
| `index.css` | navy/leaf tokens, stitch divider | rebuild | new `@theme` tokens from `tokens.json`. Navy/leaf classes are used across all pages, so tokens get renamed in one pass per screen. |
| `i18n/en.ts`, `i18n/index.ts` | `S` object, `fmt`, `label` | keep | Add `hi.ts` and a language switch. `S.*` call sites stay. |
| `auth.tsx`, `hooks.ts`, `api/*`, `lib/*` | logic | **keep** | No change. |

## Order of work (matches prompt section 6)

1. Tokens, fonts, motion presets, i18n (`hi`), base layout, `components.json`.
2. Primitives, then layout (Header, FloatingTabBar, Drawer, StickyActionBar).
3. Stage A: Home, Product, Cart. Stop for review on phone.
4. Stage B: Search with filter sheet, Orders, Track order. Stop for review.
5. Stage C: Login, OTP, Business, Pending.
6. Stage D: Seller home, Seller orders, Add product. Then admin, checkout, shipping rates, and the new pages (Account, Notifications, Payouts, Report).

Per screen: finish it fully (no half-old, half-new), run build and lint, run the headless smoke script, commit "P6.x" small.
