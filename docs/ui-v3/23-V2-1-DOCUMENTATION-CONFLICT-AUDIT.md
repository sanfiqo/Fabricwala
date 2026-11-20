# 23 — V2.1 Documentation Reconciliation and Conflict Audit

**Scope:** all original 19 V2 docs and read-only route-gap audit, observed main SHA `35d0f0e318617e1f8cae2bed82275bbcb7902f24`. This is a **documentation-level** check, not a functional QA result.

| Conflict | V2 wording/problem | Code/source evidence | V2.1 remedy | Residual status |
|---|---|---|---|---|
| A01 | Replace Add to Cart with Request Quote | Product page calls cart API; payment/order paths exist | Retain current buy path; RFQ as optional second path | Fixed docs; UI work pending |
| A02 | Password reset / forgot screens | Login uses OTP, no password routes | Mark not applicable; specify OTP states | Fixed docs |
| A03 | Buyer ↔ Supplier workspace switching | `auth.tsx` derives role from business/admin | Mark future permission design; current role gates preserved | Fixed docs |
| A04 | RFQ award and statuses 'undecided' | `docs/RFQ.md` describes implementation/award | Record exact states, 10-open limit, award and contact privacy | Fixed docs; DTO review needed |
| A05 | RFQ acceptance implies payment/commission | RFQ award gives contact only | Prohibit payable-order copy without custom-order product decision | Fixed docs |
| A06 | Missing cart, checkout, orders, shipping, payouts in screen inventory | `App.tsx` routes present | Add actual route inventory in file 09 and matrix 20 | Fixed docs |
| A07 | Public homepage/showroom assumed existing | `/` redirects to role area; no public showroom route | Mark proposed and require API/publishing policy | Fixed docs; new feature |
| A08 | Uniform meters unit in product specs | Product supports unit/variant; seller adapter currently sets `Piece` in summary | Dynamic units; validate adapter and DTO before work | Fixed docs; code audit pending |
| A09 | Theme is admin configurable now | CSS vars fixed and admin UI lacks publishing | Theme persistence, admin auth, preview/rollback future | Fixed docs; new feature |
| A10 | Manrope/Cormorant already app fonts | `index.css` uses Inter/Noto Devanagari | Mark target fonts only and preserve Hindi fallback | Fixed docs; load/contrast tests pending |
| A11 | Infinite cinema animation everywhere | Motion presets and phone budgets; globe not built | Isolated progressive-enhancement story with poster & reduced-motion | Fixed docs; build pending |
| A12 | WhatsApp/Instagram share & chat assumed implemented | Existing `waLink`; no verified Instagram publishing/chat | Treat device share targets and chat as separate scopes | Fixed docs; feature work pending |
| A13 | Supplier targeted matching / attachments built | `docs/RFQ.md` says open RFQ feed; no attachment | Remove unsupported UX claims from 'implemented' | Fixed docs; future choice |
| A14 | 'Backend implemented' means production available | RFQ feature flag off outside dev, migrations may differ | Explicit feature state + runtime verification, never flip flag | Fixed docs; runtime pending |
| A15 | All project pages exist for desktop | Current non-admin shell `max-w-xl` | Desktop responsive shell milestone | Fixed docs; build pending |

## Documentation content checks
- Existing 19 V2 markdown documents preserved, with targeted corrections and reality notes.
- New docs `18`–`23` supply evidence hierarchy, source paths, executable task contract, UI/API mapping, open decision ledger and this audit.
- Color source stays Ivory Atelier; product textile swatches are not global palette tokens.
- Source-backed vs proposed functionality labeled clearly. **Do not characterize proposed tasks as code completed.**

## Unverified and residual risks
1. Full OpenAPI DTO-by-DTO comparison and backend implementation review not performed in this documentation pass; API paths come from repo `docs/RFQ.md`.
2. Current local working tree/branch and deployed versions may differ from audited main SHA.
3. No client build, MySQL tests, browser E2E, color contrast measurements or visual screenshot comparison run here.
4. Public supplier data policy, share preview URL strategy, payable RFQ custom-order policy, and theme persistence are open work.
5. Existing backend releases/production accounts/credentials require separate launch approval.

## Result
**V2.1 docs reconciled for planning**; **implementation readiness is CONDITIONAL**, with P0 runtime baseline and decisions as gates. No claim of production readiness or fully audited runtime functionality.
