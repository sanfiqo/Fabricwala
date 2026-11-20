# FabricWala — Route-to-Screen & Design Gap Audit

**Date:** 2026-11-20  
**Source:** connected `sanfiqo/Fabricwala`, `main` branch, read-only source inspection.  
**Target:** `FabricWala_Ivory_Atelier_V2_Documentation` (19 markdown files, especially 09, 13 and 14).  
**Confidence:** route existence and inspected files confirmed; UI rendering, deployed features, latest local branches and database not independently verified.  
**Changes made to repository:** None.

## Executive conclusions

1. **Do not rebuild.** Existing app uses React/Vite/TypeScript PWA + ASP.NET Core minimal APIs + MySQL. The commerce flow and UI primitives are substantial.
2. **Separate two business journeys.** Existing `product -> cart -> checkout -> order -> shipment -> payout` must be preserved. RFQ backend is newly implemented behind an admin feature switch, but its frontend is not routed. RFQ acceptance **does not create a payable order**.
3. **Ivory Atelier v2 is a visual specification, not a replacement business contract.** Its generic login/password, role switch, RFQ attachments and messaging assumptions need reconciliation with actual code and backend capabilities.
4. **Responsive web is a real integration gap.** `AppShell` uses `max-w-xl` outside admin; public desktop pages need a different responsive frame without breaking phone journeys.
5. **Cinematic fabric globe** needs its own performance-budgeted, progressive-enhancement slice; never make product discovery depend on 3D.

## Evidence inventory

- Current routes: `client/src/App.tsx`
- Navigation and layout: `client/src/components/AppShell.tsx`, `FloatingTabBar.tsx`
- Design tokens and current fonts: `client/src/index.css`
- Product and motion reuse: `client/src/components/ProductCard.tsx`, `ProductImage.tsx`, `components/product/PhotoGallery.tsx`, `src/motion/presets.ts`
- Catalog/order/seller/admin adapters: `client/src/api/areas/`
- OTP authentication: `client/src/pages/LoginPage.tsx`, `client/src/auth.tsx`
- RFQ business contract: `docs/RFQ.md`, API descriptions `docs/API_REFERENCE.md`, `docs/openapi.json`
- Actual progress claims: `docs/PROGRESS.md`, test evidence descriptions `client/docs/NIGHT_STATE.md`
- V2 design intent: `09-SCREEN-INVENTORY-AND-PAGE-CONTRACTS.md`, `13-RFQ-QUOTATION-MATCHING-AND-PERMISSIONS.md`, `08-DESIGN-TOKENS-AND-THEME-CONTRACT.md`, `14-IMPLEMENTATION-READINESS-AND-PHASE-PLAN.md`

## Route-to-screen matrix

Legend: **REUSE** present route, keep business behavior; **RESTYLE** visual-only; **EXTEND** add content/responsiveness while keeping behavior; **NEW** no matching route in inspected `App.tsx`; **VERIFY** backend or policy requires separate confirmation.

| V2 area | Existing route(s) on `main` | Current evidence | Mapping / safe action |
| --- | --- | --- | --- |
| Landing/Home P01 / Buyer B01 | `/shop` | `ShopHome` | RESTYLE+EXTEND; distinct anonymous public landing NEW (root `/` currently redirects by auth) |
| Search/catalog P02/P04 | `/shop/products` | `ProductListPage`, `productApi.list` | REUSE+RESTYLE; desktop sidebar, filters/scroll preservation to test |
| Category P03 / Collections P08 | no dedicated route | category discovery in shopping screens | NEW dedicated landing/editorial routes if approved |
| Product detail P05 | `/shop/p/:slug` | `ProductPage`, gallery, slabs, variants, MOQ, Add to Cart | REUSE; add quote entry point, supplier crosslink, sharing. **Do not remove Add to Cart** |
| Supplier directory P06 | no route | none in inspected router | NEW; check API capability before specifying implementation |
| Public showroom P07 | no route | business/seller pages are not a public showroom | NEW; permissions and published business data contracts required |
| RFQ explanation P09 | no route | RFQ backend separate | NEW optional public explanation; no private RFQ leakage |
| Pricing P10 | no route | subscription plan rules not verified | PROPOSED only; do not imply billing exists |
| About/help P11 | no dedicated route | account help may exist | EXTEND / policy content requires approval |
| Sign-in A02 | `/login` | `LoginPage` uses mobile OTP; no password | RESTYLE; **do not invent password recovery flow** |
| Registration A03/A07 | `/register` | `RegisterPage` plus business approval | RESTYLE; preserve business onboarding and permissions |
| Pending A06 | `/pending` | `PendingPage` | RESTYLE |
| Session expired G | `/session-expired` | screen present | REUSE+RESTYLE |
| Onboarding A01 | no route | not confirmed | PROPOSED optional |
| Forgot/reset password A04/A05 | no route | OTP auth instead | **NOT APPLICABLE without auth scope change** |
| Workspace switch A08 | no route | `areaOf` derives Admin or business type | **NOT SUPPORTED AS SPECIFIED**; needs backend policy and memberships |
| Buyer saved B02 | no dedicated route | saved-products API described by backend | NEW screen / VERIFY adapter and API contract |
| Buyer RFQ B04-B09 | no route | backend RFQ feature exists | NEW frontend pages and adapters; design within actual backend fields/states |
| Buyer chat B10 | no route | no chat in router | NEW feature with backend and privacy plan |
| Notifications B11 | `/shop/notifications`, `/seller/notifications` | shared `NotificationsPage` | RESTYLE, link to RFQs after integration |
| Buyer account B12 | `/shop/account` | `AccountPage` | RESTYLE |
| Buyer cart/checkout/orders | `/shop/cart`, `/shop/checkout`, `/shop/orders`, `/shop/orders/:id`, `/shop/orders/:id/report` | current production-oriented commerce journey | **PRESERVE** and restyle; omitted from V2 screen inventory, ADD THEM TO DOCS |
| Supplier dashboard S01 | `/seller` | `SellerHome` | RESTYLE+EXTEND |
| Supplier inventory S02/S03 | `/seller/products`, `/seller/products/new`, `/seller/products/:id` | listing and editor | REUSE+RESTYLE |
| Supplier orders/payout | `/seller/orders`, `/seller/orders/:id`, `/seller/payouts` | current operations | **PRESERVE**; add these to V2 inventory |
| Supplier RFQ S05-S08 | no route | RFQ backend manufacturer endpoints | NEW frontend UI+adapter |
| Supplier showroom editor S09/S10 | no route | seller editor is product-specific | NEW, requires data model / publishing scope |
| Supplier business profile S12 | `/seller/account` | shared AccountPage | RESTYLE+EXTEND |
| Admin overview M01 | `/admin` | `AdminHome` | RESTYLE |
| Admin approvals M03 | `/admin/businesses`, `/admin/businesses/:id` | review and approval | RESTYLE |
| Admin settings M10 | `/admin/settings` | platform settings | RESTYLE; theme draft/publish/rollback is NEW functionality |
| Admin shipping | `/admin/shipping` | admin shipping page | **PRESERVE**; absent from V2 screen inventory |
| Admin RFQ M05 | no route | backend admin RFQ list/close exists | NEW UI, moderation policy existing |
| Admin themes M07 | no dedicated route | CSS vars static in `index.css` | NEW admin-controlled semantic theme feature |
| Cinematic globe | no component/route | V2 storyboard, no app equivalent | NEW isolated hero enhancement with poster/video fallback |

## Critical technical/business mismatches

### BLOCKER B1 — RFQ post-acceptance and commission
`docs/RFQ.md` confirms award reveals each side's contact; **no paid order is created** and platform receives no commission on that deal. V2 design document 13 says acceptance unapproved; this is outdated relative to current backend. Update design documentation to reflect current backend *as implemented*, then separately identify whether payable custom orders are desired. Never use 'Pay' or 'Order placed' after an RFQ acceptance today.

### BLOCKER B2 — Retailer vs manufacturer, not arbitrary buyer/supplier switching
Existing auth routes derive area using Admin role or business type. V2 'same app with switch workspace' is a design aspiration, not implemented authorization. Do not show roles a user has not been granted; don't break cookie+CSRF auth.

### BLOCKER B3 — Product fabric measurement
`ProductPage.tsx` already supports variant quantities, tier prices, MOQ and unit-aware steps. `seller.ts` maps real seller product lists to unit `Piece` in one adapter. Confirm API units and remove unsafe hardcoded assumptions *before* presenting uniform 'meters' in UI. Fabric listing may legitimately use Piece, meter, kg, etc.

### BLOCKER B4 — Existing checkout must survive redesign
The V2 screen inventory is missing cart, checkout, tracking, delivery confirmation, reporting problems, payments, seller orders and payouts. These are present and should be added as protected screen contracts before implementation. 'Request Quote' can be an additional contextual action, not a blanket replacement for 'Add to Cart'.

### BLOCKER B5 — Production proof and data contracts
`docs/PROGRESS.md` claims 582 MySQL suite tests passed with 2 skipped as of 18 Nov, plus catalogue benchmark exceptions. This audit did **not** run build, browser E2E, database migrations or real API calls. Record current commit SHA, API schema drift, test proof and branch changes before changing code.

### HIGH H1 — Responsive shell
Non-admin shell currently uses `max-w-xl`; desktop store is constrained. Plan role-specific responsive containers and desktop header/sidebar separate from admin sidebar. Keep existing phone bottom actions and route guards unchanged.

### HIGH H2 — Theme control
`index.css` defines tokens; `client/design-ref/tokens.json` is an older reference. V2 requires Ivory Atelier tokens, Cormorant Garamond editorial font and Manrope business font; current CSS loads Inter/Noto Sans Devanagari. Plan one token source, preserve Hindi fallback, validate WCAG contrast, audit every component including legacy navy aliases. Admin publication/preview/rollback requires new persistence and authorization: changing CSS alone is not full admin theme support.

### HIGH H3 — Social/showroom and messaging
`ProductPage.tsx` uses `waLink`, but that does not prove full system sharing, Instagram, public showroom or private chat. Public links need canonical URLs and metadata; supplier contact opt-in; private RFQ links must require authorization and avoid open-graph leaks. No unsupported Instagram autopost promises.

### HIGH H4 — Product visual motion
Existing `ProductCard.tsx` already uses React Router view transitions; `src/motion/presets.ts` has reusable durations, springs and reduced-motion patterns. Extend rather than replace, and isolate cinematic globe tiered fallbacks from product pages.

## Revision required in V2 design documentation

- **09 Screen Inventory:** add actual commerce screens; mark password recovery not applicable; separate anonymous landing from logged-in shop home; reflect RFQ as built; show admin shipping and seller payouts.
- **13 RFQ/Permissions:** replace proposed RFQ status and acceptance assumptions with `docs/RFQ.md` current statuses; document 10-open limit, per-manufacturer quotation, privacy, notification, and no-payable-order caveat. Keep richer RFQ fields / direct invites / files marked FUTURE.
- **08 Tokens:** retain Ivory Atelier style as target; map actual CSS names and legacy aliases; explicitly retain Devanagari font support and product swatch data independent of UI theme.
- **11 Responsive:** name existing `max-w-xl` shell as a migration item; preserve mobile tab and sticky actions.
- **14 Implementation:** add source-code audit and protection gates for checkout/order/payout/current auth.
- **15 QA:** add regressions for role guards, OTP flow, checkout idempotency, GST, commission, payouts, RFQ privacy and feature switch.

## Safe delivery phases

| Phase | Scope | Must prove before advancing |
| --- | --- | --- |
| 0 | Capture commit SHA, local-branch divergence, baseline screenshots/tests; freeze current routes/API | Read-only inventory complete; protected-flow checklist agreed |
| 1 | Revise V2 documentation to current backend facts, close business decisions | Owner approves business model and deviations |
| 2 | Semantic Ivory Atelier tokens, font loading, shared components | Contrast, EN/HI, mobile visual regression |
| 3 | Responsive public shell, catalog/product/home restyling | Existing cart/checkout/order E2E green |
| 4 | Supplier directory/showroom and public share links | Permissions, indexing, non-public data protection |
| 5 | RFQ buyer/supplier/admin frontends using existing APIs | Permissions, expiry, acceptance concurrency, feature switch |
| 6 | Admin theme manager, preview/rollback (if approved) | Admin-only authorization, WCAG, rollback proof |
| 7 | Optional cinematic globe / supporting scroll motion | Mobile budget, poster fallback, reduced-motion support |
| 8 | Full visual + functional regression, performance and deployment audit | Separate launch sign-off |

## Explicit no-change guarantee
No repository files, tests, database tables, branches, deployment resources or API configurations were modified by this audit. Nothing was executed against the user's local environment. This file is a **planning artifact**, not proof that the application passes runtime acceptance tests.

## Links
- Repository: https://github.com/sanfiqo/Fabricwala
- React routes: https://github.com/sanfiqo/Fabricwala/blob/main/client/src/App.tsx
- RFQ backend specification: https://github.com/sanfiqo/Fabricwala/blob/main/docs/RFQ.md
- Existing architecture: https://github.com/sanfiqo/Fabricwala/blob/main/docs/ARCHITECTURE.md
- Progress: https://github.com/sanfiqo/Fabricwala/blob/main/docs/PROGRESS.md
