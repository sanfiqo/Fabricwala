# FABRICWALA — BRAND VISUAL IDENTITY PROMPT

## GOAL
Define the premium visual identity of FabricWala.

## BRAND FEEL
FabricWala should feel:
- Premium
- Refined
- Textile-first
- Trustworthy
- Elegant
- Editorial
- Modern
- Business-capable

It should combine:
- luxury editorial warmth
- clean modern product design
- selective soft 3D depth

---

## APPROVED VISUAL STYLE
Primary style:
**Premium White Editorial Luxury**

Supporting style:
**Soft 3D / Selective Neumorphism**

Special section style:
**Atmospheric warm ivory/champagne luxury sections**

---

## CORE COLOR DIRECTION
Dominant surfaces:
- White
- Ivory
- Pearl white
- Soft sand

Primary dark accents:
- Espresso (primary CTA/text)
- Mocha/taupe (subtle accents)
- Charcoal

Support highlights:
- Muted champagne
- Warm taupe
- Soft silver

Avoid:
- overuse of bright orange
- overuse of blue
- neon colors
- heavy rainbow gradients

---

## COLOR USAGE RULES
Approximate balance:
- 80–85% white/ivory/pale surfaces
- 10–15% deep espresso typography and controls
- 5% or less accent/highlight tones

Atmospheric gradients are allowed only in:
- homepage hero
- featured collections
- premium supplier showcase
- onboarding / intro / storytelling sections

Do not use atmospheric gradients in:
- product listings
- dense forms
- RFQ workflows
- chat
- dashboards
- admin tables

---

## TYPOGRAPHY DIRECTION
Use:
- modern editorial headings
- clean readable body text
- controlled visual hierarchy
- strong numerical styling for data

Recommended style:
- strong sans-serif foundation
- optional elegant serif accent only in selected hero/promotional headings

Tone:
- calm
- confident
- premium
- readable

---

## DEPTH AND SHADOW SYSTEM
Use:
- soft elevation
- delicate shadows
- subtle card float
- fine border definition
- tactile buttons

Do not use:
- heavy shadows
- overly dramatic floating layers
- too much glass effect
- fake shiny UI everywhere

---

## SHAPE LANGUAGE
Use:
- rounded cards
- soft corners
- smooth surfaces
- compact elegant pills
- balanced geometry

Suggested radius:
- cards: medium-soft
- chips: round/pill
- buttons: soft rounded
- inputs: soft rounded

Avoid:
- harsh sharp corners everywhere
- overly bubbly children-style rounded UI
- inconsistent radii

---

## FABRICWALA SIGNATURE LOOK
The UI should visually communicate:
- tactile textile softness
- premium product handling
- refined sourcing workflow
- elegant marketplace trust

Products should feel premium through:
- photography
- spacing
- minimal clutter
- subtle support styling

---

## FINAL VISUAL SUMMARY
Use this final identity statement:

“FabricWala should look like a premium textile marketplace built with white editorial elegance, ivory editorial sophistication, selective soft 3D depth, and clear business usability.”

Always preserve this identity in every design decision.


## DETAIL REFERENCE
See 08 for **candidate** exact color tokens, typography, contrast, depth, responsive and theme rules. Previously discussed warm cocoa/ivory is the design direction; numerical values require visual approval.


---

## Ivory Atelier V2 primary brand expression
Primary reference: user-provided AIDA Atelier image, 20 November 2026. Its muted warm ivory/beige atmosphere, editorial serif + crisp sans-serif pairing, generous whitespace, subtle photographic depth and espresso pill actions are the target feel; avoid copying the fashion brand itself. Primary palette and precise candidate values live in file 08. The earlier warm-cocoa visual mood is **superseded** by quieter Ivory Atelier. Do not use purple, bright orange, dark site-wide backgrounds or oversaturated hero gradients.
- Hero and curated supplier collection: subtle couture-style typography and textile photography.
- Product galleries: original fabric color rendering, clean ivory presentation, 4:5 image-first cards.
- RFQ/admin/dashboard: near-white work surfaces and Manrope-only practical business typography.
- Real suppliers and verified badges require true business data rather than visuals claiming verification.


## V2.1 Implementation Reality Contract — takes precedence over older exploratory wording

- **Verified code baseline (read-only):** `sanfiqo/Fabricwala` `main` at commit `35d0f0e318617e1f8cae2bed82275bbcb7902f24` (retrieved 20 Nov 2026). Source inspection is **not** proof of running behavior; local/unpushed code may differ.
- **Two protected commercial journeys:** retain existing catalog → cart → checkout → paid order → shipment → payout; offer RFQ as an additional sourcing journey only when the `Rfq` feature is enabled. RFQ award gives contacts, **not** a paid order or platform commission.
- **Identity:** mobile OTP / HttpOnly cookie / CSRF, roles `Retailer`, `Manufacturer`, `Admin`. Do not invent password/reset UI, unrestricted role switching or dual role memberships.
- **Unit accuracy:** never assume all goods use meters; support the product's actual API unit and preserve variant/slab/MOQ pricing logic.
- **Visual rules:** Ivory Atelier V2 color intent remains unchanged; treat published token hex values as *candidate* pending visual/contrast review. Preserve Hindi typography fallback and fabric-color accuracy.
- **Scope labels:** `IMPLEMENTED (source)`, `WIRED FRONTEND`, `BACKEND ONLY`, `PROPOSED`, `BLOCKED DECISION`, and `UNVERIFIED RUNTIME` must not be conflated.
- See `19-SOURCE-OF-TRUTH-AND-DECISIONS.md`, `20-SCREEN-TO-CODE-IMPLEMENTATION-MATRIX.md`, `21-API-CONTRACT-RFQ-AND-COMMERCE.md`, `22-PHASED-TASKS-AND-ACCEPTANCE.md`, `23-V2-1-DOCUMENTATION-CONFLICT-AUDIT.md` before using any legacy instructions below. **No implementation authorization is granted by this package.**


## V2.2 Brand expression update — Soft Glass Atelier (20 Nov 2026)
V2 said "avoid too much glass effect" and "no gradients except hero". The owner reviewed the output and wants the **latest modern mobile UI feel** (three references: frosted-glass, soft-tactile, blurred-sheet). V2.2 therefore **allows glass and soft depth on mobile app surfaces** while keeping the Ivory Atelier palette (no blue, purple or neon from the references; borrow their *structure and materials*, not their colours or content). Rules: glass for navigation, summary cards, labels and sheets; flat porcelain for dense data and forms; pillow depth only on 1–2 hero tiles per screen; espresso remains the only strong colour. See 24.


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
