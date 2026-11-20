# 14 — Design-to-Implementation Readiness Plan (No Code Changes)

## Strict phase gate
This documentation pack is design planning, **not an audit of the actual FabricWala codebase**. Do not change repository, dependencies, DB, API or deployed services based on these files alone. A later repository audit is a separate step requiring explicit authorization.

## Phase 0 — Documentation freeze
- Read files 00–15 (and 16 audit report), record all versions and unresolved decisions.
- Create a single requirement traceability matrix with `feature → role → screen IDs → components → states → business rule → design status → test case`.
- Confirm user sign-off on palette and 3D hero storyboard; otherwise retain as candidate specs.

## Phase 1 — Visual design tokens
Deliver actual visual design samples for typography, colors, buttons, inputs, 5+ card types, tabs, drawers, bottom nav, status semantics, empty/loading states, responsive examples. Validate contrast and content scaling. Approval requires side-by-side screenshots for mobile/desktop.

## Phase 2 — Public marketplace high-fidelity
P01–P11 desktop/mobile layouts and interaction prototypes, with product photography and textile-accurate color. Prioritize home, catalog, product detail, supplier showroom; review transitions and responsive behavior.

## Phase 3 — Business workspaces high-fidelity
Buyer B01–B12, supplier S01–S13, auth A01–A08, admin M01–M10, global G states. Include modal inventory, backend data assumptions, and accessibility. Obtain separate sign-off for workflows.

## Phase 4 — Motion prototype
Build a standalone concept prototype (not existing app): globe Tier 0 poster, Tier 1 pre-rendered, then optional Tier 2. Benchmark representative mobile and desktop devices; honor reduced motion. Never require the Tier 3 simulation for basic product usability.

## Phase 5 — Repository audit (only after explicit approval)
Read-only inspect actual code: package manifests, framework versions, modules, routes, APIs, DB entities/migrations, auth, roles, RFQ & quote implementation, image pipeline, SEO, assets, tests, CI/CD, existing UI tokens and shared components. Record `confirmed / partial / absent / unknown` with file paths and revision hash. Never infer missing code from these prompts.

## Phase 6 — Gap analysis
Compare approved designs against audit evidence; classify: reuse / restyle / extend / new / remove only if approved. Estimate complexity and side effects. Maintain protected existing workflows. Every change has affected file list, prerequisites, acceptance tests and rollback.

## Phase 7 — Controlled implementation
Implement **only after user approval** in thin slices: tokens → shared components → public pages → roles/dashboards → RFQ/quotes → admin → hero. Preview one slice, verify and sign off before moving forward. Never run destructive migrations or silently replace existing project files.

## Phase 8 — Full QA and launch readiness
Visual regression, responsive QA, keyboard/screenreader, contrast, reduced-motion, role-based authorization, quote correctness, link/share, slow-device performance, staging verification and controlled release. Track release blockers explicitly.

## Change control format
Each change ticket: ID, source specification, affected screens, why, visual references, files expected to change, risk, acceptance criteria, test evidence, rollback, owner approval. No implementation from vague prose alone.

## Definition of done for documentation
- Every screen contract exists and references tokens, components, states and permissions.
- Every feature marked confirmed/proposed/open and its owner identified.
- No contradictions across files after precedence update.
- Cinematic hero includes storyboard and fallback.
- QA tests are measurable, but actual UI/code pass results remain **unverified until built and tested**.


---

## V2 design lock and safe sequencing
Do not implement from the legacy V1 palette. First validate V2 screenshot/mockup against the supplied AIDA mood reference and 08/15; approve typography and token contrast. Then verify globe storyboard/fallback, role flows and responsive states. Only then, with separate user approval, begin repository read-only audit and implementation planning. At this stage V2 is **documentation updated, not a completed UI or a code audit**. No changes to the GitHub Fabricwala repository are authorized by this artifact.


## V2.1 Implementation Reality Contract — takes precedence over older exploratory wording

- **Verified code baseline (read-only):** `sanfiqo/Fabricwala` `main` at commit `35d0f0e318617e1f8cae2bed82275bbcb7902f24` (retrieved 20 Nov 2026). Source inspection is **not** proof of running behavior; local/unpushed code may differ.
- **Two protected commercial journeys:** retain existing catalog → cart → checkout → paid order → shipment → payout; offer RFQ as an additional sourcing journey only when the `Rfq` feature is enabled. RFQ award gives contacts, **not** a paid order or platform commission.
- **Identity:** mobile OTP / HttpOnly cookie / CSRF, roles `Retailer`, `Manufacturer`, `Admin`. Do not invent password/reset UI, unrestricted role switching or dual role memberships.
- **Unit accuracy:** never assume all goods use meters; support the product's actual API unit and preserve variant/slab/MOQ pricing logic.
- **Visual rules:** Ivory Atelier V2 color intent remains unchanged; treat published token hex values as *candidate* pending visual/contrast review. Preserve Hindi typography fallback and fabric-color accuracy.
- **Scope labels:** `IMPLEMENTED (source)`, `WIRED FRONTEND`, `BACKEND ONLY`, `PROPOSED`, `BLOCKED DECISION`, and `UNVERIFIED RUNTIME` must not be conflated.
- See `19-SOURCE-OF-TRUTH-AND-DECISIONS.md`, `20-SCREEN-TO-CODE-IMPLEMENTATION-MATRIX.md`, `21-API-CONTRACT-RFQ-AND-COMMERCE.md`, `22-PHASED-TASKS-AND-ACCEPTANCE.md`, `23-V2-1-DOCUMENTATION-CONFLICT-AUDIT.md` before using any legacy instructions below. **No implementation authorization is granted by this package.**
