# 24 — Mobile Feel Spec (V2.2): Soft Glass Atelier

**Status:** design specification + working CSS (`css/`). Rendered once in headless Chrome (`css/preview.png`); **not** tested on real devices, not an implemented app screen. Palette and protected business rules are unchanged from V2.1.

## 1. Reference analysis (what to borrow, what not)
| Ref | Borrow (structure/material) | Do NOT borrow |
|---|---|---|
| A. Frosted-glass portfolio | Translucent cards with light edge, stat strip with hairline dividers, rounded 28px cards, pill CTAs, tiny label chips, generous padding | Icy blue gradient, chrome 3D type, stock model photo, skill percentage bars |
| B. Soft-tactile calm app | Pillow tiles, inset tracks (vertical bar rails), round icon buttons, bottom icon dock, serif title over small caps sub-line | Lavender palette, mood/wellness content |
| C. Blurred-backdrop outfit picker | Blurred ambient backdrop, round back/heart buttons, centred serif screen title, glass bottom sheet with row items, full-width dark pill CTA, dimmed+blurred screen behind sheet | Blue hoodie product, Turkish copy, canvas grid |

## 2. Material system (use exactly these four)
1. **Ambient** — screen background: ivory + 2 blurred light blobs (champagne, pearl). Home, Explore, RFQ summary, onboarding. Not on forms/tables.
2. **Glass** — navigation, summary strips, image captions, sheets. Opacity >= .72, blur 22–28px, white edge, soft shadow.
3. **Pillow / inset** — 1–2 hero tiles per screen (quotes received, price trend), tracks (segmented, bars), toggles.
4. **Flat porcelain** — forms, RFQ composer, quote comparison, tables, chat, order lists. Dense data never gets glass or pillow.

## 3. Screen anatomy
```
[status bar]
[round icon] [  serif title  ] [round icon]    <- 44px buttons, 20px serif title
[large serif title (Home/Explore only)]
[glass search pill]  [segmented: Catalogue | Post RFQ | Suppliers]
[snap rail of image cards, glass caption on lower edge]
[glass stats strip: 3 numbers, hairline dividers]
...scroll content...
[floating glass tab bar]  OR  [sticky dark CTA 56px]   <- never both on the same screen
```
Tab bar hides on detail/checkout/RFQ-compose screens where a sticky CTA exists.

## 4. Per-flow application (existing routes unchanged)
- **Home / Explore:** ambient bg, large serif title, glass search, segmented entry points, collection rails, stats strip.
- **Product detail:** full-bleed 4:5 gallery (no glass over weave), round back/save buttons, price + unit as tabular numerals, "Choose quantity/slab" opens **glass sheet** with row items (sample / roll / bulk RFQ), CTA "Continue". Unit and slab/MOQ come from API, never assumed metres.
- **Cart / checkout:** flat porcelain rows, sticky dark CTA; keep existing payment steps exactly.
- **RFQ create:** flat porcelain stepper form; sticky CTA. **RFQ status:** pillow KPI tile + inset bar rail, glass supplier quote rows with semantic badges. Award shows contacts, not a paid order.
- **Supplier workspace:** same materials; Products/Enquiries are flat lists with row items; verified badge only with real data.
- **Auth (OTP):** ambient bg, one glass card, OTP boxes 48px, no password UI.

## 5. Typography on mobile
Serif (Cormorant Garamond): screen titles and large collection titles only. Manrope: everything else including prices, labels, buttons. Hindi: Noto Sans Devanagari, line-height 1.7, no uppercase/letter-spacing. Tabular numerals for prices and counts.

## 6. Motion
Press: scale .97, 120ms, spring release. Sheet: slide up 380ms spring over fading blur backdrop. Entrance: translate 14px + fade, 60ms stagger, once per screen. Under `prefers-reduced-motion`: no animation. Shared-element product transition stays as in doc 05.

## 7. Accessibility and performance rules
- Text on glass: espresso/secondary only; glass >= .72 opacity; verify contrast over the worst-case backdrop (bright photo) — **pending measurement**.
- Touch targets >= 44px (tab items 52px, CTA 56px). Focus ring visible on glass (2px `#6F5A4A` + white halo).
- Max 3 simultaneous `backdrop-filter` layers; sheet backdrop blur 6px only. Solid fallback for unsupported / `prefers-reduced-transparency` / low-end Android (feature flag).
- Status never colour-only. No fake "Verified/Best price" claims.
- Skeletons keep 4:5 ratio; glass skeletons not needed.

## 8. Acceptance checklist (add to 15/22)
- [ ] Each screen uses <= 1 pillow hero tile and <= 1 primary dark CTA.
- [ ] No glass/pillow on forms, tables, chat, quote comparison.
- [ ] Tab bar OR sticky CTA, not both.
- [ ] `check-contrast.py` passes; glass-over-photo contrast measured on 5 real fabric images.
- [ ] Real-device test: low-end Android (blur cost), iPhone safe areas, 200% text scale, Hindi.
- [ ] Reduced-motion and reduced-transparency verified.
- [ ] Fabric photos unfiltered; hex of rendered swatch within tolerance of source.
- [ ] Existing cart/checkout/OTP/RFQ behaviour regression-tested; no route or API change.

## 9. Implementation notes
Existing app uses Tailwind-style `index.css` (`--color-*`, `--radius-card:22px`, Inter). Map old vars to V2.2 tokens in one pass (snapshot computed styles first, do not blindly map `navy-*`). Load `fabricwala-ivory-atelier.css` then `fabricwala-mobile-feel.css`, or port the variables into the Tailwind theme. Replace preview's text glyph icons with an icon set (Lucide/Phosphor). Self-host Manrope, Cormorant Garamond, Noto Sans Devanagari with `font-display: swap`.

**Suggested instruction to an AI developer:** "Read 24 and 08 (V2.2 sections) and `css/`. Audit only first: list files to change, risks, tests. Then implement the token swap and one pilot screen (Product detail + quantity sheet) behind a feature flag. Do not alter cart, checkout, OTP or RFQ logic."


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
