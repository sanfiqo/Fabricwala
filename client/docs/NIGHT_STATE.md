# NIGHT STATE

## Morning report
- **Done (commit):** A1 fc26d84, A2 970843c, A3 f40c257, A4 15a88c7, B1 3e614ab, B2a 2e4c02e, B2b 43ab19c, B2c a04da24, B3 474ed82, B4a e34e9a6, B4b 09eddc8, B5 9d46031, C1-C5 e650b18 (mock + adapter), D1 94e010f, D2 cab0939, E1-E4 9e7f877.
- **BLOCKED:** none. C1-C5 were closed as mock + adapter ready, not tested against a real server, because no API was reachable.
- **Build, lint:** pass. **Smoke** (`npm run smoke`, 108 screens, EN 390, HI 360, HI 390, admin 1280): pass. **E2E** (`npm run e2e`, 23 steps): pass. Initial JS about 97 kB gzip for the main chunk (under 200 KB).
- **Real API or mock:** every area is mock. Switch per area with `VITE_REAL_AREAS=catalog,cart,...` or all with `VITE_USE_MOCKS=false`.
- **Bugs found and fixed on the way:** a saved login state could bounce a reload through /login; a saved empty cart sent /shop/checkout back to the cart; focus ring and button font rules were beating Tailwind classes.
- **Still different from the design / not done:** voice and photo search (mic), Save heart, ratings, category counts, order-again thumbnail, cart arrival date and seller city; garment cut-out photos (textures until real photos arrive); Add/Edit product keeps the old layout with new inputs (full InkAddProduct layout not done); Razorpay checkout not wired (pending screen shown); update prompt and install prompt cannot be seen in dev (tested with a simulated install event; update prompt needs a production build).
- **Backend requests:** `client/docs/BACKEND_REQUESTS.md` (11 items: pincode lookup, refresh, error `code`, Idempotency-Key, owner name and optional GST, notifications, problem reports, order item variantId and image, seller dashboard, seller products on/off and payouts, admin dashboard).
- **You must:** (1) copy `openapi.json` and `RUN_LOCAL.md` into `client/docs/api/` and run the backend, then try `VITE_REAL_AREAS=catalog` first; (2) send product photos named `<productId>-<n>.jpg`, 800x800 (see `public/photos/README.txt`), then `npm run photos`; (3) tell me if `Section 6 (Nonstop mode)` of the night prompt should say more, it was missing from the file; (4) review the commits and merge yourself.
- **Phone:** in `client/` run `npm run dev -- --host`, open the `Network:` address it prints (this PC was `http://10.34.116.48:5173`), same Wi-Fi. Demo logins: 9000000001 shopkeeper, 9000000002 seller, 9000000003 admin, code 123456.


## Context (step 0)
- Read: CLAUDE.md, CLAUDE_NIGHT_PROMPT.md, CLAUDE_CODE_UI_PROMPT.md, docs/UX_RULES.md (present now), docs/UI_MIGRATION.md, DECISIONS.md, docs/API_SCREENS.md.
- Contract: only `client/docs/API_SCREENS.md` (an audit, no DTO shapes). No `openapi.json`, no `RUN_LOCAL.md`. API on localhost:5000/5001/7000 is not reachable. So: mock mode for every area, adapter layer per area, switch is one setting.
- Test commands: `npm run build`, `npm run lint`, smoke script (scratchpad `run2.mjs`; becomes `npm run smoke` in D2).

## Queue
Format: `- [ ] id: task` then `- [x] id: task (commit)` or `- [!] id: BLOCKED reason`.

### A. Foundation for real data
- [x] A1: API layer (error codes EN+HI, timeout, safe GET retry, one 401 refresh, Idempotency-Key) (see Log for commit)
- [x] A2: Adapter per area + per-area mock/real switch, seed names
- [x] A3: Query keys per area, stale times, optimistic cart quantity and notification read
- [x] A4: Auth store, role guards, session expired screen

### B. Screens
- [x] B1: Login, OTP, Business details (pincode lookup, GST check), Waiting for approval
- [x] B2a: Search with filter sheet and live count
- [x] B2b: Orders list, Track order
- [x] B2c: Account, Notifications, Report a problem
- [x] B3: Checkout restyle, payment result states
- [x] B4a: Seller Home, Seller Orders (accept, cannot do, ship with LR)
- [x] B4b: Seller Products (list, toggle), Add/Edit product, Payouts
- [x] B5: Admin (laptop layout): Home, Approvals, Review

### C. Real flows (mock + adapter ready while API is unreachable)
- [x] C1 (mock, adapter ready): Auth, business, approval
- [x] C2 (mock, adapter ready): Catalog, cart
- [x] C3 (mock, adapter ready): Checkout, orders, tracking, delivery confirm
- [x] C4 (mock, adapter ready): Seller orders, products, payouts, admin approvals
- [x] C5 (mock, adapter ready): Notifications, report a problem, account

### D. Proof
- [x] D1: `npm run e2e` full journey with screenshots in docs/e2e/
- [x] D2: `npm run smoke` every screen, EN 390, HI 360/390

### E. Polish
- [x] E1: Screenshot compare Home, Product, Cart (POLISH_LIST.md refresh)
- [x] E2: Look and feel checks
- [x] E3: Advanced features (update prompt, font preload still open)
- [x] E4: Image slot `public/photos/<productId>-<n>.jpg`

## Log
- Step 0 done. Prompt section 6 (Nonstop mode) is not in CLAUDE_NIGHT_PROMPT.md on disk (file ends at section 5); I follow section 1 stop rules and keep going through the queue.
- User rule: commit locally only; never push, reset --hard, clean, force push or delete a branch. If git needs approval: finish other work, retry once, then BLOCKED.
- A1 done: ApiError now carries `code` and a friendly EN/HI message (raw server text kept in `serverMessage` only); 20 s timeout; one retry for GET on network error/502-504; one silent POST auth/refresh before treating a 401 as expired (skipped when the backend has no route); Idempotency-Key on checkout and payments (mock honours it). TODO in B3: CheckoutPage must pass one key per attempt so a double tap reuses it.
- A2 done: `src/api/areas/*.ts` (auth, business, catalog, cart, orders, seller, admin; payouts, notifications, account get added with their screens), `endpoints.ts` re-exports. `VITE_REAL_AREAS` / `VITE_MOCK_AREAS` (or localStorage tc_real_areas / tc_mock_areas) choose per area. Mock seed names now Sharma Textiles, Kapoor Knits, Shree Kurti House; mock db key bumped to v2.
- A3 done: `src/api/keys.ts` (keys per area + STALE), per-area stale times in queryClient.ts, optimistic cart quantity with rollback (money stays as the server sent it). Notification read optimistic comes with B2c.
- A4 done: guards already per role (Gate); added `/session-expired` screen, shown when a logged-in person gets a 401. No token storage: auth is the server cookie, so nothing is kept in JS. Tested by firing the 401 event.
- B1 done: Login (+91 field, language pill, 6-box OTP with auto submit and WebOTP), Business details in 2 steps (type cards, pincode fills city and state, GST optional with live check, documents optional), Waiting for approval as a checklist. React Hook Form + Zod. Drafts in sessionStorage so a language switch or reload keeps typed data. Waiting shopkeepers can browse (Gate `allowPending`); ordering still needs approval. Backend requests: pincode lookup, owner name, WhatsApp code.
- B2a done: Search page rebuilt (search pill, filter and active-filter chips, auto load more, skeleton grid, empty state with Clear). Filter sheet has sort, price presets, MOQ, material, season and a sticky button that shows the live result count (tested 10 -> 5). Also fixed: focus ring rule moved into the base layer (it was beating `outline-none`), and a saved `me = null` is never restored (it could log you out right after login).
- B2b done: Orders list (Active / Delivered / Problems chips, "Needs your action" confirm card with auto-confirm date, skeleton, empty, error, offline from saved data) and Track order (headline, per-seller tabs, step bar that fills on open, timeline with current step pulsing twice, carrier and LR card, WhatsApp help, sticky confirm delivery). Links to `/shop/orders/:id/report` are built in B2c.
- B2c done: Account (profile, business, addresses, language, help, demo switch, log out), Notifications (Today / Earlier, unread dots, optimistic read and mark-all, bell with dot on Home), Report a problem (reason chips, pieces, up to 3 photos shrunk on the phone, note, what happens next, sent state). Mock handlers for notifications and claims. Tested: seller marks order reached, shopkeeper sees confirm card, reports a problem with a photo. Backend requests added below.
- B3 done: Checkout restyled (address radio cards, summary, sticky Place order). One Idempotency-Key per order attempt and per payment attempt (double tap makes 1 order, tested). Result screens: success, pending, failed (retry payment is manual, never automatic). Mock switch `localStorage tc_mock_payfail=1` shows the failed screen. Razorpay itself is still not wired (account blocked), so real mode ends in the pending screen.
- B4a done: Seller Home (headline, dark new-orders card with accept countdown, 4 stats, 6 month bars, low stock, best selling) and Seller Orders (New / To pack / Shipped / Done, order value, platform fee, You get, countdown chip, Cannot do it, Accept, Ship with LR number, detail page). Seller tabs: Home, Orders, My cloth (Payouts comes in B4b). Mock dashboard handler; backend request for `GET seller/dashboard` shape.
- B4b done: Seller Products (search, All / Low stock / Draft chips, row with photo slot, price, MOQ, status, on/off switch with undo toast), Payouts (next payout card, on hold, paid this month, order by order, CSV download, 7 day rule), seller tabs now Home, Orders, My cloth, Payouts. Add/Edit product keeps all its logic (variants, slab validation, photos) and uses the new inputs, cards and buttons through the shared primitives; it was not re-laid-out screen by screen (see Morning report). Fixed a stale-language bug in the product status labels.
- B5 done: Admin laptop layout (left sidebar from 1024px, floating tab bar on phones), Admin Home (needs a decision, Today / 7 / 30 days, 4 numbers), Approvals (table on laptop, cards on phone, search, status chips, Tab + Enter), Review page (details, documents, note, Approve, Reject needs a note of 5+ letters, Esc goes back). Settings and Freight keep their logic and use the new primitives. Tested at 1280 px in English and 360 px in Hindi.
- C1-C5 closed as MOCK + ADAPTER READY: the API on localhost:5000/5001/7000 is not reachable and there is no openapi.json, so no area could be tested against a real server. Each area has one adapter file in `src/api/areas/` (auth, business, catalog, cart, orders, seller, payouts, notifications, account, admin). To go real for one area: `VITE_REAL_AREAS=catalog` (or all: `VITE_USE_MOCKS=false`) and run the backend; see BACKEND_REQUESTS.md for the 11 shapes the screens assume.
- D1 done: `npm run e2e` (23 steps, starts its own demo server, screenshots in docs/e2e/). Mock data only, API not reachable.
- D2 done: `npm run smoke` (108 screens: English 390, Hindi 360 and 390, admin also 1280). It found a real bug: a saved empty cart sent a reload of /shop/checkout back to the cart. Fixed.
- E1 done: comparison refreshed (docs/polish, POLISH_LIST.md). E2 done: fonts load, 120 ms press, skeletons, photo slot, safe areas, bottom padding were verified in the polish pass and kept by the smoke run.
- E3: recent searches and suggestions DONE; filter sheet with live count DONE; quick quantity chips and tier hint DONE; undo toast on cart removal and product pause DONE; pull to refresh DONE (Home, Cart, Seller Home, Payouts); swipe-down to close sheets DONE (tested with touch); haptics DONE (add to cart, chips, remove, confirm, checkout); install prompt DONE (tested with a simulated event); offline cache for Home, categories, cart, orders DONE (tested); update prompt DONE in code (cannot be shown in dev, needs a production build with a new version); font preload DONE (checked in the built index.html).
- E4 done: `public/photos/<productId>-<n>.jpg` + `npm run photos` (manifest). Tested with a temporary file, then removed.
