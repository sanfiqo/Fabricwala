# 25 — FINAL UI SPEC: Black & White Sourcing UI (V3.0, 20 Nov 2026)

**Status:** OWNER-APPROVED visual direction ("thik hai", 20 Nov 2026). **Supersedes every earlier colour/style decision** (Ivory Atelier V2, Lagoon V2.3, Weave/soft-glass concepts, pastel kit). Those files remain only as history in `css/archive/`. Design + CSS spec only; not an implemented app, not tested on real devices, no permission to change the repository.

## 1. Direction in one line
An **Alibaba-style, dense B2B sourcing UI in strict black and white; all colour comes from the fabric photographs.** Fabric is the only hero.

Reference studied: Alibaba mobile home (owner screenshot) and alibaba.com homepage: search with camera/image search, tabs (Products / Manufacturers / Worldwide), shortcut cards (Source by category, Request for Quotation, Top ranking), horizontal product rails showing price range + MOQ, Verified-supplier badges, sign-in bar, 5-item bottom navigation. **Borrow structure and information density, not branding or imagery.** Unlike Alibaba's home cards, FabricWala shows **tiered (slab) pricing** on the product page because fabric is priced by volume.

## 2. Palette (monochrome) — `css/fabricwala-bw.css`
| Token | Value | Use |
|---|---|---|
| `--bg` | `#FFFFFF` | Screens, cards |
| `--s1` / `--s2` | `#F5F5F5` / `#EBEBEB` | Section bands, search field, inset areas |
| `--tx` / `--tx2` | `#0A0A0A` / `#595959` | Text / secondary text (19.8 / 7.0:1 on white) |
| `--ink` / `--on-ink` | `#0A0A0A` / `#FFFFFF` | Primary buttons, selected chips/tier, banner, active nav |
| `--ln` | `#E2E2E2` | Decorative dividers and card outlines ONLY (1.3:1) |
| `--ln-ctl` | `#8C8C8C` | Borders of interactive controls (chips, search) — 3.36:1 |
| Status `--ok --warn --err --info` | `#1E6B45 #8A5A14 #B42318 #1F4E79` | **Only colour exception.** Always with text/icon, never colour alone |
Rules: no gradients, no tints, no accent colour, no glass/blur, no neumorphism, no dark mode in v1. Hierarchy comes from weight, size, fill (black vs white) and spacing.

## 3. Typography
Inter/Manrope + Noto Sans Devanagari fallback (self-host, `swap`; test Hindi and ₹). Sizes: body 14, small 12–13, card price 15–16/800, section title 18/800, product price 26/900 tabular numerals. Prices always show currency, unit (per metre or the API unit) and MOQ — **never assume metres**; use the product's real unit.

## 4. Screen contracts (map to existing app routes; do not change business logic)
1. **Home:** header (logo + search with camera icon + black search button) -> tabs (Fabrics / Mills / Ready stock / Worldwide) -> category chips -> 3 shortcut cards (Source by category, Request for Quotation, Top ranking mills) -> rails ("Trending this week", "Ready stock") with price range + "Min. order" -> optional sign-in bar (guests only) -> bottom nav (Home, Categories, Messenger, Cart, My account).
2. **Results / category:** back + search, chips (Filter, Verified, Ready stock, MOQ, Price), result count, 2-column grid cards (1:1 fabric image, 2-line title, price range, MOQ, Verified badge, city + years), floating black "Can't find it? Request quote".
3. **Product detail:** large fabric gallery with counter, price range, title, **tier table** (selected tier = black fill), key facts (MOQ, lead time, sample), supplier card with Verified badge, sticky bar: Chat | Add to cart | **Request quote** (black, primary).
4. **RFQ create / quotes / award:** same system: flat white forms with `--ln-ctl` borders, black primary button, quote rows as cards with Verified badge + price + days. Awarding gives contacts, not a paid order (V2.1 rule unchanged).
5. **Cart / checkout / orders / payouts:** existing flows unchanged; restyle only.
6. **Supplier workspace:** same components; Verified badge only with real verification data.
7. **Auth:** mobile OTP only; no password UI.

## 5. Components (all in `css/fabricwala-bw.css`)
Header + search (44px), tabs (3px black underline), chips (selected = black fill), shortcut cards, product rail card (148px), grid card, tier table, key/value list, supplier card, Verified badge (outlined pill with check), black sign-in bar, 5-item bottom nav (active = filled icon + bold), sticky action bar, floating RFQ pill, outlined and black buttons (48px, pill), fabric box `.fab`. States required for each: default, pressed, focus-visible (2px black outline), disabled, loading, error. Touch targets >= 44px.

## 6. Fabric imagery rules (most important)
- Product/listing images **1:1** (cards) and original aspect in gallery; real supplier photos only — never stock/AI shown as real products.
- **Never filter, tint, overlay or caption-over** fabric photos; text sits below the image.
- Provide 3+ photos per listing: full drape, close-up weave, colour-accurate flat shot.
- `.f-*` classes in the CSS are placeholders for design review and empty states only.

## 7. Protected business rules (unchanged from V2.1)
Catalogue -> cart -> checkout -> paid order -> shipment -> payout stays intact; RFQ is an additional journey when the `Rfq` feature is on; RFQ award = contacts, no commission; OTP + HttpOnly cookie + CSRF; roles Retailer / Manufacturer / Admin; preserve variant/slab/MOQ pricing and the product's actual unit. No fake claims ("Verified", "Best price") without backing data.

## 8. Accessibility / QA checklist
- [ ] `python3 css/check-bw.py` shows 0 failures (verified 20 Nov 2026).
- [ ] Control borders use `--ln-ctl`, never `--ln`.
- [ ] Keyboard focus visible on every control; 44px targets; 200% text scale; Hindi + ₹ render.
- [ ] Status never colour-only; reduced-motion respected.
- [ ] Real-device test (low-end Android, iPhone safe areas), real-photo review of 10+ fabrics.
- [ ] Regression: cart, checkout, OTP, RFQ unchanged. No route/API change.
**Not done:** real-device testing, Hindi glyph test, performance measurement, repository audit.

## 9. Instruction for an AI developer (Codex / Claude Code)
> Read `25-BW-SOURCING-UI-SPEC.md`, `19`–`23`, and open `css/reference-screens.html` + `css/fabricwala-bw.css`. **Audit only first**: list files to change, existing CSS tokens/consumers (do not blindly map `navy-*`), risks and tests; snapshot current computed styles. Then implement in order: (1) token swap, (2) shared components, (3) pilot screen: Product detail with tier table behind a feature flag, (4) Home and Results. Do not change cart, checkout, OTP, RFQ logic, routes or APIs. Use real fabric photos where available; no filters on images.

---
**V3.1:** Home screen features are specified in `26-HOME-SCREEN-FEATURES-SPEC.md` (reference `css/home-screen.html`).
