# PHASES.md — TextileConnect task list

How to use: take the first unchecked task, do it, build + test, commit `<id>: <title>`, tick it `[x]`.
Rules for working are in `AGENTS.md`. Stop only at the end of a phase or when AGENTS.md says to stop.

**What the ticks mean (checked against the code on 17 Nov 2026).** `[x]` = done and shown by a test or by running it. `[ ]` = not done, or done only in part: the note after it says what is missing.
Items that were ticked before but are not really done have been unticked and annotated, so nobody trusts them. The newest work (F0 to F9) is in the completion plan at the end of this file, and `docs/PROGRESS.md` is the dated log.

Launch target: **all India**, one category first (winter wear). (The first plan named four towns in Kashmir; it was widened on 13 Nov 2026.)
Owners of work: [BE] backend, folders `src/` and `tests/`; [WEB] the web app, folder `client/`, branch `frontend/pwa`. One agent per folder at a time. A [WEB] task starts only after its [BE] task is done.

---

## Phase 0 — Foundation
- [x] P0.1 Create .NET 10 solution skeleton (6 projects), git init, build passes
- [x] P0.2 Remove template files (Class1.cs, WeatherForecast*, UnitTest1.cs); build; commit
- [x] P0.3 Add AGENTS.md (root) and docs/PHASES.md to the repo; commit
- [x] P0.4 Add base packages: MySql.EntityFrameworkCore, Serilog, FluentValidation, Swashbuckle/OpenAPI UI
- [x] P0.5 Api: Serilog, global error handling (own error body `{code, message, fields}`), health endpoints (`/api/v1/health`, `/health/live`, `/health/ready`)
- [x] P0.6 Connection string + keys via user-secrets; required secrets documented in docs/SECRETS.md (names only, no values)

## Phase 1 — Identity (Sanfiqo.Identity)
- [x] P1.1 ApplicationUser, ApplicationRole, Identity DbContext in Sanfiqo.Identity
- [x] P1.2 OTP challenge service, policy, entities, delivery (expiry, single use, attempt limit, resend/IP/mobile limits, hashed storage)
- [x] P1.3 Contact ownership + crypto, HMAC key from options
- [x] P1.4 Cookie + antiforgery + claims factory + cookie validator as `AddSanfiqoIdentity()` / `MapSanfiqoAuthEndpoints()`
- [x] P1.5 Options class for all settings
- [x] P1.6 Roles Admin, Retailer, Manufacturer seeded at startup. (Admin MFA is still a TODO.)
- [x] P1.7 OTP delivery interface with a Development fake sender. **The real MSG91 sender does not exist** (P5.9, P11.1, F3.2).
- [x] P1.8 Wired into the Api; initial migration
- [x] P1.9 End to end OTP login tests, wrong code locks after 5 tries
- [ ] P1.10 Data Protection keys are persisted in the identity database (`PersistKeysToDbContext`), but no test proves it and the keys are not encrypted at rest yet (F3.6)

## Phase 2 — Businesses and approval
- [x] P2.1 Domain: Business, BusinessUser, BusinessAddress, BusinessDocument
- [ ] P2.2 Master data: states, cities and nine delivery zones are seeded, **but only 2 pincodes**; the full pincode directory is not loaded yet (F7.1). The seed is all India, not J&K only.
- [x] P2.3 Migrations and seed apply (the seed runs at startup; migrations are applied by `dotnet ef`, never by the app)
- [x] P2.4 `ICurrentBusiness`: the business of the signed-in user, used for every isolation filter
- [x] P2.5 API: POST /businesses, GET/PUT /businesses/me, address book
- [x] P2.6 Document upload through `IFileStorage`, private access. (Only the Development disk storage exists: F3.1.) Uploads are checked by file bytes and size.
- [x] P2.7 Admin API: pending list, approve, reject with note
- [x] P2.8 Policy `ApprovedBusiness`
- [ ] P2.9 Staff users: `POST /businesses/me/users` exists but adds a staff member by **user id**, not by mobile number as planned
- [x] P2.10 Isolation tests, including the automated walk over every route with an id (`IdorWalkTests`)

## Phase 3 — Catalogue
- [x] P3.1 Domain: Category, Product, ProductMedia, ProductVariant, PriceSlab, PlatformSetting
- [x] P3.2 Categories seeded
- [x] P3.3 Seller API: create/edit/delete own products with variants, tiers, photos
- [x] P3.4 Tier validation
- [x] P3.5 Public API: categories, products (search, filters, paging), product page
- [x] P3.6 Shipping rates and zone rates + admin CRUD
- [x] P3.7 Landed cost
- [x] P3.8 Tests

## Phase 4 — Cart and orders
- [x] P4.1 Domain: CartItem, Order, SellerOrder, OrderItem (snapshots), OrderStatusHistory
- [x] P4.2 Cart API
- [x] P4.3 Checkout computed on the server, split by seller, idempotent, stock reserved
- [x] P4.4 Retailer orders with status timeline
- [x] P4.5 Seller orders: accept, reject, dispatch (one state machine)
- [x] P4.6 Shipments: seller adds InTransit or ReachedCity, only the retailer confirms delivery
- [ ] P4.7 Notifications: in-app notifications are written on every status change; **`INotificationSender` (WhatsApp/SMS) exists but nothing calls it** (F3.3)
- [x] P4.8 Tests: split by seller, tampering, isolation

## Phase 5 — Payments and launch readiness
- [ ] P5.1 Payments: orders and payment sessions work with the fake gateway; the Razorpay gateway is written but has never talked to Razorpay (F3.4)
- [x] P5.2 Webhook: signature checked, each event applied once (`PaymentWebhookEvents`)
- [ ] P5.3 PayoutAccounts store a linked account id, but there is no onboarding flow (F3.5)
- [ ] P5.4 Settlements: held, scheduled and released by rule (docs/SETTLEMENT.md), **but "release" only changes a status: no money moves to the seller** (F3.5)
- [ ] P5.5 Refunds: created and sent to the gateway interface; the fake gateway is the only one tried; a real refund was never made (F3.4)
- [ ] P5.6 Invoices are stored with a number and a PDF, **but the PDF is not a GST invoice** (F4.1)
- [x] P5.7 Audit log: rebuilt (docs/AUDIT.md)
- [x] P5.8 Rate limits per client address on sign-in and search plus an overall limit; CORS for the frontend only outside Development
- [ ] P5.9 BLOCKED: needs accounts (MSG91 and WhatsApp)

## Phase 6 — Frontend PWA (client/)
Tracked in `client/docs/` and in Phase F8 below; the screens exist and run against the real API (`npm run e2e:real`), but the list of what is verified and what is missing belongs to the frontend.

## Phase 7 — After first 20 real orders (do not start early)
These were "backend foundations": empty stubs. They were reviewed and removed on 17 Nov 2026 (F6) rather than left half done.
- [ ] P7.1 RFQ (enquiries, quotations): deleted, not built
- [ ] P7.2 Disputes with evidence: deleted; the problem report on an order (claims with photos, seller reply, admin decision) does this job
- [ ] P7.3 Product reviews: deleted; the order rating (1 to 5 stars) exists and feeds the seller rating
- [x] P7.4 Saved products and one-click reorder (`/saved-products`, `POST /orders/{id}/reorder`)
- [ ] P7.5 Collective buying: deleted, not built
- [ ] P7.6 Logistics partner role: deleted, not built

## Open decisions (ask before building)
- Who pays commission in MVP: manufacturer only (current behaviour) or shared with retailer? Also: commission on refunded goods, clawback of a payout that was already released (docs/SETTLEMENT.md).
- Settlement release: after delivery confirm, or N days (what N)? Today: at delivery confirmation, or 7 days after dispatch.
- Brand name and domain.
- PDF library for GST invoices; log sink and error tracking vendor; dataset for all-India pincodes (see the completion plan).

---

## Phase 10 — Launch essentials (MVP+)

### Data contract and auth
- [x] P10.1 [BE] Response records for all endpoints, no EF entity in a response (enforced by `NoEntityInResponsesTests`)
- [x] P10.2 [BE] Claims refresh after admin approval without logout

### All India
- [ ] P10.3 [BE] All-India data: 36 states and union territories, 90 cities, 9 pincode zones are seeded; **the pincode directory (about 19,000 pincodes) is not loaded** (F7.1). The import command exists.
- [x] P10.4 [BE] Landed cost uses the retailer pincode zone; "shipping on request" when no rate
- [ ] P10.5 [WEB] Pincode/city picker on home; landed price everywhere uses it

### Retailer
- [x] P10.6 [BE] Search: word search through the MySQL full-text index with a "contains" fallback, filters, filter counts, sort, paging. (The full-text path has not been run on a real MySQL yet, F5.2.)
- [ ] P10.7 [WEB] Search screen with filter bottom sheet and sort
- [x] P10.8 [BE] Address book: several delivery addresses, default address
- [ ] P10.9 [BE] Invoices: a PDF per seller order is stored and downloadable, **but it is not a GST invoice** (F4.1)
- [ ] P10.10 [WEB] Invoice download on order detail
- [x] P10.11 [BE] Problem report on a shipped or delivered order: reason, photos, seller reply, admin decision, partial refund
- [ ] P10.12 [WEB] Report a problem screen + claim status
- [x] P10.13 [BE] Notification centre: list, unread count, mark read
- [ ] P10.14 [WEB] Notification centre screen + bell badge
- [x] P10.15 [BE] Seller storefront endpoint
- [ ] P10.16 [WEB] Storefront screen

### Manufacturer
- [x] P10.17 [BE] Seller dashboard API
- [ ] P10.18 [WEB] Seller dashboard screen
- [ ] P10.19 [BE] Bulk product upload: **CSV only (no Excel)**: template, preview, import; photos by SKU (checked by bytes and size)
- [ ] P10.20 [WEB] Bulk upload screen with error preview
- [x] P10.21 [BE] Stock alerts and duplicate check
- [x] P10.22 [BE] Payout statement with CSV export
- [ ] P10.23 [WEB] Payouts screen

### Admin
- [x] P10.24 [BE] Admin dashboard API
- [x] P10.25 [BE] Reports export CSV
- [x] P10.26 [BE] Product moderation
- [x] P10.27 [BE] Categories and home banners admin
- [x] P10.28 [BE] Suspend / reactivate business
- [ ] P10.29 [WEB] Admin screens for P10.24–P10.28

### Legal pages
- [ ] P10.30 [WEB] Privacy policy, terms of use, seller agreement, refund and returns policy pages (content placeholders marked [LEGAL REVIEW NEEDED])

---

## Phase 11 — Production readiness (before launch)
(The same work, with its exact status, is Phase F3 and F9 of the completion plan below.)

- [ ] P11.1 [BE] Real MSG91 OTP sender — needs account (F3.2)
- [ ] P11.2 [BE] Razorpay live mode + Route linked accounts — needs account (F3.4, F3.5)
- [ ] P11.3 [BE] WhatsApp Cloud API sender — needs Meta verification (F3.3)
- [ ] P11.4 [BE] File storage on Cloudflare R2 or Azure Blob with signed URLs — needs account (F3.1). **Without it the app refuses to start in Production** (`ProductionAdapterGuard`).
- [ ] P11.5 [BE] Health checks are done (live and ready, database and storage) and logs are one JSON line per request with a correlation id and no secrets; **the log sink and the error tracker are not chosen** (F9.6)
- [ ] P11.6 [BE] Dockerfile, compose file, environment table (docs/DEPLOY.md), backup and restore scripts are written; **none of them has been run** (no Docker or MySQL server was available)
- [ ] P11.7 [BE] CI workflow is written (`.github/workflows/ci.yml`); **it has never run**: GitHub refused to start the jobs because of an account billing problem
- [ ] P11.8 [BE] Security pass: docs/SECURITY_REVIEW.md, per-client rate limits, headers, upload checks, IDOR walk test; **a secret scanner (gitleaks) and a penetration test were not run**
- [ ] P11.9 [BE] k6 load test is written (`ops/load`); **not run**
- [ ] P11.10 [WEB] Production build, PWA icons from brand, error pages, analytics events

---

## Phase 12 — After launch (growth)

- [ ] P12.1 Coupons and offers (seller-funded and platform-funded)
- [ ] P12.2 Reviews and ratings UI
- [ ] P12.3 Wishlist, recently viewed, product compare
- [ ] P12.4 Quick order: quantities for many products on one screen (reorder of a whole past order exists)
- [ ] P12.5 Courier integration (Shiprocket / Delhivery)
- [ ] P12.6 Regional languages: Hindi first, then Gujarati, Marathi, Tamil, Bengali, Telugu, Punjabi, Urdu (RTL)
- [ ] P12.7 Referral
- [ ] P12.8 Seller subscription plans

---

## Phase 13 — Franchise network (later, after Phase 12.8)

- [ ] P13.1 Franchise role, territories (city / pincode list), one franchise per territory
- [ ] P13.2 Attribution: manufacturer linked to the franchise that onboarded it (first come wins)
- [ ] P13.3 Earnings ledger: share of subscription (and optional share of commission), only when the manufacturer is approved and paid; clawback if it churns within 90 days
- [ ] P13.4 Payouts to franchise via Razorpay Route linked account
- [ ] P13.5 Franchise dashboard
- Rules: single level only (no franchise under franchise); written agreement; TDS on commission — confirm with CA

---

## Completion plan (FABRICWALA_COMPLETION_PROMPT.md)

### Phase F0 - Establish the truth
- [x] F0.1 Build and tests run: 211 passed, 0 failed, 0 skipped (before F0 changes). Build warning NU1903 (`System.Security.Cryptography.Xml` 10.0.9 has known high severity advisories, pulled in by the test project) is open, see F2.3.
- [x] F0.2 `ProductionAdapterGuard`: outside Development and Testing the app stops at startup while file storage, payment gateway or OTP sender is a development or unconfigured adapter. 7 tests; shown with a real `Production` start.
- [x] F0.3 `AddControllers` and `MapControllers` removed (no controller class exists); JSON enum options stay on `ConfigureHttpJsonOptions`. OpenAPI drift test unchanged and green.
- [x] F0.4 `docs/ARCHITECTURE.md` section 2 and tech stack, and the stack lines in `docs/AGENTS.md`, now say what is built and what is planned.

### Phase F1 - Data integrity
- [x] F1.1 Every relation in the F1.1 list is a real foreign key with `ON DELETE RESTRICT` (relations with navigation properties are configured through them). The model test pins the list.
- [x] F1.2 `docs/FK_ORPHAN_CHECK.sql` is generated from the model (a test keeps it in step). Run it on a non-empty database before the migration.
- [ ] F1.3 Migration `AddForeignKeys` is generated (32 foreign keys, 11 new indexes, the 5 old cascade keys become restrict) and its SQL was reviewed offline. **Not yet applied to a fresh MySQL database and to a database built from the old migrations: needs the owner (see the commands in the F1 report).**
- [x] F1.4 `CreatedAtUtc` and `UpdatedAtUtc` added to Settlement, Quotation, Dispute, Enquiry, GroupBuy, GroupBuyParticipant, LogisticsAssignment, Review, Favourite (migration `AddTimestamps` backfills existing rows with now). `MarketplaceDbContext` stamps new and changed rows on both `SaveChanges` and `SaveChangesAsync`. 2 tests.
- [x] F1.5 Status texts are constants (`SellerOrderStatus`, `PaymentStatus`, `SettlementStatus`, `RefundStatus` in `TextileConnect.Domain`); services, readers, `OrderStatusRules` and tests use them. The API/database values are unchanged. `MarketplaceDbContext` refuses to save an unknown status (9 tests). Left as text on purpose: shipment status, claim decisions (Approved/Rejected/PartiallyRefunded), the derived display statuses of `OrderStatusRules`, and the stub tables.
- [x] F1.6 `PaymentWebhookEvents` (unique per gateway and event id) makes the Razorpay webhook idempotent: a replayed event id returns 200 with no second effect; an event that failed is handled when the gateway sends it again. Migration `AddPaymentWebhookEvents`. 3 tests.
- [x] F1.7 Indexes reviewed for the catalogue, seller inbox, retailer orders, notifications and the per-minute jobs. 8 composite indexes added (migration `AddHotQueryIndexes`; two status columns become `varchar(255)` so they can be indexed); `docs/INDEXES.md` lists each index and the query it serves. Chosen by reading the queries, not measured (no MySQL for `EXPLAIN` here).

### Phase F2 - Test safety net
- [x] F2.1 MySQL test category: `[MySqlFact]` tests are reported as skipped without `TEXTILECONNECT_TEST_MYSQL`; with it, `MySqlStore.RunAsync` gives each test its own database built from the migrations and `MarketplaceHttpFixture` uses it. `MySqlSuiteTests` runs 16 existing tests on MySQL (checkout split, payment and webhook replay, refund on rejection and expiry, late payment, payout hold and release, claim hold, the whole journey, concurrency). **Written but not yet run on a real MySQL server: needs the owner or CI (see docs/TESTING.md).**
- [x] F2.2 Two checkouts for the last stock: exactly one wins, the other gets 409, stock ends at 0 (in-memory today, also in the MySQL suite where the order count is checked too).
- [x] F2.3 `.github/workflows/ci.yml`: build, fast suite, MySQL suite (`mysql:8.0` service), vulnerable-package check that fails the build, and for `client/` `npm ci`, `npm run lint`, `npm run build`. **Not run on GitHub yet.** Also fixed the open NU1903 warning: `System.Security.Cryptography.Xml` and `.Pkcs` are pinned to 10.0.15 (the 10.0.9 versions pulled in by DataProtection had high severity advisories).
- [x] F2.4 `docs/TESTING.md`.

### Phase F4 - Business correctness
- [x] F4.5 `SellerOrderTransitions` is the one rule table for seller order moves; the order service applies it for accept, reject, dispatch, cancel and delivery. A table test covers every from-to pair (36) and every seller action in the service. Closed a loophole: the seller could set the shipment status to "Delivered" without moving the order or the payout; the shipment route now accepts only InTransit and ReachedCity for a shipped order. Unknown shipment statuses are refused on save like the other statuses.
- [x] F4.3 GSTIN check character (mod 36 algorithm, verified on a published valid GSTIN) and state code added to the format check (`GstinRules`, 17 tests). A real GST portal lookup is not built: it needs an API account from the owner.
- [x] F4.2 `docs/SETTLEMENT.md` written from the code, each rule with a test: commission on goods only with default and override rates (3 orders, rate frozen per order), one rounding rule (half away from zero; the Razorpay paise conversion now uses it too, it truncated before), payout date from `Settlement.AutoReleaseDays` after dispatch, claim refund reduces the held payout and never below 0. Removed two unused duplicates in `IFinanceService` (a second release routine and a second refund creator) so there is one implementation of each. Open: who bears the commission, commission on refunded goods, clawback of a released payout.
- [x] F4.4 Audit log reviewed and rebuilt (`docs/AUDIT.md`): the old interceptor wrote nothing useful (sync saves only, no user), so only product moderation was ever recorded. Now every admin write is recorded by `AdminAuditMiddleware` (user, route, id, IP) and the money and order events (payment, order moves, refunds, payout release, claim decision, settings change) are recorded in the same save as the change through `IAuditTrail`. Columns added: `IpAddress`, `Details` (migration `AddAuditDetails`). 6 tests; request bodies, OTPs and document keys are never stored. Open: no endpoint to read the log, proxy IP (F9.5), no endpoint to set a seller commission override.
- [ ] F4.1 Real GST invoice: **waiting for the owner's choice of PDF library** (plan says ask first). Findings for it: the invoice PDF today is one text block with the order id and the subtotal; `OrderItems` do not keep the HSN code; the seller registered address has no street line (sign-up only takes the pincode); invoice numbers are a truncated GUID, not a series per seller and financial year. Needs: HSN snapshot at checkout, seller and buyer address lines, a numbering table, CGST/SGST or IGST by state of supply.

### Phase F5 - Search and catalogue quality
- [x] F5.1 `Product.UnitsSold` is a stored counter, raised by the pieces of a seller order when the retailer confirms delivery; the "popular" sort and the list no longer run a sum over order items for every product row. Cancelled, rejected and expired orders never raise it; a problem report does not lower it (delivered pieces stay counted). Migration `AddProductUnitsSold` backfills from delivered orders and adds an index for the sort. 3 tests. Behaviour change: popularity now follows delivery, not payment.
- [x] F5.2 Word search uses a MySQL full-text index (`MATCH ... AGAINST`, migration `AddProductFullText`) for plain words of 3 letters or more, with "contains" as the fallback for short, ignored or non-English words; the query text is reduced to letters and digits (`ProductTextSearch`, 11 cases). Same parameters and response as before. **The index path is written but not run on a real MySQL server yet** (`ProductSearchMySqlTests`, needs the owner or CI). See `docs/SEARCH.md`.
- [x] F5.3 `GET /api/v1/products/filters`: counts for category, material, season, size, colour, seller state, seller city and the price range, each counted without its own filter. 4 tests. The reader now builds its rows in one place for both the list and the counts.
- [x] F5.5 Photo copies at upload: main 800, card 400, thumb 120 (square WebP, fitted on white, never cut or enlarged, camera orientation applied) and a tiny `lqip` data URL; `?size=` on the photo address; `srcSet` and `lqip` on product, list, cart and seller responses; old photos keep working. SkiaSharp (MIT) is the image library. Migration `AddProductMediaVariants`. 17 new tests. See `docs/MEDIA.md`. Not done: a photo on order items.
- [ ] F5.4 **[PROVE]** The 20,000 product benchmark (`CataloguePerformanceTests`, seeder `CatalogueSeeder`, fails when a p95 is 300 ms or more) is written and its seeder is tested, but it has **not been run**: it needs a real MySQL server. `docs/PERFORMANCE.md` says how to run it and where to paste the result. Open until a table with p95 < 300 ms is in that file.

### Phase F6 - Complete or remove the stubs
Owner decisions (17 Nov 2026): delete RFQ, delete product reviews, delete group buy, dispute and logistics assignment.
- [x] F6.1 RFQ: `Enquiry`, `Quotation`, the `POST /enquiries` stub and their tables are deleted.
- [x] F6.2 Reviews: `Review`, `POST /reviews` and the table are deleted. The seller rating (dashboard and storefront, which always showed 0 from the empty reviews table) now comes from the order ratings (`SellerRating`).
- [x] F6.3 Saved products: one concept. The duplicate `POST /favourites/{productId}` route and `ICommunityService` are gone; `GET/PUT/DELETE /saved-products` remain. `Favourite` now has foreign keys to the business and the product.
- [x] F6.4 `GroupBuy`, `GroupBuyParticipant`, `Dispute`, `LogisticsAssignment`, their endpoint and tests are deleted. Migration `RemoveCommunityStubs` drops the 7 stub tables (they held no real data: nothing but empty-body stubs ever wrote to them).
- [x] F6.5 No endpoint returns an entity: `NoEntityInResponsesTests` reads the compiled endpoint code and fails if a Domain class is returned (inside lists, wrappers or anonymous objects too). It found and fixed: admin shipping rates, categories and banners, the public banners list, business addresses and staff. Those routes now return response records and take request records (the caller can no longer set an id or a business id). Banner image and link are checked to be web addresses or in-app paths.

### Phase F7 - Location and master data
- [ ] F7.1 **[PROVE]** Counts of a fresh database measured and pinned by a test: 36 states and union territories, 90 cities, 9 zones and **2 pincodes**, so the all-India claim was false (`docs/MASTER_DATA.md`). The repeatable import command `import-pincodes <file.csv>` is written and tested on a sample in the India Post layout. **Open until the owner approves a data source and the full directory is imported** (then the real counts go into MASTER_DATA.md).
- [x] F7.2 No region specific constants in the code: the unused `MasterDataSeed.KashmirDeliveryCities` file is removed and the sourcing hubs are one list (`IndiaMasterData.SourcingHubs`) used by the seed and the importer; zones and cities come from the database (imported data), not from lists in the code.
- [x] F7.3 One pincode check for every place an address is saved: `pincode_invalid` or `pincode_unknown` (400, with the pincode in the message) at sign-up, `/account/addresses` and `/businesses/me/addresses`; the city comes from the pincode, not from the caller. 6 tests.

### Phase F9 - Docs, ops and release
- [x] F9.1 `ARCHITECTURE.md` rewritten from the code; `API_REFERENCE.md` and `DATA_MODEL.md` are generated by tests and checked for drift.
- [x] F9.2 `PHASES.md` corrected; the night and prompt files moved to `docs/archive/`.
- [ ] F9.3 **[PROVE]** `Dockerfile`, `docker-compose.yml`, `DEPLOY.md`, live and ready probes: written, probes tested; the image and compose were **never built or run** (no Docker on this machine).
- [ ] F9.4 **[PROVE]** Daily backup, restore and `--verify` scripts (bash and PowerShell), RPO 24 h and RTO about 1 to 2 h in `DEPLOY.md`: syntax checked only, never run against MySQL; the RTO is an estimate.
- [x] F9.5 `SECURITY_REVIEW.md` with the OWASP table, headers, per-client rate limits (a real shared-counter bug fixed), IDOR walk (49 routes, no leak), upload checks by bytes, CSV formula injection fixed. Open: admin MFA, outside pen test, gitleaks not run.
- [ ] F9.6 Structured logs with a correlation id and no secrets are done and tested; **the log sink and the error tracker are not chosen** (owner decision).
- [ ] F9.7 **[PROVE]** k6 script `ops/load/catalogue-and-checkout.js` written, never run.
- [x] F9.8 Root `README.md`.

### Phase F3 - Real adapters
Owner has only Razorpay test keys (17 Nov 2026); MSG91, R2, WhatsApp and Route are not started because each needs an account.
- [ ] F3.4 Razorpay in test mode: the client now sends per-call credentials (the shared header was a race), cuts receipts to Razorpay's 40 characters, returns Razorpay's own reason on refusal (`PaymentGatewayException`, no key in it), turns a gateway failure at payment start into `payment_gateway_unavailable`, and the key id sent to the client now follows the documented `Payments:Razorpay:KeyId` name (it used to fall back to `dev_key`). 11 tests against a fake server. **One real test-mode order was created** (`RAZORPAY_LIVE_TEST=1`). Not yet tried against the real service: a refund, a webhook delivery (needs the webhook secret and a public URL), the checkout popup with the frontend.
- [ ] F3.1 R2 storage, F3.2 MSG91, F3.3 WhatsApp, F3.5 Route payouts, F3.6 key encryption: waiting for accounts.
