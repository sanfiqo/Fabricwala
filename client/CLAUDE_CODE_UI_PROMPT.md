# Prompt for Claude Code — TextileConnect PWA: modern UI, motion, smart features

Paste everything below the line into Claude Code (frontend worktree, branch `frontend/pwa`).

---

## 0. Scope and rules (read first)

- Read and follow `AGENTS.md` and `docs/UX_RULES.md`. Every screen must pass UX_RULES and its Definition of done.
- Your scope is Phase 6 only: the frontend PWA in `client/`. Never edit `src/`, `tests/`, migrations, or `docs/PHASES.md` ticks for backend tasks.
- Stay in mock mode (`VITE_USE_MOCKS`) unless a real endpoint already exists. Never touch `appsettings*.json`. No secrets in code.
- One agent per folder. Work on the `frontend/pwa` branch. No force push, no `git reset --hard`.
- Do not use flag emoji or country flags anywhere (language picker uses plain text: English, हिंदी).
- Token budget: do not re-read files you already know. Read only what the task needs. Report briefly.

## 1. What we are building

TextileConnect is an all-India B2B textile marketplace (manufacturers sell to retailers). Users are shopkeepers and factory owners, 25–60 years old, on mid and low-end Android phones, often on slow 4G, comfortable with WhatsApp, may prefer Hindi.

Goal of this task: make the existing PWA look and feel like a modern 2026 premium app, while staying fast and easy on a low-end phone. Looks and motion must never cost speed or clarity.

## 2. Design source of truth

The approved design is in `client/design-ref/` (HTML files exported from our design canvas, plus `tokens.json`). Open them in a browser to see the exact look. Files named `Ink*.dc.html` are the screens. `InkHomeV2`, `InkProductV2`, `InkCartV2`, `InkOrdersV2` are the latest versions of those four screens. Use V2 where it exists.

If a file is missing, stop and tell me which one. Do not invent a different look.

Design language, in plain words:
- Soft white app, white and soft-grey rounded cards, one charcoal pill button per screen. Colour comes mostly from product photos.
- Big plain titles, small quiet details, lots of space.
- Floating pill tab bar at the bottom with content passing underneath it.
- Soft shadow under each product photo so it looks placed on a surface.
- Amber chips only for savings and warnings. Never decorative colour.

Tokens (put them in Tailwind v4 `@theme` in the main CSS, never hard-code colours in components):

| Token | Value |
|---|---|
| background | #FAF9F6 |
| surface (cards) | #FFFFFF |
| surface-soft (product cards, inputs) | #F2F1ED |
| ink (text, primary button) | #1C1B19 |
| muted text | #5C5A55 |
| line | #E9E7E2 |
| saving chip bg / text | #FCEBC4 / #6B4A06 |
| success | #1F7A4D |
| error | #B3261E |
| dot accent (notification) | #F4B63F |

Radius: pills 28px, cards 20–24px, chips 20–22px. Font: Inter plus Noto Sans Devanagari as fallback (self-hosted, subset, `font-display: swap`). Body 15–16px, never below 12px. Touch targets at least 48 × 48 (44 minimum for small chips).

## 2b. Existing pages: restyle, do not throw away

The PWA already has pages from earlier work. Do not delete or rewrite them from scratch. Their logic is working and tested: routes, API calls and hooks (TanStack Query), mock data, form validation, auth flow, cart and checkout logic, types.

Step 0, before any change (read-only, no edits):
1. List every existing page and shared component in `client/src`.
2. For each, match it to a screen in `client/design-ref/` and mark one of: **keep** (already fits), **restyle** (same logic, new look), **rebuild UI** (keep hooks and data, replace markup), or **new** (no page yet). Also list old pages with no matching design (keep them working, restyle later).
3. Write the table to `client/docs/UI_MIGRATION.md` and show me a short summary. Then continue without waiting, unless something would be deleted.

Rules for the migration:
- Keep all hooks, API clients, types, validators and tests. Change only presentation (markup, classes, motion).
- Keep every existing route URL working so links and tests do not break.
- Existing tests must still pass after each step. If a test breaks only because markup changed, update the test and say so.
- Replace old UI components with the new primitives one screen at a time, and delete an old component only when nothing imports it any more.
- Never leave a screen half old, half new. Finish a screen, then move on.
- If the old page has a feature the design does not show, keep the feature and style it in the new look. Tell me about it.

## 3. Tech choices (verified from 2026 sources; confirm exact package names with the official docs and `npm view` before installing)

Keep the current stack: React, Vite, TypeScript, Tailwind v4, TanStack Query. Do not replace it. Add only what is listed.

1. **Components: shadcn/ui.** In July 2026 shadcn made Base UI the default for new projects. Radix is still supported and not deprecated, and both can live in one project. Our existing hand-written components stay. For new overlay components (Drawer, Dialog, Toast, Select, Tabs) use the shadcn CLI. Check if `components.json` exists. If not, set it up carefully inside `client/` only, choose Base UI, and show me the diff summary. Never rewrite working components just to swap libraries.
2. **Bottom sheets: shadcn Drawer** (it now runs on Base UI, not Vaul). Use `swipeDirection="down"` and `snapPoints`. Use it for: filters, quantity picker, address change, size and colour grid, "Report a problem". On wide screens use a Dialog instead. Add `body { position: relative; }` for iOS Safari.
3. **Toasts: shadcn Toast** (Base UI) with swipe to dismiss and an Undo action. Per UX_RULES, undo toast instead of confirm dialogs, except for pay, reject order, delete product.
4. **Styling: Tailwind v4 CSS-first.** Tokens in `@theme`. Use container queries (`@container`) for cards that must work in grids and lists.
5. **Motion: the `motion` package** (the new name of Framer Motion; import from `motion/react`). Use `LazyMotion` with `domAnimation` and the `m` component to keep it small. Measure the bundle before and after and report the numbers. Animate only `transform` and `opacity`. Never animate width, height, top or left.
6. **Route transitions: View Transitions API.** Same-document view transitions work in Chrome and Edge 111+, Safari 18+ and Firefox 144+. Use it for page changes and for the product-card-to-product-page image morph. Feature-detect (`document.startViewTransition`) and fall back to a simple fade. Do not rely on view transition types (Firefox does not have them yet).
7. **Scroll effects: CSS scroll-driven animations only as progressive enhancement.** Not Baseline yet (Firefox stable still has it behind a flag). Wrap in `@supports (animation-timeline: view())` and `@media (prefers-reduced-motion: no-preference)`. The default state must be fully visible and usable. Use only for small things such as a header that shrinks.
8. **PWA: `vite-plugin-pwa` with Workbox.** Installable manifest, icons, splash, theme colour, custom "Add to home screen" banner, offline banner. Offline cache for Home, categories, cart and orders per UX_RULES. Never auto-retry payments. Service worker update prompt ("New version, tap to refresh").
9. **Lists:** virtualize long lists (search results, orders, seller orders) with a lightweight list virtualizer. Page size 20.
10. **Forms:** React Hook Form with Zod, using the same field rules as the backend validators. Number pad keyboards (`inputMode="numeric"`) for phone, OTP, pincode, quantity.
11. **Text:** all strings from resource files (`en` and `hi`) via a small i18n library. No hard-coded text. Language switch in Account and on Login.

## 4. Motion system (one place, one set of rules)

Create `client/src/motion/` with shared presets and use only those. No one-off animations in screens.

- Durations: tap feedback 100ms, small UI 180–220ms, sheets and page changes 250–300ms. Nothing longer than 350ms.
- Springs: one "snappy" (buttons, chips, stepper), one "soft" (sheets, cards). Same values everywhere.
- Press: `whileTap` scale 0.97 on every button, chip and card.
- Page change: View Transition with a short fade and slide. Product image morphs from the list card to the product page (shared element).
- Sheets: slide up with spring, backdrop fades in, swipe down to close.
- Lists: items fade and rise in with a small stagger (max 6 items, 40ms apart), only on first load. Never on every scroll.
- Cart: when quantity changes the total counts up or down smoothly (200ms). When an item is removed the row collapses and an Undo toast appears.
- Add to cart: the button shows a quick check mark; the cart icon in the header bumps once. On Android also call `navigator.vibrate(10)` if available.
- Skeletons instead of spinners for every list and screen. Shimmer must be a cheap CSS transform, not a background-position animation.
- Order tracking: the progress bar fills step by step on open. The current step has a soft pulse (2 cycles, then stop).
- Reduced motion: respect `prefers-reduced-motion` everywhere (`useReducedMotion`). With it on, only opacity changes, no movement.
- Low-end safety: if the device is weak (`navigator.deviceMemory <= 2` or `hardwareConcurrency <= 4`, when available) switch to the light profile: no stagger, no blur, shorter durations.
- `backdrop-filter` blur only on the floating tab bar and sheet backdrop. Never on list items. Provide a solid-colour fallback with `@supports not (backdrop-filter: blur(1px))` and in the light profile.

## 5. Smart features to add (UI first, real backend later)

Build the UI and the client logic now. Where the backend does not exist, use mock data and mark the call with `// TODO(BE)`.

1. **Voice search in Hindi and English.** Mic button inside the search box using the Web Speech API (`hi-IN` and `en-IN`). If unsupported, hide the mic. Show what was heard before searching and let the user fix it.
2. **Search by photo.** Camera button opens the phone camera or gallery (`<input type="file" accept="image/*" capture>`). Compress the image on the device before upload. Backend does not exist yet: show the screen with mock "similar products".
3. **Smart reorder.** "Order again" card on Home (last order) and on Orders. One tap fills the cart, then shows the cart with price changes highlighted.
4. **Share on WhatsApp.** Web Share API with a `wa.me` fallback for order tracking, product and cart. Message text from resource files.
5. **Help on WhatsApp** on every order and product screen, with the order or product id prefilled.
6. **Quick quantity.** +50 and +100 chips, stepper, and typing. A hint such as "Add 50 more, save ₹10 on each piece" computed from the slabs.
7. **Pincode check.** Typing a 6-digit pincode shows the city and state (mock lookup) and updates landed prices everywhere.
8. **Offline and slow network.** Cached Home, categories, cart and orders. Clear banner when offline. Queue only safe actions (add to cart); never queue payments.
9. **Install prompt and update prompt** as in section 3.8.

Do not build these now: push notifications, biometric login, Bluetooth, file system access. They need backend or product decisions first.

## 6. Components to build or update (in this order)

1. Theme and tokens, fonts, motion presets, i18n setup, base layout.
2. Primitives: Button (primary pill, soft, ghost), Chip, Card, SoftCard, Input, PhoneInput, OtpInput, Stepper, QuantityChips, Badge, SavingChip, Skeleton, EmptyState, ErrorState, OfflineBanner, Toast.
3. Layout: Header (round back button), FloatingTabBar (retailer: Home, Search, Orders, Account; seller: Home, Orders, Products, Payouts), BottomSheet (Drawer), StickyActionBar.
4. Product: ProductCard (soft card with photo shadow), PhotoGallery with thumbnails, SlabPriceTable, LandedPriceBreakdown, VariantPicker (colour, size).
5. Screens in this order. Stop after step A and after step B and ask me to review on my phone:
   - A: Home, Product, Cart.
   - B: Search with filter sheet, Orders, Track order.
   - C: Login, OTP, Business details, Waiting for approval.
   - D: Seller Home, Seller Orders (accept or decline), Add product.

Every screen needs: loading (skeleton), empty, error, offline, and long-text states (long product names, Hindi text, 10 price slabs).

## 7. Performance budget (measure on a mid-range Android, Chrome, 4G)

- Home visible in under 2 s. Search results under 1 s after typing stops.
- Initial JS as small as possible: split by route, lazy-load Drawer, motion features and the camera/voice code. Report the gzip size of the initial bundle and of each route chunk. Tell me if the initial bundle is above 200 KB gzip and why.
- Images: WebP, sized for the screen with `srcset`, lazy loaded, blurred placeholder, fixed aspect ratio to avoid layout shift.
- Fonts: self-hosted, subset, preloaded for the first paint only.
- No layout shift. Test with Chrome DevTools: CPU throttling 4× and "Slow 4G".
- Lighthouse mobile: Performance 90+, Accessibility 95+, PWA installable.

## 8. Accessibility

- Contrast at least 4.5:1 for text. Check the amber chip and muted text.
- Every icon has a text label. Only back, close and search may be icon-only, and they need `aria-label`.
- Visible focus ring, keyboard friendly, screen reader names on all controls.
- Works at 200% font size without breaking layout.
- Works at 360px width. Check 360, 390 and 430.

## 9. Definition of done for this task

- [ ] Matches `client/design-ref/` at 390px width (screenshot comparison for each screen)
- [ ] Loading, empty, error, offline, long-text states done
- [ ] No hard-coded colours or text (run a search to prove it)
- [ ] Reduced-motion and low-end profiles work
- [ ] `npm run build`, type check, lint and tests pass
- [ ] Bundle report and Lighthouse numbers included
- [ ] Committed on `frontend/pwa` in small commits, one per step

## 10. How to report back

Keep it short. After each stage (A, B, C, D) give me:
1. What is done, in 5 lines or fewer.
2. Screenshots of each screen at 390px.
3. Bundle size and Lighthouse numbers.
4. Anything you could not do, or any place the design could not be followed, and why.
5. What you need from me.

Ask me only when a decision is really mine. Otherwise choose the simplest option, say what you chose, and continue.
