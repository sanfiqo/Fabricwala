# Morning report

Pending: night run in progress.

## Run state

- Branch: `backend/api-for-screens`; backend checkout only. Work alone.
- Scope: `src/`, `tests/`, migrations, `docs/`; never `client/` or sibling folders.
- Never access `appsettings.Local.json`; never output secrets; development OTP and fake/test payments only.
- Process: first unfinished task only; targeted tests per task, full suite every three tasks and at end. One task per commit (`P-NIGHT <id>: <what>`).
- After three failed attempts: revert only this task's uncommitted implementation, log BLOCKED, continue. Stop on three consecutive BLOCKED tasks, unrecoverable full-suite failure after three attempts, or completed queue.
- Commit hashes are recorded after commits, in the following checkpoint. No unrelated phase ticks.
- Latest full suite: 108 passed / 0 failed. Consecutive blocked tasks: 0. Tasks since full suite: 1 (B2).

## Queue

- [x] 0. Read rules, contracts and phases; create state; baseline build/tests. `50b5235`.

### A. Contract
- [x] A1. Audit Gaps and rows for all 38 named screens; document 18 unspecified screen identities. `ffcac6f`.
- [x] A2. Stable `{ code, message, fields? }` errors everywhere; document codes. `adeaebb`.

### B. Core journey
- [x] B1. Auth: send/verify OTP (Development 123456), refresh, logout; retailer/manufacturer/admin. `8d2d146`.
- [x] B2. Business: create/get, pincode lookup, optional 15-character GST, verification status/note. Commit recorded in next checkpoint.
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
- A2: build and 12 loopback HTTP error/download contract tests passed on attempt 2. Full suite due after B1.
- B1 checkpoint: build passed; 16 targeted tests passed; full suite 108 passed / 0 failed (MySQL caveat above still applies).
- B2: final build and 21 targeted tests passed; MySQL migration SQL generated/checked offline. No live schema migration applied.

## Log

- 0: Latest explicit night-run instruction supersedes the cancelled frontend task and authorizes backend contract changes. The four supplied untracked docs are run inputs; preserve prompt and UX text and track them on this branch. No sibling access, frontend work, or sub-agents.
- 0: Source audit names 23 designed plus 15 additional screens (38 total); the night prompt refers to 33 additional screens, but its referenced section does not name them. A1 will audit every named screen, document the discrepancy, and leave unidentified screens explicitly unspecified.
- 0: `docs/PHASES.md` already marks backend P10 tasks complete; night queue is authoritative for this run. No frontend tasks or real-service setup will be attempted.
- 0: Created requested branch from backend `master` at `d0aa587`; baseline green. Existing dependency advisory and nullable warnings are recorded above; do not count the early-return MySQL test as database proof.
- A1: Verified endpoint registrations against all 38 named rows. Corrected Cart, Account, Approvals, Category list and Address book to partly covered (6 exists / 28 partly / 4 missing, counted from table); added explicit gaps. The input summary counts were off by one. Unnamed screens are an input gap and will not be invented; continue with the explicit backend queue.
- Resume 2026-11-14: User explicitly requested nonstop execution and section 6. The saved night prompt ends at section 5; no section 6 is present. Continue without questions using the existing written hard stops; retain completed step 0 and finish interrupted A1 rather than recreate history.
- A2 in progress: normalize API errors before auth/rate limiting; suppress arbitrary error bodies without buffering successful files, sanitize exception responses/logs and field validation values. No NuGet additions or DTO renames. HTTP contract tests use a loopback Kestrel test server, not mocked middleware results.
- A2 attempt 1: build passed; HTTP tests failed during harness startup because clearing configuration removed its writable provider. Add an empty in-memory provider (no configuration files or secrets) and rerun.
- A2 complete: 12 HTTP tests green. Stable error DTO covers framework/endpoint statuses and exceptions; arbitrary diagnostics suppressed; successful files stream unchanged. Rate-limit rejection is explicitly 429. No packages or public DTOs renamed.
- B1 decision: fixed Development OTP is 123456 without relaxing resend/IP/mobile/attempt/expiry protections. Production always uses secure cookies and random OTPs. Development HTTP supports cookies for local previews; JSON mutations require CSRF (gateway webhooks retain signature authentication). Remove OTP console logging. Add a named auth profile DTO without renaming existing DTOs.
- B1 targeted validation: 16 auth/policy/identity tests passed. HTTP role/cookie/CSRF/refresh/logout tests substitute the mobile-auth adapter and use isolated in-memory Identity storage; they are not MySQL locking/proof-consumption evidence. Full suite running at the three-task checkpoint.
- B1 complete: full suite green (108/0). AuthProfileResponse is newly named from prior anonymous JSON; no existing public DTO renamed. No new package. Next: B2.
- B2 decisions: accept a safe signup DTO; reset approval/commission/platform flags in the service; atomic business/owner/optional registered-location creation. Existing user lookup assumes one business per user, so enforce a unique membership user index. A pincode-only registered location has no fabricated street address; delivery addresses still require completion. Exact postal directory starts with verified Indore/Ludhiana entries; unknown codes return 404, never inferred from freight-zone prefixes. Further nationwide postal data remains an explicit data-import requirement.
- B2 attempt 1: build failed on the migration factory's AddUserSecrets overload; corrected to the documented ID-based overload. Adding HTTP tests for overposting, role denial, isolation, validation, duplicates and exact postal lookup before the next run.
- Git steering: commits are local only. Never push, reset --hard, clean, force push or delete branches. If a Git action awaits approval, complete independent work, retry once, then mark the task BLOCKED and move on if approval still blocks it. No Git approval rejection has occurred so far.
- B2: nine targeted tests passed on attempt 2. Scaffolded NightBusinessProfiles without opening a database; corrected EF's year-0001 backfill to a MySQL-valid date followed by existing CreatedAtUtc. Duplicate-key races map to 409 and roll back the registration transaction. Migration adds only the profile timestamp, unique membership user index and exact postal directory. No live migration applied; pre-existing duplicate memberships need review before applying the unique index.
- B2 complete: 21 final targeted tests passed; migration SQL checked. Existing EF CLI 9.0.10 emits an older-tool warning against runtime 10.0.9 but generated the migration and SQL successfully. No package added. New named DTOs only (BusinessCreateRequest, BusinessProfileResponse, PincodeResponse); existing DTO names unchanged. Next B3.
