# 19 — Source of Truth, Baseline and Decision Register

## Baseline / version
- Audited GitHub: `sanfiqo/Fabricwala / main / 35d0f0e318617e1f8cae2bed82275bbcb7902f24`; observed 2026-11-20. Not a local checkout, live API, deployment, DB or unpushed branch snapshot.
- Design baseline: the 19-file `FabricWala_Ivory_Atelier_V2_Documentation` pack (AIDA reference-inspired visual direction), plus `18-EXISTING-REPOSITORY-ROUTE-AND-GAP-AUDIT.md`.
- Hierarchy for code-facing facts: **runtime verification (once performed)** > **current repository source/actual API models** > `docs/RFQ.md` / architecture / OpenAPI > this V2.1 alignment > exploratory V2 design suggestions. Where sources conflict, block the task and confirm; never silently invent an API field.
- Hierarchy for look-and-feel: user's approved Ivory Atelier direction > V2.1 semantic visual contract > legacy design experiments. Rejected purple visuals are not targets.

## Evidence vocabulary
| Label | Meaning |
| --- | --- |
| Source confirmed | Inspected code/routes/doc on above GitHub commit only |
| Backend only | Backend routes/rules documented; frontend has no corresponding route/adapter |
| Future/proposed | Design requirement, not actual functionality |
| Decision required | Cannot implement safely until product/business choice is made |
| Runtime unverified | No local build/DB/E2E/browser test performed in this engagement |

## Locked / protected behavior
1. One React/Vite PWA serves Retailer/Manufacturer/Admin with current role guards; do not assume multiple roles per identity.
2. Existing catalog order + payment workflow remains operational and must not be replaced by RFQ. Keep Add to Cart.
3. Existing SMS OTP login remains; no password recovery UI by default. Admin MFA exists in backend but off by default; UI for it not currently routed.
4. B2B RFQ already exists behind `Feature.Rfq`: approved retailer creates; manufacturer quotes; retailer awards; contact exchange follows; there is no payable order/commission from award.
5. Product units vary. Maintain product-defined units, MOQ, variant quantity and server-derived tier prices.
6. Brand: FabricWala; internal repo/project code names TextileConnect not automatically renamed.
7. Ivory Atelier is the chosen target style; semantic token palette candidate values require contrast/visual validation. Admin alone controls global themes **if feature built**.
8. No development action is authorized by documentation delivery. No secrets, force pushes, migrations, destructive commands, or existing project file edits.

## Open decision ledger
| ID | Decision | Baseline fact / options | Status / gate |
| --- | --- | --- | --- |
| D01 | Paid custom RFQ orders | Existing RFQ award exchanges contacts only; custom order + commission are separate scope | **BLOCKED DECISION**, not needed to restyle existing flows |
| D02 | Supplier direct invitation | Current RFQ feed is visible to approved manufacturers; direct invites are not evidenced | FUTURE decision; no invented targeted API |
| D03 | Buyer/supplier workspace switch | Current auth area derives roles/business type; no arbitrary switch | FUTURE backend permission design |
| D04 | Public anonymous homepage/showrooms | `/` currently redirects; public authorization and data boundaries not verified | Design proposed; approve API/policy before delivery |
| D05 | Sharing/social links | Existing `waLink` utility; no verified Instagram autopost, supplier contact consent or canonical public preview | Requires URL/privacy policy |
| D06 | Chat and attachments | In-RFQ messaging and attachments absent | Independent backend/security scope |
| D07 | Global theme publishing | CSS vars only; no verified admin persistence/publish endpoint | Build separately incl. preview/rollback |
| D08 | Subscriptions | Entitlements/prices/billing not confirmed | Exclude from phase-1 implementation |
| D09 | Product pricing display | Current catalogue supports price slabs; future 'request price' requires product policy | Maintain live values |
| D10 | Launch compliance and external accounts | Real SMS, storage, payouts, GST/freight confirmation etc documented with gaps | Separate production-release gate |

## Change control
- Any implementation prompt must identify exact baseline SHA, changes and risks, wait for approval, use one working session, preserve unrelated changes and validate regression tests.
- Update this ledger and corresponding acceptance matrix for any owner decision; do not turn a design suggestion into an authorization.


## V2.2 authority note
For visual styling the order is: `24` > V2.2 sections of `08/04/03/01` > rest of `00`–`17`. Business, API, permission and route rules stay governed by `19`–`23` and are not changed by V2.2. New open decision: **D-V22 serif allowed for app screen titles (proposed yes)**.

## V2.3 decision
D-V23 CLOSED: palette = Lagoon (owner choice). Supersedes the Ivory Atelier colour decision above (item 7). Still no implementation authorization.

## V3.0 decision
D-V30 CLOSED: visual direction = Black & White Sourcing UI (file 25). Visual order of authority: `25` > everything else visual. Business rules remain governed by `19`–`23`.
