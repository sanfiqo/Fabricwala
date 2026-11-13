# Frontend decisions (Phase 6)

Short notes on choices made without asking. Newest at the bottom.

- **PHASES.md ticks:** not ticked from this branch. The backend agent edits the same file in parallel, so ticking here would cause merge conflicts. Tick P6.x after merging.
- **Tailwind v4** (CSS-first `@theme` in `src/index.css`) with `@tailwindcss/vite`. No `tailwind.config.js`.
- **shadcn/ui:** the CLI is interactive and needs network, so components are written by hand in the shadcn style (`cva` + `clsx` + `tailwind-merge`, files under `src/components/ui/`). No Radix dependency yet; bottom sheets and tabs are small custom components. Add Radix only if accessibility needs grow.
- **Path alias** `@/` -> `src/`.
- **Text:** every visible string is in `src/i18n/en.ts` and used as `S.group.key`. `fmt()` fills `{placeholders}`. For Hindi/Urdu later: add `hi.ts`/`ur.ts` with the same shape and make `S` in `src/i18n/index.ts` pick one. Urdu needs `dir="rtl"` on `<html>`; not done yet (P6.6).
- **Mock mode** (`VITE_USE_MOCKS=true`): the API client hands each request to `src/api/mock/router.ts`, which keeps data in memory and in `localStorage` (`tc_mock_db_v1`, `tc_mock_session`). It enforces the same rules as the architecture doc (approval gate, MOQ, slab price, landed cost, ownership). Demo numbers are on the login screen; OTP is always `123456`.
- **Enums are expected as strings** from the backend (`"Approved"`, `"PendingAcceptance"`). If the backend serialises ints, add `JsonStringEnumConverter` on the API side.
- **CSRF:** `GET auth/csrf` is expected to return `{ "token": "..." }`. The token is cached, dropped after login/logout, and refreshed once if a POST fails with an antiforgery 400.
- **Delivery city for landed cost:** taken from the retailer's default Delivery address (`businesses/me`). No separate city picker unless the business has no delivery address.
- **Cart/checkout summary:** `GET cart` is expected to return server-computed shipping, GST and totals per manufacturer (the architecture doc has no checkout-preview endpoint). The client only displays them; the server recalculates at `POST checkout`.
- **Payments:** Razorpay account is not set up (see P5.9). After checkout the app calls `POST payments` and shows the result. Real Razorpay checkout.js is not wired; if `keyId` is null it shows "payment is being set up". Mock mode marks the order paid.
- **Manufacturers can accept only paid orders** (flow B: payment, then accept). The Accept button is disabled with an explanation when `paymentStatus != Paid`.
- **Confirm delivery** is offered to the retailer when a seller order is `ReachedKashmir`.
- **Product photos in mock mode** are resized in the browser and stored as data URLs. Documents are not stored.
- **Icons:** the PWA icon is one SVG (`public/icon.svg`). Some Android/iOS versions want PNG icons; generate 192/512 PNGs before store-quality install testing.

## UI migration (Step 1 and Stage A, charcoal ink design)

- **UX_RULES.md was not found.** `client/docs/UX_RULES.md` does not exist in the working tree (searched all of `d:\2026 projects`). I followed AGENTS.md and the rules inside `CLAUDE_CODE_UI_PROMPT.md`. Re-check the screens against UX_RULES once the file is in place.
- **Tokens:** design tokens live in `@theme` in `src/index.css` (names: `background, surface, soft, ink, mute, line, saving, saving-bg, success, error, dot`). The old `navy-*`, `leaf-*`, `warn`, `bad` names still exist but now point at the ink palette, so screens that are not migrated yet look like part of the same app. Delete them after the last screen moves.
- **`mute` is the muted-text colour** (#5C5A55). shadcn's `muted` (a background) is mapped to the soft grey, because the generated Drawer and Dialog files use it that way.
- **shadcn:** `components.json` added (Base UI, `base-nova`). `drawer.tsx` and `dialog.tsx` come from the shadcn CLI; the CLI wrote them to a folder literally named `@/`, so I moved them to `src/components/ui/`, pointed `cn` at `@/lib/utils`, replaced the `animate-in` classes (we do not ship tw-animate-css) with Base UI `data-starting-style` transitions, and used our timing (280 ms). The Toast is hand-written on `@base-ui/react/toast` because the CLI has no Base UI toast. Hand-written components stay (Button, fields, cards).
- **Sheet:** `Sheet` in `ui/misc.tsx` keeps its old props, so every existing screen got the new Drawer (phones) or Dialog (768px and wider) with no edits. It loads in its own chunk the first time something opens.
- **Fonts:** Inter and Noto Sans Devanagari are self-hosted from `@fontsource-variable`, with only three files (Inter latin, Inter latin-ext for the rupee sign, Devanagari). `font-display: swap`. Not preloaded yet: Vite gives the files hashed names, so a `<link rel=preload>` needs a small plugin. Left for the performance pass.
- **i18n:** `S` is now a Proxy that reads the active language, so all existing `S.group.key` code works. Language is saved in `localStorage` (`tc_lang`), defaults to the phone language (Hindi if it starts with `hi`). Changing language re-mounts the app root (`main.tsx`), which resets in-progress form state on that one action. `hi.ts` must have exactly the keys of `en.ts` (TypeScript checks this).
- **Router:** `BrowserRouter` became `createBrowserRouter` with one catch-all route. This is what `<Link viewTransition>` and the card-to-product photo morph need. Routes and URLs are unchanged.
- **Mock switch:** Account has a Demo mode on/off switch; `?mock=1` or `?mock=0` in any URL does the same. It overrides `VITE_USE_MOCKS` and reloads the app.
- **Motion:** presets in `src/motion/` (durations, two springs, reduced-motion and weak-phone profile). `motion` loads lazily (`LazyMotion`). The count-up number is a 20-line rAF tween instead of the library, so it adds no bytes. Press feedback is a CSS class (`.press`, scale 0.97 in 100 ms), cheaper than a JS handler on every button.
- **Cart row removal:** the row fades and slides out (opacity and transform only), then an Undo toast appears. It does not animate height, as the prompt forbids it, so rows below jump up after the fade. Undo re-adds the item.
- **Product page:** the design shows one quantity and sizes as a set. Our data has stock and quantity per colour and size, so I kept that: pick a colour, tap a size, and the bar edits that size's quantity. Sizes with a quantity show a small count. Slab price still follows the total across sizes.
- **Not in Stage A (by design):** Save (heart) on the product page, notification bell on Home (no Notifications screen yet), voice and photo search, mic button, pincode lookup, per-seller arrival date in the cart (the cart API has no city or transit days). The Filters button on Home opens the existing product list until the filter sheet is built in Stage B.
- **Order again:** needs `variantId` on order items. Added as optional in `types.ts` and filled by the mock. `// TODO(BE)`: the real order response must include it. Without it, the button adds nothing and shows a message.
- **Cart bottom bar** says "Checkout · total" and goes to the existing checkout page. The design says "Pay"; payment itself is not wired (P5.9).
- **Tab bar:** all roles use the floating pill. It is hidden on Product, Cart and Checkout, which have their own bottom action bar (as in the design files). Seller and admin keep a slim header with Log out until their stages. Retailer has Account (new, minimal: language, demo switch, log out).
