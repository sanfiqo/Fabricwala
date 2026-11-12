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
