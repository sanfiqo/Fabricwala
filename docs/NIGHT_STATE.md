# Morning report

Pending: night run in progress.

## Run state

- Branch: `backend/api-for-screens`; backend checkout only. Work alone.
- Scope: `src/`, `tests/`, migrations, `docs/`; never `client/` or sibling folders.
- Never access `appsettings.Local.json`; never output secrets; development OTP and fake/test payments only.
- Process: first unfinished task only; targeted tests per task, full suite every three tasks and at end. One task per commit (`P-NIGHT <id>: <what>`).
- After three failed attempts: revert only this task's uncommitted implementation, log BLOCKED, continue. Stop on three consecutive BLOCKED tasks, unrecoverable full-suite failure after three attempts, or completed queue.
- Commit hashes are recorded after commits, in the following checkpoint. No unrelated phase ticks.
- Baseline: build passed; suite 90 passed / 0 failed. Consecutive blocked tasks: 0. Tasks since full suite: 0.

## Queue

- [x] 0. Read rules, contracts and phases; create state; baseline build/tests. Commit recorded in next checkpoint.

### A. Contract
- [ ] A1. Audit Gaps and rows for the not-yet-designed screens.
- [ ] A2. Stable `{ code, message, fields? }` errors everywhere; document codes.

### B. Core journey
- [ ] B1. Auth: send/verify OTP (Development 123456), refresh, logout; retailer/manufacturer/admin.
- [ ] B2. Business: create/get, pincode lookup, optional 15-character GST, verification status/note.
- [ ] B3. Admin approvals: paged status list, review detail/GST/documents, approve/reject with note.
- [ ] B4. Retailer catalogue: home/categories/best sellers; paged search/filter; detail/tiers/landed price/variants.
- [ ] B5. Cart: CRUD, tier price and add-more savings data.
- [ ] B6. Checkout: address/order/test payment, Idempotency-Key, seller splits.
- [ ] B7. Retailer orders: paged status groups, detail/timeline, confirm delivery.
- [ ] B8. Seller orders: status groups/actions, configurable fee (2% default), acceptance expiry/full refund/fake clock.
- [ ] B9. Seller products: filtered/searchable list, on/off, CRUD/draft/publish, tiers/MOQ/variants/stock.
- [ ] B10. Payouts: hold until confirmed or seven days, summary/per-order/CSV; no bank numbers.
- [ ] B11. Seller dashboard: counts, monthly sales/next payout, six-month chart, low stock/best sellers.
- [ ] B12. Notifications: list/unread count/read one/read all.
- [ ] B13. Problem reports: enum/pieces/photos/note; open claims hold payout.
- [ ] B14. Account: profile/address CRUD/language en or hi.
- [ ] B15. Admin home: today/seven/thirty day stats and decision counts.

### C. HTTP integration proof
- [ ] C1. FullJourney: signup/approval/publish/search/cart/checkout/accept/ship/delivery/payout through HTTP.
- [ ] C2. Expired seller acceptance cancels and refunds.
- [ ] C3. Open problem report holds payout.

### D. Frontend handoff
- [ ] D1. Export `docs/openapi.json`; preserve DTO names/log any renames.
- [ ] D2. Development-only design seed businesses/products/admin.
- [ ] D3. Local run guide: base URL, development users/OTP, user-secrets names only.
- [ ] D4. Development-only localhost:5173 and LAN frontend CORS.

### E. Only after A-D
- [ ] E1. Admin orders/claims/businesses/moderation/payouts/commission lists.
- [ ] E2. Saved products/order rating/invoice list/pincode check/reorder.

## Validation

- Baseline `dotnet build TextileConnect.slnx --no-restore --nologo`: passed (12 pre-existing warnings: 10 dependency advisories, 2 nullable dereferences in ReturnClaimEndpoints).
- Baseline `dotnet test TextileConnect.slnx --no-build --no-restore --nologo`: 90 passed, 0 failed, 0 reported skipped.
- MySQL coverage limitation: `TEXTILECONNECT_TEST_MYSQL` is unset; existing MySQL test returns without exercising a database. No local settings or secrets were read.

## Log

- 0: Latest explicit night-run instruction supersedes the cancelled frontend task and authorizes backend contract changes. The four supplied untracked docs are run inputs; preserve prompt and UX text and track them on this branch. No sibling access, frontend work, or sub-agents.
- 0: Source audit names 23 designed plus 15 additional screens (38 total); the night prompt refers to 33 additional screens, but its referenced section does not name them. A1 will audit every named screen, document the discrepancy, and leave unidentified screens explicitly unspecified.
- 0: `docs/PHASES.md` already marks backend P10 tasks complete; night queue is authoritative for this run. No frontend tasks or real-service setup will be attempted.
- 0: Created requested branch from backend `master` at `d0aa587`; baseline green. Existing dependency advisory and nullable warnings are recorded above; do not count the early-return MySQL test as database proof.
