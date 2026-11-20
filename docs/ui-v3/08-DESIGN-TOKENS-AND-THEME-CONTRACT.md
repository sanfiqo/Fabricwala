# 08 — FabricWala Design Tokens & Admin Theme Contract

## Status / authority
- Design specification baseline v2.0, prepared from the preceding design discussion. **Candidate implementation tokens, not previously approved measured values.**
- Brand direction approved in conversation: Ivory Atelier editorial luxury, mostly porcelain/ivory, minimal espresso, selective soft 3D, textile colors untouched. The admin controls **global colors**, not geometry or layout.
- If tokens conflict with older prompt prose, this file is the detailed source for candidate numeric design values; ask for visual approval before implementation.

## Semantic color system (light theme only)
| Token | Candidate value | Purpose |
|---|---|---|
| `color.canvas` | `#F5F0E9` | Ivory Atelier base canvas |
| `color.surface` | `#FFFCF8` | Porcelain cards, forms, dashboards |
| `color.surface-subtle` | `#E9DED1` | Inset surfaces, lightweight panels |
| `color.surface-raised` | `#FFFFFF` | Elevated panels, icon buttons, sheet rows (V2.2: now differs from surface) |
| `color.text-primary` | `#2E211B` | Primary titles and body |
| `color.text-secondary` | `#66594E` | Secondary text |
| `color.text-muted` | `#6E6054` | Muted labels; 4.57:1 even on Warm Sand (V2.2: was identical to secondary) |
| `color.placeholder` | `#7A6A5C` | Placeholder text on ivory/porcelain only (4.58 / 5.08:1) |
| `color.border` | `#E6DDD3` | Decorative dividers/card outlines ONLY (1.3:1, not for controls) |
| `color.border-control` | `#8F7E6E` | Inputs, outlined buttons, chips: >=3:1 (3.4 ivory / 3.8 porcelain) |
| `color.action-primary` | `#2E211B` | Espresso primary CTA (dark, not a brown-filled page) |
| `color.action-primary-text` | `#FFFFFF` | CTA text |
| `color.accent` | `#8B7362` | Mocha: decorative fills, large icons, bar fills. NOT body text (3.9:1 on ivory) |
| `color.accent-text` | `#6F5A4A` | Links, small accent text, focus (5.7–6.3:1; 4.9 on sand) |
| `color.accent-soft` | `#E9DED1` | Accent-tinted surfaces |
| `color.focus` | `#6F5A4A` | Focus ring + 4px white halo (V2.2: was mocha, too weak on ivory) |
| `color.overlay` | `rgba(46,33,27,.44)` | Backdrop for modals (sheets use .38 + 6px blur, see 24) |
| `color.success` | `#286648` | Semantic success, never theme-dependent |
| `color.warning` | `#8A5A14` | Semantic caution |
| `color.danger` | `#B42318` | Error/irreversible |
| `color.info` | `#245C80` | Information |

**Critical:** Exact hex values are design proposals derived from visual direction, not photo-extracted source hex values. Validate contrast across all states; change token values where checks fail.

## Theme boundaries
The **global admin theme may replace** the values of `action-primary`, `accent`, `accent-soft`, `focus`, and color-related ornamental tint/shadow values, plus their calculated accessible on-colors. Global publish applies consistently to public web, buyer workspace, supplier workspace and mobile.

The admin theme **must never modify** typography, font sizes, radii, layout, spacing, breakpoint logic, elevation geometry, animation type/timing, image hue/saturation, business statuses, logo identity, content ordering or role permissions. Semantic success/warning/danger colors remain recognizable and accessible independently of theme.

Theme version model (proposed): `theme_id`, `name`, `status` (draft/published), `semantic_color_overrides`, `contrast_validation`, `created_at`, `published_at`, `version`. Publish is admin-only; preview uses draft tokens; rollback selects a previously published version. Do not assume a theme backend is already implemented.

## Typography scale (candidate)
- Typography pair selected for visual design: Cormorant Garamond for editorial hero, collection, campaign and showroom headlines; Manrope for navigation, body, product metadata, all forms, RFQs, quotes, tables and dashboards. Limit to these two font families with system fallbacks and test Indic language glyph coverage.
- Cormorant Garamond is restricted to expressive public/editorial headings, never forms, prices, navigation, metrics or tables; use Manrope for dense workspaces.
- Mobile sizes: Display 32–40/1.08; H1 28–32/1.16; H2 22–26/1.2; H3 18–20/1.3; body 16/1.5; small 14/1.45; label minimum 12/1.4 (not for critical actions).
- Desktop sizes: Display 52–72/1.05; H1 36–48/1.12; H2 28–36/1.2; H3 20–24/1.3; body 16/1.55.
- Numeric product values and dashboard metrics use tabular numerals; preserve units and currency conventions.

## Spacing / radius / elevation
- Base spacing: 4px. Steps: 4, 8, 12, 16, 20, 24, 32, 40, 48, 64, 80, 96.
- Card padding: 16–24px mobile, 20–32px desktop by density; forms use consistent label/input/help spacing.
- Radii: small 8; control 12; card 20; featured/promo 24–32; pill 9999. Avoid arbitrary one-off radii.
- Shadow low: `0 2px 12px rgba(46,33,27,.06)`; raised: `0 10px 30px rgba(46,33,27,.08)`; floating: `0 16px 44px rgba(46,33,27,.12)`; focus uses visible outline not shadow alone.
- Neumorphism may be used on *selected* floating search, visual/KPI highlight, toggle; avoid in dense data surfaces, table rows, RFQ forms, chat.
- Touch targets >=44x44 CSS pixels (48 preferable on Android). Inputs 44–52px minimum height.

## Responsive breakpoints (candidate)
- Compact mobile: < 480px; mobile large 480–767; tablet 768–1023; desktop 1024–1439; wide 1440+.
- Use fluid content width, not hardcoded screenshot widths. Public desktop max content ~1280px; business dashboard may use wider adaptive layout.
- Respect safe areas, text scaling, pointer type, landscape, keyboard open and reduced-motion.

## Image integrity and photography
- Fabric product photographs are accurately colored and not exposed to admin theme filters.
- Product listing/detail primary image uses 4:5 or original aspect preserved in gallery; use object-fit strategies that do not crop weave details unexpectedly.
- Cover/hero can use editorial 16:9; identity/logo must not be distorted. Skeleton retains image ratios to avoid layout shift.
- Supplier imagery is user-provided; stock/AI placeholders must never be shown as real supplier products.

## Contrast validation
- WCAG AA baseline: >=4.5:1 normal text and >=3:1 for large text and relevant UI non-text contrast where applicable.
- Every theme preset must pass automated checks *and* human visual QA; never publish an invalid palette.
- Maintain visible keyboard focus and accessible states for hovered, pressed, loading, disabled and errors.


## V2 reference authority — 20 November 2026
- The user-provided **AIDA ATELIER** three-screen reference is the primary *visual-feel reference*: porcelain light surfaces, warm ivory background, sand/taupe atmospheric layering, tasteful photographic whitespace, restrained editorial serif headings, and deep espresso capsule CTAs. **Do not copy AIDA branding, merchandise, photography or layout verbatim.**
- Color values above are **proposed visual matches, not colorimeter-sampled values**. Palette is specified for design work; not an assertion of pixel-perfect color extraction or production deployment.
- Primary palette: Ivory `#F5F0E9`, Porcelain `#FFFCF8`, Warm Sand `#E9DED1`, Soft Taupe `#C9B8A5`, Mocha `#8B7362`, Espresso `#2E211B`. Main `color.accent` is Mocha.
- Approximate color area: predominantly light ivory/porcelain (80–85%), dark espresso reserved for readable headings and primary actions, warm beige/taupe for subtle context. This is a qualitative guideline, not a CSS constraint.
- **No legacy purple, neon, rainbow gradients, cocoa-filled dashboards or dark homepage as a default.** The one globe scene should remain pearl/ivory and textile-first.
- Primary body text on porcelain and ivory should be contrast-checked against its actual background, including muted labels, placeholder text and focus indicators. Decorative light taupe is not automatically a safe text color.
- The admin may change global *semantic color variables* (including background and card surface tints if a preset explicitly defines them), only as an audited theme preset; layout, fonts, sizes, shadows' geometry, motion, content and fabric photograph colors remain unchanged. Every preset must pass contrast and visual QA.


## V2.1 — Real CSS mapping and implementation constraints

Current `client/src/index.css` defines `--color-background:#faf9f6`, `--color-surface:#ffffff`, `--color-soft:#f2f1ed`, `--color-ink:#1c1b19`, `--color-mute:#5c5a55`, `--color-line:#e9e7e2`, semantic status vars, legacy `navy-*` aliases, `--radius-card:22px` and `--font-sans` = Inter + Noto Sans Devanagari. **V2 palette is a design target, not the current app.** Before modifications, snapshot computed styles. Proposed mapping: background → Ivory Canvas `#F5F0E9`; surface → Porcelain `#FFFCF8`; soft → Warm Sand `#E9DED1` (verify dense workspaces); ink/primary → Espresso `#2E211B`; border → warm-neutral `#E6DDD3`; mute → approved high-contrast neutral. Do NOT blindly map every `navy-*` alias; inventory legacy consumers first. Keep semantic success/error/warning tokens accessible and separate fabric swatch data (`client/src/lib/colours.ts`) from theme.

Font plan: Manrope for UI with Noto Sans Devanagari fallback and optional Cormorant Garamond **only** for selected public editorial headings. Self-host, check Hindi/₹ coverage, swap behavior and bundle impact. Global admin theme preview/draft/publish/rollback is **not implemented**: API persistence, authorization, safe published defaults and rollback are separately scoped; no CSS-only claim.


## V2.2 — Audited token corrections and Mobile Feel tokens (20 Nov 2026) — TAKES PRECEDENCE over the table and prose above

**Measured, not assumed.** All pairs below were computed with `css/check-contrast.py` (WCAG 2.x relative luminance). Re-run after any token change.

| Finding in V2.1 candidate tokens | Measured | V2.2 fix |
|---|---|---|
| Mocha `#8B7362` as text | 4.34 porcelain / 3.92 ivory / 3.35 sand (fail 4.5) | `accent-text #6F5A4A` (6.34 / 5.72 / 4.89) |
| White on mocha button | 4.44 (fail) | Primary action stays espresso; never fill buttons with mocha |
| "Fine taupe border" on controls (doc 04) | taupe 1.89:1 (fail 3:1 for UI components) | `border-control #8F7E6E` |
| `text-muted` identical to `text-secondary` | n/a | `#6E6054` plus separate `placeholder` |
| Focus ring mocha | 3.92 on ivory (barely passes, weak) | `#6F5A4A` + white halo |
| Shadow tint `45,33,28` vs text `46,33,27` | inconsistent | single espresso RGB `46 33 27` |

**Passing pairs (verified):** espresso on ivory 13.7, porcelain 15.2, sand 11.7; secondary on sand 5.1; success/warning/danger/info solid on porcelain 5.8–7.0 and on their tinted badge backgrounds 5.0–6.1; white on espresso 15.6.

### Mobile Feel tokens (new; see 24 for usage)
| Token | Value | Purpose |
|---|---|---|
| `glass.bg` / `glass.bg-strong` | `rgb(255 252 248 / .72)` / `.86` | Frosted surfaces. Text must sit on >= .72 opacity |
| `glass.blur` | `22px` (+ `saturate(1.4)`), sheets `28px` | Backdrop blur |
| `glass.border` | `rgb(255 255 255 / .75)` | Light edge highlight |
| `radius.xl` / `radius.2xl` | `28px` / `36px` | Glass cards / bottom sheets |
| `shadow.pillow` | `8px 8px 20px rgba(46,33,27,.09), -8px -8px 20px rgba(255,255,255,.95)` | Selective soft-3D tiles |
| `shadow.inset` | inset equivalent of pillow | Pressed/track surfaces (bar rails, segmented control) |
| `motion.spring` | `cubic-bezier(.34,1.4,.64,1)` | Sheet open, press release |
| Ambient blobs | champagne `#EADCC8` (.9), cool pearl `#DDE3E6` (.65), blur 60px | Backdrop depth on hero/home/RFQ screens |

**Theme boundary unchanged:** admin may replace only action/accent/focus/soft-accent tints. Glass opacity, blur, radii, shadow geometry, springs and ambient blob geometry are NOT themeable.


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
