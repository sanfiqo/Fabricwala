# 22 — Phased Tasks, File Impact, Dependencies and Test Gates

Baseline: `sanfiqo/Fabricwala / main / 35d0f0e318617e1f8cae2bed82275bbcb7902f24`. **Planning only: no runtime tests performed or repository modified.** Define each PR narrowly, never overwrite existing client/backend files wholesale. Follow repo `docs/AGENTS.md`; one agent working session at a time.

| Phase | Change scope and candidate impacted paths | Backend dependency | Exit / acceptance evidence | Risk |
|---|---|---|---|---|
| P0 Baseline | read `docs/AGENTS.md`, `client/src/App.tsx`, `client/src/api`, `docs/openapi.json`, current CSS, CI; save route snapshot + screenshots in separate audit artifacts | Git SHA, local tree and DB availability | Owner confirms baseline; existing tests/build/E2E captured without altering data | HIGH if missing |
| P1 Docs contract sign-off | V2.1 docs, decision ledger, protected-flow matrix | RFQ current DTO/states, roles, paid orders | Approval of 2 commercial journeys and optional features; conflicts zero | HIGH |
| P2 Tokens + shared primitives | `client/src/index.css`, `client/src/components/ui/*`, `client/src/components/ProductCard.tsx`, `ProductImage.tsx`; font assets | None | EN+HI, text 4.5:1 (normal), visible focus, visual snapshots mobile/desktop, no color hardcode regression | MED |
| P3 Responsive shell + current storefront | `client/src/components/AppShell.tsx`, `FloatingTabBar.tsx`, `client/src/pages/shop/{ShopHome,ProductListPage,ProductPage}.tsx`, shell nav | Current catalog APIs | Mobile cart/checkout/order regression PASS, desktop 1280+ usable, no layout overflows; no route guard regression | HIGH |
| P4 Current commercial workspaces | buyer order/cart/checkout/report and seller products/orders/payouts; admin current screens | Existing order/finance APIs | Status and monetary totals unchanged, idempotency proof, real test data clearly labeled | HIGH |
| P5 RFQ frontend | `client/src/api/areas/rfq.ts` (new), `types.ts`, `keys.ts`, `endpoints.ts`, new pages, `App.tsx`, `AppShell.tsx` | Existing RFQ API + `Feature.Rfq`, actual DTOs | Retailer create/list/cancel/award; manufacturer own quotes; admin close; 10-open/409/privacy/off switch tests | HIGH |
| P6 Public directory/showroom/share | new public pages, route rules, API endpoint(s)/DTO if authorized, SEO metadata | **No verified public showroom API** | Public visibility consent, deep links, share sheet/WhatsApp, private identity protected, no 404 on auth return | HIGH new feature |
| P7 Admin theme manager | new semantic theme persistence/UI, `admin/settings` additions after design, preview/rollback | **New server contract required** | Only admin publishes, safe rollback, cache sync across apps, default Ivory accessible | HIGH new feature |
| P8 Signature globe | isolated poster/3D component + assets only on homepage; existing motion presets | No business API | No blocked LCP/text search, low-power fallback, pause/reduced motion, 3D disabled on weak devices | MED optional |
| P9 Regression/release | browser/mobile screenshots, builds/tests, backend migrations only if separately approved, CI | live/test backend, external services | Cross-role E2E, GST/settlement, permissions, assets, performance, deployment signoff | HIGH |

## Concrete handoff template (per task)
- **Task ID:** `FW-V21-Px-yy`; one feature, one atomic PR. Owner-approved? yes/no.
- **Current code:** current path, function/component, SHA; identify affected routes and contracts.
- **Do not touch:** unrelated auth/finance/settlement/migrations, `docs/AGENTS.md`, user modifications.
- **Changes:** file list with create/modify; why existing component cannot cover it.
- **UI:** tokens, states, responsive modes, content, accessibility, i18n and reduced-motion.
- **Behavior:** API requests, errors, permission guards, optimistic updates, fallbacks.
- **Verification:** `npm run lint`, `npm run build`, applicable smoke/E2E, .NET suites configured on test MySQL, screenshot diff; record pass/skip/failure honestly.
- **Rollback:** revert isolated PR/feature flag; do not delete production data.
- **Exit:** owner review and explicit approval before next risky phase.

## Pre-implementation decision gate
Only P0 read-only baseline and documentation review are pre-authorized by this conversation. P2+ code changes require explicit owner approval; P5 RFQ exposure depends on feature/contract privacy review; P6+ independent feature/product approval. No inferred authorization to install dependencies, run migrations, deploy, push, or turn on `Feature.Rfq`.
