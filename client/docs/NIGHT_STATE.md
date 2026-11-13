# NIGHT STATE

## Morning report
(written when the run stops)

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
- [ ] D1: `npm run e2e` full journey with screenshots in docs/e2e/
- [ ] D2: `npm run smoke` every screen, EN 390, HI 360/390

### E. Polish
- [ ] E1: Screenshot compare Home, Product, Cart (POLISH_LIST.md refresh)
- [ ] E2: Look and feel checks
- [ ] E3: Advanced features (update prompt, font preload still open)
- [ ] E4: Image slot `public/photos/<productId>-<n>.jpg`

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
