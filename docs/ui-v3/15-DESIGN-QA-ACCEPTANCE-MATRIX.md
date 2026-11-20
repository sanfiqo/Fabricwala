# 15 — Design Acceptance Matrix & Audit Checklist

## How to use
**Spec ready** means criteria written; **Pass** requires inspected Figma/mockup/prototype/code evidence. Never mark a visual or functional test passed just because it appears in documentation.

| ID | Area | Acceptance criteria | Evidence required |
|---|---|---|---|
| QA01 | Brand | Consistent ivory/espresso usage across public, buyer, supplier and admin | Side-by-side screenshots |
| QA02 | Theme | Admin preview/publish changes only color semantics, without geometry change | Two theme visual diff + token inspection |
| QA03 | Contrast | WCAG AA text and controls across themes/statuses | Contrast report + manual QA |
| QA04 | Typography | Consistent scale, no clipped content at 200% zoom | Responsive screenshots |
| QA05 | Product image | True fabric colors, correct crop/zoom, no theme filter | Reference image comparison |
| QA06 | Product card | Image-first; name/selected specs/MOQ; accessible detail link; uncluttered | Card variant/screenshots |
| QA07 | Product detail | Gallery/swatches/verified details/specs/RFQ and related sections | Desktop/mobile prototype |
| QA08 | Supplier showroom | Shareable cover, identity, collection/product browsing, enquiry | Public link and screen flow |
| QA09 | Links/social | Copy, WhatsApp, native share, Instagram where supported; public previews | Device tests and canonical URLs |
| QA10 | Buyer dashboard | Clear RFQ, quotes, inbox, saved; fast actions | Walkthrough |
| QA11 | Supplier dashboard | Enquiries, quotes, products, showroom management | Walkthrough |
| QA12 | RFQ wizard | Three steps, prefill, draft recovery, validation, submit feedback | Scenario cases |
| QA13 | Quote compare | Comparable units, validity, cost inclusions; not falsely rankable | Data scenario tests |
| QA14 | Authentication | Login/register/recovery, error/loading, accessible forms | Test matrix |
| QA15 | Modal catalog | Confirm/drawer/bottom sheet/toast/image viewer states | Component storybook/prototypes |
| QA16 | Skeleton/empty/error | Every major screen has 3 distinct states | Screen-state inventory |
| QA17 | Responsive | All target viewport/input sizes, safe area and keyboard states | Device grid screenshots |
| QA18 | Back/scroll | Product back returns filtered list/position | Navigation scenario |
| QA19 | Motion | Editorial scenes enhance, forms/quotes stay responsive | Motion prototype |
| QA20 | Globe fallback | Search usable without WebGL, video, or animation; reduced-motion works | Slow-device/assistive test |
| QA21 | Role permissions | Buyer vs supplier vs admin actions displayed appropriately | Role walkthrough and backend authorization test |
| QA22 | Trust | Badges and claims are backed by verified data | Source data audit |
| QA23 | Performance | No material main-thread slowdown or layout shift from hero | Measured field/lab data |
| QA24 | Security/privacy UX | Public vs private RFQ/share links properly separated | Permission scenario |
| QA25 | Consistency | All screens use same token/component families; no one-off arbitrary styles | Visual inventory |

## Audit reporting format
Per criterion record `Not started / Spec ready / Design ready / Verified / Failed / Blocked`, evidence link, issue severity, remediation, reviewer/date. As of this documentation release, **all are at most Spec ready**; no actual UI validation claimed.

## Severity
- P0: privacy/authorization, corrupt business data, unavailable core action.
- P1: broken responsive workflow, inaccessible critical action, false verification or quote comparison.
- P2: design drift, missing supported modal/empty state, inconsistent component.
- P3: minor visual polish.


---

## V2 QA gates
- Reference mood: ivory/beige/porcelain editorial aesthetic matches supplied AIDA feel without source cloning.
- Color: every screen uses 08 palette, with no leftover purple/dark-heavy visual source.
- Typography: editorial Cormorant Garamond limited to storytelling; Manrope for operational UI.
- Buttons/card families: espresso action, light porcelain cards, consistent radii and simple indicators.
- Data integrity: sample-only stats and products visibly marked as mock data; no fake trust badges.
- Motion: warm pearl globe with pause/skip/poster plus `prefers-reduced-motion`; business flows remain stable.
- Pass/fail must be based on actual evaluated screens, not on a document claim. All tests are **Not Run** until a prototype exists.


## V2.1 protected regression gates

- OTP login, resend, approval pending, admin MFA when enabled, cookie + CSRF; role/business isolation.
- Current catalogue units, price slabs/MOQ, cart, idempotent checkout/payment, GST/commission, order tracking, delivery confirm, dispute/report, payout hold/release and admin shipping unchanged.
- RFQ enabled/disabled routes, retailer 10-open rule, manufacturer quote isolation, no buyer contact leak before award, correct concurrency on award; RFQ award must not claim payable order.
- Public link metadata must not expose private contact or RFQ content; WhatsApp/Instagram must use platform-supported share semantics.
- Responsive desktop, Hindi text, touch targets, reduced-motion globe fallback and no horizontal overflow.
- All gates need actual runtime/e2e/test evidence on the implementer's environment; source code existence does not equal passing tests.
