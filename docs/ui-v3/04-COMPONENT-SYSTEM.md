# FABRICWALA — COMPONENT SYSTEM PROMPT

## GOAL
Define the complete reusable premium component system for FabricWala.

## SYSTEM PRINCIPLE
All components must feel:
- consistent
- premium
- soft
- modern
- refined
- scalable

---

## COMPONENT CATEGORIES

### NAVIGATION
Design:
- floating header
- bottom nav
- top tabs
- segmented controls
- breadcrumb
- sidebar
- mega menu
- sticky subnav

### BUTTONS
Design:
- primary
- secondary
- tertiary
- icon buttons
- floating action buttons (if needed)
- sticky action buttons

Style:
- premium
- tactile
- soft 3D influence
- not cartoonish

### INPUTS
Design:
- search field
- text input
- textarea
- selects
- chips
- dropdowns
- autocomplete
- upload field
- filter controls

### CARDS
Design:
- product card
- category card
- supplier card
- RFQ card
- quotation card
- dashboard KPI card
- message preview card
- collection card
- banner card

### OVERLAYS
Design:
- modal
- drawer
- sheet
- confirmation dialog
- popover
- tooltip

### FEEDBACK
Design:
- toast
- success state
- error state
- empty state
- skeleton loading
- progress stepper
- badge/state tags

### DATA
Design:
- tables
- comparison cards
- status rows
- analytics widgets
- sorting/filtering UI

---

## PRODUCT CARD RULE
Product cards must be:
- image-first
- clean
- premium
- lightly informative

Show limited information only.
Do not overload with too many badges or controls.

Primary action should usually happen on product detail page.

---

## SUPPLIER CARD RULE
Supplier cards must feel:
- trustworthy
- professional
- clear

Include:
- company name
- location
- key specialization
- verification/capability indicators where valid
- profile action

---

## RFQ / QUOTATION CARD RULE
These should be:
- structured
- easy to compare
- status-oriented
- not overly decorative

---

## SEARCH AND FILTER RULE
Filters should feel elegant and powerful, not clunky.

Use:
- chips
- sheets
- side panels
- sliders
- toggles
- multi-select patterns

---

## THEMING RULE
Every component must support:
- admin theme accent changes
- stable structure
- accessible states
- hover/focus/active/disabled/loading states

---

## MICRO-DETAILS
Define:
- corner radius system
- spacing scale
- typography scale
- shadow scale
- border styles
- icon style
- state colors
- motion durations

---

## OUTPUT INSTRUCTION
Generate:
- component inventory
- component anatomy
- state definitions
- usage rules
- do/don’t rules

All components must preserve the FabricWala premium identity.


## COMPONENT STATE COMPLETENESS
See 08 for numeric candidate tokens, 10 for auth, forms, modals, skeletons, upload and error states, and 15 for component QA evidence requirements.


---

## V2 component styling contract
- Primary buttons: espresso `#2E211B` with white text, capsule or soft-round by consistent variant, no gradients.
- Secondary: porcelain `#FFFCF8` + accessible espresso text + fine taupe border. Tertiary: warm sand `#E9DED1` only where contrast passes.
- Cards: image-first, porcelain, 16–20px radius standard product; airy layouts, subtle warm low-elevation shadow. RFQ and quote cards use compact rows, not editorial large type.
- Inputs: porcelain, visible sand border and explicit focus ring, not inset low-contrast neumorphism.
- Status badges: semantic colors and text, not merely matching beige.
- Component tokens source: file 08; hover/focus/pressed/disabled/invalid/loading/empty design states source: 10 and 15.
- Do not apply dark cocoa-filled cards or purple accents from rejected image experiments.


## V2.1 Implementation Reality Contract — takes precedence over older exploratory wording

- **Verified code baseline (read-only):** `sanfiqo/Fabricwala` `main` at commit `35d0f0e318617e1f8cae2bed82275bbcb7902f24` (retrieved 20 Nov 2026). Source inspection is **not** proof of running behavior; local/unpushed code may differ.
- **Two protected commercial journeys:** retain existing catalog → cart → checkout → paid order → shipment → payout; offer RFQ as an additional sourcing journey only when the `Rfq` feature is enabled. RFQ award gives contacts, **not** a paid order or platform commission.
- **Identity:** mobile OTP / HttpOnly cookie / CSRF, roles `Retailer`, `Manufacturer`, `Admin`. Do not invent password/reset UI, unrestricted role switching or dual role memberships.
- **Unit accuracy:** never assume all goods use meters; support the product's actual API unit and preserve variant/slab/MOQ pricing logic.
- **Visual rules:** Ivory Atelier V2 color intent remains unchanged; treat published token hex values as *candidate* pending visual/contrast review. Preserve Hindi typography fallback and fabric-color accuracy.
- **Scope labels:** `IMPLEMENTED (source)`, `WIRED FRONTEND`, `BACKEND ONLY`, `PROPOSED`, `BLOCKED DECISION`, and `UNVERIFIED RUNTIME` must not be conflated.
- See `19-SOURCE-OF-TRUTH-AND-DECISIONS.md`, `20-SCREEN-TO-CODE-IMPLEMENTATION-MATRIX.md`, `21-API-CONTRACT-RFQ-AND-COMMERCE.md`, `22-PHASED-TASKS-AND-ACCEPTANCE.md`, `23-V2-1-DOCUMENTATION-CONFLICT-AUDIT.md` before using any legacy instructions below. **No implementation authorization is granted by this package.**


## V2.2 Component contract — Mobile Feel (TAKES PRECEDENCE over "V2 component styling contract" above)

Overrides of V2 lines:
- **Secondary button:** porcelain fill + espresso text + `border-control #8F7E6E` (NOT taupe, which fails 3:1).
- **Inputs:** porcelain, `border-control`, 12px radius, 48px (52px on coarse pointers) high, 16px minimum font size to prevent iOS zoom, 2px focus outline + white halo. Soft inset shadows are allowed ONLY on tracks (segmented control, bar rails), never on text inputs.
- **Cards:** keep image-first 4:5 product cards (20px). Mobile hero/summary/KPI cards may be **glass** (28px) or **pillow**. Dense data (tables, RFQ forms, chat, quote comparison rows) stays flat porcelain.
- **"No gradients" relaxed:** allowed ONLY for ambient backdrop blobs, the bottom CTA scrim, and skeleton shimmer. Never on buttons or text.

New components (spec in 24, CSS in `css/fabricwala-mobile-feel.css`):
1. Ambient screen background (`app-bg`)  2. Glass card / glass strip  3. Pillow tile and inset track  4. Round icon button 44px (back, save, close)  5. Floating glass tab bar (pill, inset from edges)  6. Sticky dark capsule CTA with scrim  7. Glass bottom sheet with grabber + blurred dimmed backdrop  8. Row item (56px thumb, title, meta, price/chevron)  9. Segmented control (inset track, raised selected pill)  10. Snap rail (collections / swatches)  11. Vertical bar rails (price trend, quotes)  12. Large screen title (serif).

States required for every one: default, hover (pointer only), pressed (scale .97 spring), focus-visible, disabled, loading (`aria-busy`), error. Status is never colour-only.


## V2.3 DECISION — Palette is now LAGOON (teal + peach pastel gradients), 20 Nov 2026 — OVERRIDES every Ivory/espresso colour in this file
Owner chose **Lagoon** after reviewing a pastel-gradient reference (frosted teal/peach app UI). Ivory Atelier colours (ivory, porcelain, sand, taupe, mocha, espresso) are **retired**; structure, glass/pillow materials, radii, spacing, motion and all protected business rules are unchanged. Working code: `css/fabricwala-lagoon.css` (load after the other two CSS files). Live kit: `css/fabricwala-pastel-kit.html` (Lagoon is default; Aurora/Mint/Rose/Sage remain as alternates).

| Token | Lagoon value |
|---|---|
| Pastel stops | aqua `#D4F1EE`, mint `#E6F4EF`, peach `#FFE4D4` (page/background/chip gradients) |
| Button/active gradient | `#0A7C84` -> `#087A66` (white text 5.0–5.3:1); hover `#08666D`, pressed `#064F55` |
| Canvas / surface / subtle | `#EAF6F3` / `#FFFFFF` / `#D6EFEC` |
| Text / secondary / muted / placeholder | `#0F2427` / `#4A5F62` / `#4F6568` / `#5B7073` |
| Accent text & focus | `#0A7479` |
| Control border (>=3:1) / decorative border | `#6F8D8F` / `#D3E4E2` |
| Status colours | unchanged (success `#286648`, warning `#8A5A14`, danger `#B42318`, info `#245C80`) |

Rules: pastel gradients for backgrounds, glass, chips and tracks; **gradient only on buttons, active tab and bar fills, using the deep stops**; never white text on pastel or on the reference's light teal `#3FC1B5` (2.2:1, fails). Text is always dark ink. Fabric photos stay unfiltered; clinical 3D props from the reference are not used — use real fabric/swatch/yarn photography. Measured with `css/check-lagoon.py` (0 failures); glass-over-photo contrast and real-device tests still pending.


## V3.0 SUPERSEDES THIS FILE'S STYLE — see `25-BW-SOURCING-UI-SPEC.md` (20 Nov 2026)
Owner approved the **black & white Alibaba-style sourcing UI**. All Ivory/espresso, Lagoon, pastel, glass, neumorphism, lime/Weave and accent-colour guidance in this file is **retired** (history only; old CSS in `css/archive/`). Current tokens: `css/fabricwala-bw.css`. Colour appears only in fabric photographs (and semantic status colours).
