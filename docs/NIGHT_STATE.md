# Morning report

Pending: night run in progress.

## Run state

- Branch: `backend/api-for-screens`; backend checkout only. Work alone.
- Scope: `src/`, `tests/`, migrations, `docs/`; never `client/` or sibling folders.
- Never access `appsettings.Local.json`; never output secrets; development OTP and fake/test payments only.
- Process: first unfinished task only; targeted tests per task, full suite every three tasks and at end. One task per commit (`P-NIGHT <id>: <what>`).
- After three failed attempts: revert only this task's uncommitted implementation, log BLOCKED, continue. Stop on three consecutive BLOCKED tasks, unrecoverable full-suite failure after three attempts, or completed queue.
- Commit hashes are recorded after commits, in the following checkpoint. No unrelated phase ticks.
- Latest full suite: 150 passed / 0 failed. Consecutive blocked tasks: 0. Tasks since full suite: 0.

## Queue

- [x] 0. Read rules, contracts and phases; create state; baseline build/tests. `50b5235`.

### A. Contract
- [x] A1. Audit Gaps and rows for all 38 named screens; document 18 unspecified screen identities. `ffcac6f`.
- [x] A2. Stable `{ code, message, fields? }` errors everywhere; document codes. `adeaebb`.

### B. Core journey
- [x] B1. Auth: send/verify OTP (Development 123456), refresh, logout; retailer/manufacturer/admin. `8d2d146`.
- [x] B2. Business: create/get, pincode lookup, optional 15-character GST, verification status/note. `f1705dc`.
- [x] B3. Admin approvals: paged status list, review detail/GST/documents, approve/reject with note. `4cb4987`.
- [x] B4. Retailer catalogue: home/categories/best sellers; paged search/filter; detail/tiers/landed price/variants.
  - [x] B4.1. Populated catalogue/home/category DTOs, search filters, stable paging, public seller visibility, stored product attributes and migration. `6268c28`.
  - [x] B4.2. Shared server landed-price quote by pincode/quantity, shipping/GST/fee handling and product integration. `4253dec`.
- [x] B5. Cart: CRUD, tier price and add-more savings data. `615b6ce`.
- [x] B6. Checkout: address/order/test payment, Idempotency-Key, seller splits.
  - [x] B6.1. Order snapshots/totals/commission, seller split, idempotent checkout, stock reservation, status history, migration. `1729b55`.
  - [x] B6.2. Payment: idempotent create, test gateway completion (Development), verified idempotent webhook, order status/notifications. `1611351`.
- [x] B7. Retailer orders: paged status groups, detail/timeline, confirm delivery. `9345a97`.
- [x] B8. Seller orders: status groups/actions, configurable fee (2% default), acceptance expiry/full refund/fake clock. `694f6fb`.
- [x] B9. Seller products: filtered/searchable list, on/off, CRUD/draft/publish, tiers/MOQ/variants/stock. `COMMIT`.
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
- B3: build and 11 targeted approval/profile/security-stamp tests passed. Full suite due after B4; HTTP fixture still uses isolated test authentication/in-memory persistence.
- B4.1: build, 8 targeted tests and full suite (119 passed / 0 failed) passed. Migration SQL validated offline; no live database migration or MySQL flow exercised.

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
- B3 in progress: paged stable approval lists; review with masked phone, GST format result explicitly not a live registry check, authorized document URLs. Required rejection note is enforced in Application/service and endpoint; business and all-member claim/stamp changes share a MySQL transaction/row lock. Keep unrelated Admin roles; rejection removes approved claims. Status changes queue notifications; repeating the same decision does not duplicate notifications.
- B3 complete: 11 tests green, including all-member claim removal, required notes, admin role denial, missing resources, pagination, document isolation/privacy and notification deduplication. No packages or renamed DTOs. Next B4.
- B4 split: current product summaries return placeholder prices/seller fields and Product lacks the documented material/weight/spec attributes. First complete real catalogue data and filters (B4.1, then full suite); then central landed-cost quotes (B4.2) for later cart/checkout reuse. Keep existing DTO names and query aliases.
- B4.1 attempt 1: six existing catalogue tests passed; two new HTTP tests failed in fixture setup because mixed explicit/generated variant IDs collided in the in-memory provider. Assign all fixture variant IDs explicitly. Catalogue migration scaffolded offline; existing product creation dates are unknown and use an explicit epoch backfill rather than fabricated history.
- B4.1 complete: real catalogue fields, stable paged filtering, paid-unit popularity and approved-seller visibility are covered by HTTP tests. Batch media/stock/city lookups avoid list N+1 queries. No packages or existing DTO names changed; metadata/index migration SQL checked. Full suite 119/0. Next B4.2.
- B4.2 decisions: one batched pricing service for catalogue, cart and checkout; zone rates take precedence over city fallback, preferring per-piece when both rate types exist. Minimum freight applies to either type; unknown weight cannot produce a per-kilo quote. Totals round to paise away from zero; rounded per-unit displays need not multiply exactly to aggregate freight. GST is stored as a percentage (5 means 5%). UX rules require factory + delivery + GST, so retailer fee stays zero and seller commission is separate. Exact postal mappings validate city/pincode; unknown pincode-only destinations return 404 instead of inventing a city.
- Continued by Claude Code after Codex hit its token limit; resumed from B4.2 (Codex left the pricing service, endpoint wiring and DI uncommitted; build and 119 tests were green, tests were missing). Added 5 HTTP tests (per-piece/per-kg/min charge, on-request shipping, validation 400/404, role 401/403, catalogue card/detail quotes). B4.2 complete; B4 done. Note: the saved prompt has no section 6 (see Resume entry); proceeding under the written stop rules. Next B5.
- B5 decisions: tier price and MOQ use the product's total quantity across sizes/colours; GST fixed to percentage (old code multiplied by the raw rate, 100x too high). New validated `AddItemAsync` (variant active, product Active, seller approved manufacturer, qty 1..1,000,000, cumulative stock) because existing `AddAsync` is used by an old test with a fictitious variant. Below-MOQ shows lowest tier price + `below_moq`; unpurchasable lines flagged `unavailable` and excluded from totals; `canCheckout` false while any issue. Shipping is not in the cart (null) - B6 checkout must use IProductPricing and the same product-total quantity rule (current EfCheckoutService prices per line, hard-coded 2% commission, ignores GST). Cart now Retailer-role only. Added 5 HTTP tests. Full suite 129/0. Next B6.
- B6.1 decisions: checkout needs a quotable delivery (postal-directory pincode, seller pickup address, freight rate) else 409 `shipping_unavailable`; retailer pays goods + GST + freight, platform fee is charged to the seller (commission = subtotal x seller override or `Commission.DefaultRate`, default 2%). Stock is reserved at checkout via a concurrency token on `StockQty` (race -> 409). TODO for B8: release stock and cancel unpaid orders after a timeout, and on cancellation/refund. New `RuleViolationException(code, message, status)` for stable business-rule codes. `IdempotencyRecord` keyed (business, scope, key) with request hash; replay returns the same order with 200. Two existing tests (`Full_order_flow_...`, MySQL flow) only had their fixtures extended (city, postal code, pickup address, freight rate) and call the new checkout signature; assertions unchanged. `OrderItem`/`OrderStatusHistory` rewritten as multi-line entities with snapshot fields; `OrderStatusHistory` was previously not mapped at all. Migration NightCheckoutSnapshots (offline SQL checked; existing rows get zero totals and 2000-01-01 PlacedAtUtc). `OrderItemResponse` gained `GstAmount`; no DTO renamed.
- B6.2 decisions: `IPaymentService` (Application) / `EfPaymentService`; start is idempotent per `Idempotency-Key` and reuses a still-pending gateway order; `Failed` payments allow a new attempt. Confirmation is idempotent (second call changes nothing, no duplicate notifications), verifies the paid amount, never downgrades Paid to Failed, sets each seller order's `AcceptDeadlineUtc` (setting `Seller.AcceptWindowHours`, default 24) and notifies seller and retailer users. Webhook acknowledges unrelated events with 200, unknown gateway order 404. The `dev-complete` route only works while the fake `DevelopmentPaymentGateway` is registered (real Razorpay gateway -> 404), not tied to the environment name. Concurrency token on `Payment.Status` (migration NightPaymentStatusToken narrows the column to varchar(32)). Old inline payment code in PaymentEndpoints removed; `POST /api/v1/payments` now Retailer-only. Full suite 137/0. Next B7.
- B7 decisions: `GET /api/v1/orders` changed from a bare list to a paged object (`items,total,page,pageSize`) with `?group=`; frontend had not consumed it yet. Derived retailer status: AwaitingPayment, WaitingForSeller, Packing, Shipped, Delivered, ProblemReported, PartiallyCancelled, Cancelled; groups: problems = any seller order Rejected/Cancelled or an unresolved claim (ResolvedAtUtc null), delivered = paid and every seller order Delivered, active = everything else. List runs as one filtered SQL page plus batched lookups (no N+1). `confirm-delivery` now only moves Dispatched seller orders to Delivered (was: all, regardless of status), writes history, updates the shipment, notifies seller users; repeating it is a no-op; before shipping -> 409 `order_not_shipped`. Retailer order routes are Retailer-role only. `UpdateSellerOrderAsync` now writes an `OrderStatusHistory` row (rule 6); B8 will finish the seller state machine. Full suite 140/0. Next B8.
- B8 decisions: seller routes are Manufacturer-role only and only show orders the retailer has paid (Paid/Refunded/PartiallyRefunded); anything else is 404. Tabs: `new` (Pending, closest deadline first), `to_pack` (Accepted), `shipped` (Dispatched), `done` (Delivered/Rejected/Cancelled), `all`. Money on the seller screens: `orderValue` = what the retailer paid for this seller (goods + GST + freight), `platformFee` = goods subtotal x seller rate (override or `Commission.DefaultRate`, default 2%, stored at checkout), `youGet` = orderValue - platformFee. Strict transitions: Pending -> Accepted | Rejected (reason 1-500 chars), Accepted -> Dispatched (LR 1-60 chars, optional carrier), Delivered only by the retailer; concurrency token on `SellerOrder.Status` makes accept-vs-expiry races a 409. Reject and expiry share one path: status, stock released, full refund of the seller order's total (Refund row, then gateway refund, `Requested` rows are retried each run), history, notifications to both sides, order payment status Refunded/PartiallyRefunded. `IOrderMaintenance.ExpireAsync` (hosted `OrderMaintenanceWorker`, every minute, no new package since Hangfire is not installed) cancels paid orders past `AcceptDeadlineUtc` and unpaid orders older than `Orders.UnpaidExpiryMinutes` (default 60, no refund, stock released). A payment that arrives after cancellation is recorded and refunded automatically. All order code now takes `TimeProvider` (fake `TestClock` in tests); checkout was moved onto it after a timeline-ordering bug. `IPaymentGateway.RefundAsync` added (Razorpay implementation untested against the real API; dev gateway fake). Migration NightOrderLifecycle (Refund.GatewayRefundId/CreatedAtUtc, index). `UpdateSellerOrderAsync` kept its old overload and gained one with carrierName. Full suite 146/0. Next B9.
- B9 decisions: old seller product routes took raw domain entities (overposting, seller could set Status/Id) and are replaced by safe DTOs (`SellerProductRequest` etc.); `GET /api/v1/seller/products` is now paged (`filter=all|low_stock|draft|active|inactive`, `q` on title/SKU, `threshold` default 5), `POST` creates (201), `PUT /{id}` edits, `PUT /{id}/active` is the on/off toggle, `DELETE` unchanged (soft). Drafts need only a title; publishing needs category, MOQ>=1, tiers (first tier = MOQ, ascending, price never rises), >=1 active variant; errors are `validation_failed` with field paths like `tiers[0].minQty`. Publish = Active immediately (admin moderation stays after the fact). Slug/SKU generated (slug stable on edit); variants left out of an edit are deactivated, never deleted. New `Product.AdminHold` (set by admin Deactivate) blocks the seller from switching it back on (409 `product_deactivated_by_admin`); migration NightSellerProducts. Seller product routes are Manufacturer-only. Full suite 150/0. Also: docs/PROGRESS.md is now updated per task (user asked); it was previously skipped because the night prompt names only NIGHT_STATE.md. Next B10.
