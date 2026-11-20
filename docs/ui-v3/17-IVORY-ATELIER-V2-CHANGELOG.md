# FabricWala — Ivory Atelier V2 Change Log & Audit

## Source and scope
Visual-feel reference: AIDA Atelier three-phone ivory/beige editorial design image provided by the user. This reference was discussed and endorsed as the preferred mood. This file defines design direction only, not codebase inspection.

## Superseded
- Rejected: purple/indigo render variants, navy sci-fi globe, dark cocoa-heavy marketing panels, saturated CTA gradients, excessive neumorphism.
- Superseded: V1 color token proposal (`#F8F6F3` canvas etc.) and optional two-sans font proposal; V2 source of truth is 08.

## Current V2 design contract
- Warm ivory canvas `#F5F0E9` / porcelain `#FFFCF8`; sand `#E9DED1`, taupe `#C9B8A5`, mocha `#8B7362`, espresso `#2E211B`. All values are approximate design proposals, not pixel-measured from the reference.
- Cormorant Garamond for art-directed editorial headlines; Manrope for all UI body, operational, admin, forms, tables and numbers.
- Product cards 4:5, soft 16–20px rounded porcelain surfaces, fabric natural colors; espresso primary and quiet secondary buttons.
- One buyer/supplier app; role-aware navigation; WhatsApp/native sharing where supported; B2B RFQs rather than generic fashion checkout.
- Cinematic globe is approved as a *concept*: pearl/ivory globe and multiple naturally colored textiles/garments; optimized technical tier remains to be selected.
- Admin controls global semantic theme colors only, no geometry/typography/motion changes; validate contrast.

## Scope of audit
Source-set audit: eighteen V1 Markdown documents reviewed and extended; V2 changes synchronized into their individual topical rules; original source retained separately. Checks are documentary, not an actual visual acceptance test.

## Open approval gates
1. True high-fidelity screens in new Ivory Atelier mood and side-by-side user signoff.
2. Contrast, typography and performance testing on actual interfaces.
3. Business policy decisions (RFQ visibility, quote acceptance, supplier verification, subscriptions).
4. 3D asset licensing, device benchmarks and fallback prototype.
5. Later source repository audit and implementation only with explicit user permission.


## V2.1 Implementation Reality Contract — takes precedence over older exploratory wording

- **Verified code baseline (read-only):** `sanfiqo/Fabricwala` `main` at commit `35d0f0e318617e1f8cae2bed82275bbcb7902f24` (retrieved 20 Nov 2026). Source inspection is **not** proof of running behavior; local/unpushed code may differ.
- **Two protected commercial journeys:** retain existing catalog → cart → checkout → paid order → shipment → payout; offer RFQ as an additional sourcing journey only when the `Rfq` feature is enabled. RFQ award gives contacts, **not** a paid order or platform commission.
- **Identity:** mobile OTP / HttpOnly cookie / CSRF, roles `Retailer`, `Manufacturer`, `Admin`. Do not invent password/reset UI, unrestricted role switching or dual role memberships.
- **Unit accuracy:** never assume all goods use meters; support the product's actual API unit and preserve variant/slab/MOQ pricing logic.
- **Visual rules:** Ivory Atelier V2 color intent remains unchanged; treat published token hex values as *candidate* pending visual/contrast review. Preserve Hindi typography fallback and fabric-color accuracy.
- **Scope labels:** `IMPLEMENTED (source)`, `WIRED FRONTEND`, `BACKEND ONLY`, `PROPOSED`, `BLOCKED DECISION`, and `UNVERIFIED RUNTIME` must not be conflated.
- See `19-SOURCE-OF-TRUTH-AND-DECISIONS.md`, `20-SCREEN-TO-CODE-IMPLEMENTATION-MATRIX.md`, `21-API-CONTRACT-RFQ-AND-COMMERCE.md`, `22-PHASED-TASKS-AND-ACCEPTANCE.md`, `23-V2-1-DOCUMENTATION-CONFLICT-AUDIT.md` before using any legacy instructions below. **No implementation authorization is granted by this package.**


## V2.2 — 20 Nov 2026 (Mobile Feel + audited CSS)
- Contrast audit of candidate tokens: 6 defects fixed (see 08 V2.2 table). Added runnable `css/check-contrast.py`.
- Added real CSS: `css/fabricwala-ivory-atelier.css` (tokens + base + components) and `css/fabricwala-mobile-feel.css` (glass, pillow, sheets, tab bar, spring motion) with rendered `css/preview.png` / `preview.html`.
- New file `24-MOBILE-FEEL-SPEC.md`; 01, 03, 04, 08 amended with V2.2 precedence sections.
- Not done: no real-device test, no Hindi/₹ glyph test, no repository change, glass-layer contrast not yet measured over photos.

## V2.3 — 20 Nov 2026
- Lagoon palette chosen; `css/fabricwala-lagoon.css`, `css/check-lagoon.py`, `css/fabricwala-pastel-kit.html` added. Colour sections of 01/04/08/24 overridden by V2.3 notes. Ivory tokens retained only as history.

## V3.0 — 20 Nov 2026
- FINAL direction: black & white Alibaba-style sourcing UI (owner-approved). New `25-BW-SOURCING-UI-SPEC.md`, `css/fabricwala-bw.css`, `css/check-bw.py`, `css/reference-screens.*`. Earlier palettes/CSS archived in `css/archive/`.
- Not done: real-device test, Hindi/₹ glyph test, photo-based review, repository audit.
