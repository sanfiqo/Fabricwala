# Prompt for the frontend session: connect the whole backend API

Paste this to the Claude Code session that works on the frontend (`client/`, branch `frontend/pwa`). It is written for that session only.
Backend state: `main` at the time of writing includes everything in `docs/PROGRESS.md` up to 17 Nov 2026 (GST invoices, credit notes, platform fee GST, admin second step, audit log, commission rate).

---

## 0. Your job

Make the React client talk to the **real** backend for **every** screen and every route, with no screen left on mock data. The backend is finished for this round; you only change `client/`. If something in the API is missing or wrong, do not work around it silently: write it in `client/docs/BACKEND_REQUESTS.md` (route, what you need, why) and carry on with the rest.

Read first, in this order, and nothing else until you need it:
1. `docs/openapi.json` (the contract; copy it again now, it changed a lot) and `docs/API_REFERENCE.md` (who may call each route, rate limit group, request and response schema names).
2. `docs/API_SCREENS.md` (screen by screen notes), `docs/INVOICING.md`, `docs/SETTLEMENT.md`, `docs/DEPLOY.md` section "Admin second step".
3. `client/src/api/` (`client.ts`, `types.ts`, `areas/*`, `mock/*`) to see what is already wired. About 66 of 129 routes are already called; the rest are listed in section 3.

## 1. Rules of the contract (these decide whether it works at all)

- **Same site, cookie auth.** Sign in = `POST /api/v1/auth/otp/request` then `POST /api/v1/auth/otp/verify` (dev code `123456`). The session is an HttpOnly cookie; the client never stores a token. In development use the Vite proxy so the browser sees one origin; never ask the backend for CORS changes.
- **CSRF.** Every `POST`, `PUT`, `DELETE` needs the header `X-CSRF-TOKEN` from `GET /api/v1/auth/csrf` (`{ token }`). Fetch a new token after sign-in and after sign-out (a new session needs a new token).
- **Errors** are always `{ code, message, fields? }`. Show `message`, branch on `code` (for example `pincode_unknown`, `invoice_not_ready`, `seller_gstin_missing`, `refund_not_processed`). Never show raw JSON or a stack. `fields` maps a field name to its message: put it under the input.
- **429**: read `Retry-After` (seconds) and show a countdown; do not retry in a loop.
- **Idempotency**: `POST /api/v1/checkout` and `POST /api/v1/payments` need an `Idempotency-Key` header (8 to 100 characters). Make one key per user action (for example when the Pay button is first pressed) and **reuse it on a retry** of the same action.
- **Money** is a decimal with 2 places in rupees. Never do arithmetic on money in the client except to add what the API already returned; show what the API returns (`total`, `platformFee`, `platformFeeGst`, `youGet`). Format with `en-IN`.
- **Ids** are numbers (bigint), user ids are strings. **Times** are UTC ISO strings: show them in the user's local time.
- **Images**: use the URLs the API returns, as they are (for example `/api/v1/products/{slug}/media/{id}?size=thumb`). Use `srcSet` and `lqip` where the product response has them. Order lines have `imageUrl` (may be null: show a placeholder).
- **PDFs** (invoice, credit note) are downloaded with the cookie: fetch as a blob and open or save it; the file name comes from `Content-Disposition`.
- **Pagination**: `page` (from 1) and `pageSize` (1 to 100) everywhere; the answer is `{ items, total, page, pageSize }`.
- **Roles**: Admin, Retailer, Manufacturer. A session can also be *waiting for the second step*: see section 4.
- Send `X-Correlation-Id` (a random 8 to 64 character id) on every request and show it in the "something went wrong" screen, so a user can quote it to support.

## 2. How to work

1. Generate TypeScript types from `docs/openapi.json` (for example `openapi-typescript`) into `client/src/api/generated.ts` and make `types.ts` re-export from it. Delete hand-written types that the contract now covers. A route that returns an anonymous object is typed in the contract by its inline schema: use it, do not guess.
2. Keep the structure: one file per area in `src/api/areas/`, screens import only from `src/api/endpoints.ts`.
3. The **mock layer must stay working** (it is how the UI is developed offline) but a screen must never need it: with `VITE_USE_MOCKS=false` nothing may break. When you add a real call, add the same route to the mock router with the same shape, so both modes behave the same. Then **delete mock-only behaviour that the real API does not have** (for example fake status changes).
4. Every visible text goes in `src/i18n/en.ts` **and** `hi.ts`.
5. Work area by area. After each area: `npm run build`, `npm run lint`, and the e2e against the real backend (`npm run e2e:real`, extend it). Commit locally per area (`FE-API <area>: ...`). **Do not push and do not open a PR**; the owner will say.
6. Run the backend yourself: `dotnet run --project src/TextileConnect.Api` from the repo root (Development: fake OTP `123456`, fake payment gateway, demo data). Never read or print `appsettings.Local.json`, never put a key or password in the client or in a commit.

## 3. Routes still to connect (verify with a script, do not trust this list blindly)

Write `client/scripts/api-coverage.mjs`: read `docs/openapi.json`, scan `src/api/areas/*.ts`, print routes that no area calls. The goal is an **empty list**, except `health*`, `payments/webhook`, `payments/{id}/dev-complete` (development only; the dev screen may use it) and `products/{slug}/media/{id}` (used through image URLs). Run it in CI of the client (`npm run check:api`).

As of this writing these are not called:

**Retailer**
- Account: address edit, delete and default (`PUT/DELETE account/addresses/{id}`, `PUT .../default`), language (`PUT account/language`), pincode check at checkout (`GET account/pincode-check`: show "we deliver here" or the error).
- Home and discovery: `GET home` (one call for the home screen), `GET banners`, `GET products/filters` (counts for category, material, season, size, colour, seller state and city, price range: build the filter sheet from it; counts update with the other chosen filters).
- Saved products: `GET saved-products`, `PUT/DELETE saved-products/{productId}` (heart button, saved list screen).
- Seller storefront: `GET storefronts/{businessId}` (seller page with rating and products).
- Orders: rate an order (`GET/POST orders/{id}/rating`); invoices list `GET invoices` and download `GET invoices/seller-orders/{id}/download`; credit note `GET invoices/refunds/{refundId}/credit-note`. The invoice exists only after dispatch: before that `409 invoice_not_ready` means "available after the seller dispatches".
- Payment: `POST payments` returns the gateway session with the Razorpay key id and order id: open Razorpay Checkout with it (test keys), then poll `GET payments/{gatewayOrderId}` until paid. In development with the fake gateway use `POST payments/{id}/dev-complete`.
- Team: `POST businesses/me/users` (add a staff member), `GET businesses/me/documents/{id}`.

**Seller (Manufacturer)**
- Claims: `POST seller/claims/{claimId}/respond`.
- Shipment: `POST seller/orders/{id}/shipment` (note InTransit or ReachedCity; the seller cannot mark Delivered, only the retailer confirms).
- Products: photo delete (`DELETE seller/products/{id}/media/{mediaId}`), bulk template, preview, import and images (`seller/products/bulk/*`), inventory alerts and duplicates (`seller/inventory/*`).
- Orders show `platformFee`, `platformFeeGst` and `youGet`; the payout screen must show the GST on the fee as its own line.
- Invoices: seller downloads the same tax invoice, and the platform fee invoice with `?type=PlatformCommission`.

**Admin**
- Lists: `admin/orders` (+ `{id}`), `admin/claims` (+ `{id}`, photos, `decide`), `admin/sellers`, `admin/retailers`, `admin/products` (+ `moderate`), `admin/payouts`, `admin/commission`, `admin/reports/{kind}` (CSV download), `admin/businesses/{id}/suspension`, document download.
- Catalogue: `admin/catalogue/categories` (list, create, update) and `admin/catalogue/banners`.
- **New**: `GET/PUT admin/platform-company` (legal name, GSTIN, address, pincode, GST rate on the fee; show `isComplete`; explain that fee GST starts only when it is complete); `GET admin/audit-log` (filters: action prefix, entity, user, from, to; paged; newest first); `PUT admin/sellers/{id}/commission-rate` (percent in the form, **fraction in the API**: 3% is `0.03`; empty means "use the default").
- Settings and shipping rates are already wired: re-check them against the contract.

## 4. Admin second step (authenticator app)

The profile (`auth/me`, `otp/verify`, `refresh`) has a new field `mfaPending`: `null`, `"enrol"` or `"verify"`. While it is set, **`roles` is empty** and every admin route answers 403: that is by design.
- `"enrol"`: show an enrol screen. `POST auth/mfa/setup` returns `sharedKey` and `authenticatorUri`: draw a QR code of the URI (a small QR library) and also show the key as text. Then `POST auth/mfa/enable` with `{ code }`. The answer holds `recoveryCodes` (10, **shown once**: make the user confirm they saved them, offer copy and download) and the new `profile`.
- `"verify"`: show a code screen (6 digits, or a recovery code with a hyphen). `POST auth/mfa/verify` with `{ code }`. `401` = wrong code, `429` = locked for a few minutes (show it).
- When the profile comes back with `mfaPending = null` and roles filled, go to the admin home. A normal retailer or seller never sees any of this.
- The second step is switched by the server setting `Auth:AdminMfa` (off by default); build the screens regardless and test them by running the backend with `Auth__AdminMfa=Required`.

## 4b. Optional parts behind switches, and the register (added 18 Nov 2026)

**Feature switches.** `GET /api/v1/features` returns `{ "Rfq": true|false }` (public). Load it once after sign-in and **hide the screens of a feature that is off**; a call to such a route answers `404` with code `feature_off`. An admin switches a part with `PUT /api/v1/admin/features/{name}` and `{ "on": true }` (add a small settings screen for it).

**Requests for quotation** (read `docs/RFQ.md` first; all behind `Rfq`):
- Retailer: "Ask for a price" form (`POST /api/v1/rfqs`: title, description, optional category, quantity, unit, optional target price, delivery pincode, optional needed-by date, days open 1 to 30; show `fields` errors), "My requests" (`GET /api/v1/rfqs?status=`), request detail with all quotations cheapest first (`GET /api/v1/rfqs/{id}`), cancel, and **accept** a quotation (confirm dialog: "the others are rejected"). After it is accepted the winner's `contact` (name, phone) is shown: say that the platform only connects the two.
- Manufacturer: feed of open requests (`GET /api/v1/seller/rfqs?categoryId=&q=`; each row has `iHaveQuoted`), request detail (the buyer is not named), quote form (`PUT /api/v1/seller/rfqs/{id}/quotation`: price per piece, quantity up to what was asked, days to deliver, note), withdraw (`DELETE` same path), "My quotations" (`GET /api/v1/seller/quotations?status=`). After acceptance the detail has `buyerContact`.
- Admin: list (`GET /api/v1/admin/rfqs?status=`) and close with a reason (`POST /api/v1/admin/rfqs/{id}/close`).
- Statuses to show in both languages: Open, Expired, Awarded, Cancelled, Closed; quotation: Submitted, Withdrawn, Accepted, Rejected. The notification links look like `/rfqs/{id}` (retailer) and `/seller/rfqs/{id}` (manufacturer).
- The error codes are `rfq_limit_reached`, `rfq_not_open`, `quotation_not_available`, `quotation_final`, `rfq_changed`, `pincode_unknown`, `pincode_invalid`, `validation_failed` (with `fields`): show a friendly line for each.

**Invoice register for the accountant.** Manufacturer: `GET /api/v1/seller/invoices/register?from=yyyy-MM-dd&to=yyyy-MM-dd` (JSON, paged: `rows`, `sales` and `purchases` totals) and the same with `&format=csv` as a download button ("Download for my accountant"). Admin: `GET /api/v1/admin/invoices/register` (platform fee invoices, optional `sellerId`). Default is this month; show the totals above the table; credit notes are listed as `CreditNote` and count as minus.

**Platform company** (admin): `GET/PUT /api/v1/admin/platform-company`: until it is complete (`isComplete`) new orders carry no GST on the platform fee; say so on the screen.

## 5. Things to check on every screen

- Loading, empty, error and offline states; no spinner that never ends; a failed save keeps what the user typed.
- Retailer cannot see seller or admin routes and the other way round; a 403 or a session that expired (401) goes to sign-in without losing the page.
- A seller or retailer who is not approved sees the waiting screen (the profile says so), not 403 errors.
- Order status words: the API sends `PendingAcceptance`, `Accepted`, `Dispatched`, `Delivered`, `Rejected`, `Cancelled`; the retailer list groups are `active`, `delivered`, `problems`, `all`. Map to friendly text in both languages.
- Phone width first (360 px), keyboard does not hide the main button, every tap target 44 px.

## 6. Done means

- `node client/scripts/api-coverage.mjs` prints nothing but the allowed exceptions.
- `npm run build`, `npm run lint` and `npm run e2e:real` pass against the real backend; the e2e covers: sign-up and approval, search with filters, cart, checkout and payment (dev gateway), seller accept and dispatch, retailer confirm, invoice download, a problem report with a refund and credit note, rating, saved products, admin lists, platform company, audit log, commission rate, and the second step with `Auth__AdminMfa=Required` (the test computes the TOTP from `sharedKey`: HMAC-SHA1, 30 s, 6 digits).
- A report of at most 8 lines: routes connected, tests, what you could not do and why, what the owner must do. Say plainly what was not run (a real Razorpay payment, a real SMS).
