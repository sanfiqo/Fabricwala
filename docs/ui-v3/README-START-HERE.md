# FabricWala — FINAL UI V3.0: Black & White Sourcing UI / Implementation-Ready Documentation (Planning)

**Updated:** 2026-11-20  |  **GitHub read-only baseline:** `sanfiqo/Fabricwala / main / 35d0f0e318617e1f8cae2bed82275bbcb7902f24`.

## Start here (one command to an AI developer)
> Read `19-SOURCE-OF-TRUTH-AND-DECISIONS.md`, `18-EXISTING-REPOSITORY-ROUTE-AND-GAP-AUDIT.md`, `20-SCREEN-TO-CODE-IMPLEMENTATION-MATRIX.md`, `21-API-CONTRACT-RFQ-AND-COMMERCE.md`, `22-PHASED-TASKS-AND-ACCEPTANCE.md`, `23-V2-1-DOCUMENTATION-CONFLICT-AUDIT.md` and original V2 documents `00`–`17`. Audit only; do not modify the repository. Verify the local working tree and baseline SHA; report differences. Preserve catalogue purchase flows, OTP auth, permissions and RFQ award behavior. Never assume a proposed design screen is already implemented. List risks/tests before asking for implementation approval.

## Files / hierarchy
- `00`–`17`: retained and reconciled Ivory Atelier design/motion/screen materials; legacy speculative language is overridden by V2.1 reality notes.
- `18`: source-based route/gap audit from the prior pass.
- `19`: authority hierarchy, protected rules, open owner decisions.
- `20`: screen/route/code mapping and reuse guidance.
- `21`: existing RFQ endpoint/rules, cart/checkout preservation, unsupported features.
- `22`: phased file impact, dependencies, exact acceptance and rollback gates.
- `23`: documentation conflict audit and residual gaps.

**Visual:** approved Ivory Atelier light treatment with warm neutrals; editorial serif only in selected public headings, accessible business UI; existing app may still use older tokens and fonts.

**Important status:** Version 2.1 is implementation-*planning ready*; it is **not** a tested app, production sign-off, completed UI, executed migration or permission to change the GitHub repository.


## V2.2 addendum (20 Nov 2026)
- **Read `24-MOBILE-FEEL-SPEC.md` first for visual work.** It plus the "V2.2" sections at the end of `01`, `03`, `04`, `08` override older styling text (no-glass rule, taupe borders, mocha text, flat-only inputs).
- `css/` contains working CSS, a contrast checker and a rendered preview. Tokens were contrast-audited; see 08.
- Protected business rules from V2.1 are unchanged. Still no implementation authorization and no real-device testing.

## V2.3 (20 Nov 2026)
**Palette decision: LAGOON** (teal + peach pastel gradients) replaces Ivory/espresso everywhere. Start with `24-MOBILE-FEEL-SPEC.md` and the V2.3 section at the end of 08. Use `css/fabricwala-pastel-kit.html` as the visual reference instead of generating images.


## V3.0 — FINAL (20 Nov 2026)
**Read `25-BW-SOURCING-UI-SPEC.md` first.** It replaces all earlier visual directions. Code: `css/fabricwala-bw.css`; visual reference: `css/reference-screens.html` (+ `.png`); contrast check: `python3 css/check-bw.py`. Business/API/permission rules in `18`–`23` are unchanged. No implementation authorization, no real-device testing yet.


## V3.1 — Home screen features (20 Nov 2026)
Added `26-HOME-SCREEN-FEATURES-SPEC.md` plus working placeholder reference `css/home-screen.html` / `.png` (filters, bulk estimate, swatch board, WhatsApp, Hindi toggle, sample orders). Read after `25`.

## V3.2 — Mill / manufacturer profile (20 Nov 2026)
Added `27-SUPPLIER-PROFILE-SPEC.md` and reference `css/supplier-profile.html` / `.png` (identity, stats, verification, factory, reviews, contact gating, request-quote sheet, supplier profile-strength meter). Read after `26`.

## V3.3 — Import / Export module (20 Nov 2026)
Added `28-IMPORT-EXPORT-MODULE-SPEC.md` and working references `css/import-export.html` / `.png` (bulk CSV product import with validation, data export) plus the Export section and IEC verification in `css/supplier-profile.html`. Part A = data import/export; Part B = international export/import trade enquiries (lead generation only in v1). Read after `27`.

## V3.4 — Overseas buyer signup (20 Nov 2026)
Owner decision: overseas buyers may sign up in v1. Added `29-OVERSEAS-BUYER-SIGNUP-SPEC.md` and working reference `css/overseas-signup.html` / `.png` (4-step flow with email verification, status model, admin review, compliance list). Read after `28`. Auth changes need audit + owner approval first.

## V3.5 — Admin buyer review queue (20 Nov 2026)
Added `30-ADMIN-BUYER-REVIEW-SPEC.md` and working reference `css/admin-buyer-review.html` (+ mobile and desktop screenshots): queue with counts, SLA, risk flags, approve / request info / reject, masked tax ID with logged reveal, audit log. Read after `29`.

## V3.6 — End-to-end flow with sequence numbers (20 Nov 2026)
Added `31-END-TO-END-USER-FLOW.md`: numbered flows A (register), L (login), X (lost access, replaces "forgot password"), B (retailer profile), C (mill onboarding and documents), D (overseas), E (admin), F (trade), S (session). Use these Seq numbers in tickets and Codex prompts. Read it before 29 and 30.

## V3.7 — Additional features backlog (20 Nov 2026)
Added `32-ADDITIONAL-FEATURES-BACKLOG.md`: ~90 proposed features by area with effort, phase and backend need, a revenue-model warning, and a suggested first-10 order. Ideas only; nothing approved.

## V3.8 — Feature map (20 Nov 2026)
Added `33-FEATURE-MAP.md`: one index linking every feature (FM-xxx built/specified, BL-x.y backlog) to its spec, reference screen and flow Seq number. Use the IDs in tickets and Codex prompts.


## V3.9 — Export globe (20 Nov 2026)
Added `34-EXPORT-GLOBE-COMPONENT.md`, the component `css/fw-globe.js`, the standalone `css/export-globe.html`, and live use in `css/overseas-signup.html` and `css/supplier-profile.html`. Feature map has FM-607. Land outlines are approximate; replace with Natural Earth data (task in 34).
