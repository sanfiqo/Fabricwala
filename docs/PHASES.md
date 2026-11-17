# PHASES.md — TextileConnect task list

How to use: take the first unchecked task, do it, build + test, commit `<id>: <title>`, tick it `[x]`.
Rules for working are in `AGENTS.md`. Stop only at the end of a phase or when AGENTS.md says to stop.

Launch target: one category (Winter wear), retailers in Srinagar, Sopore, Baramulla, Bandipora.

---

## Phase 0 — Foundation
- [x] P0.1 Create .NET 10 solution skeleton (6 projects), git init, build passes
- [x] P0.2 Remove template files (Class1.cs, WeatherForecast*, UnitTest1.cs); build; commit
- [x] P0.3 Add AGENTS.md (root) and docs/PHASES.md to the repo; commit
- [x] P0.4 Add base packages: MySql.EntityFrameworkCore, Serilog, FluentValidation, Swashbuckle/OpenAPI UI
- [x] P0.5 Api: Serilog, global exception handler (ProblemDetails), health endpoint `/api/v1/health`
- [x] P0.6 Connection string + keys via user-secrets; document required secrets in docs/SECRETS.md (names only, no values)

## Phase 1 — Identity (Sanfiqo.Identity)
Source: old project, read-only. Only the files listed in the Step 2 prompt.
- [x] P1.1 Copy ApplicationUser, ApplicationRole, Identity DbContext into Sanfiqo.Identity (Users/, Persistence/); rename namespaces
- [x] P1.2 Copy OTP challenge service, policy, entities, delivery into Otp/; rename Customer* to neutral names; keep ALL security rules (expiry, single use, attempt limit, resend/IP/mobile limits, hashed storage)
- [x] P1.3 Copy contact ownership + crypto into Contacts/; HMAC key from options
- [x] P1.4 Security/: cookie + antiforgery + claims factory + cookie validator as extension methods `AddSanfiqoIdentity()` / `MapSanfiqoAuthEndpoints()`
- [x] P1.5 Options class for all settings (no hardcoded local-database restrictions)
- [x] P1.6 Roles Admin, Retailer, Manufacturer seeded at startup; remove MFA requirement from Manufacturer policy; TODO for Admin MFA
- [x] P1.7 `IOtpSender` with Development fake sender (logs OTP to console)
- [x] P1.8 Wire into Api; fresh initial migration; apply to local MySQL test DB
- [x] P1.9 Test end to end: request OTP -> verify -> cookie -> protected test endpoint returns 200; wrong OTP 5x gets locked
- [x] P1.10 Persist Data Protection keys in the database

## Phase 2 — Businesses and approval
- [x] P2.1 Domain: Business (type, names, GSTIN, PAN, phone, VerificationStatus, IsPlatformOwned, CommissionRateOverride), BusinessUser, BusinessAddress, BusinessDocument
- [x] P2.2 Master data: State, City (IsDeliveryZone, IsSourcingHub); seed J&K delivery cities and sourcing hubs (Surat, Delhi, Ludhiana, Panipat, Jaipur, Tiruppur, Ahmedabad, Mumbai)
- [x] P2.3 Apply migrations and seed local MySQL database
- [x] P2.4 `ICurrentBusiness` service: resolves BusinessId from the logged-in user; used for all isolation filters
- [x] P2.5 API: POST /businesses, GET/PUT /businesses/me, POST /businesses/me/addresses
- [x] P2.6 Document upload via `IFileStorage` (local disk in Development); private access
- [x] P2.7 Admin API: list pending, approve, reject (with note); approval assigns Retailer/Manufacturer role
- [x] P2.8 Policy `ApprovedBusiness` blocks listing/ordering until approved
- [x] P2.9 Staff users: POST /businesses/me/users (owner adds staff by mobile)
- [x] P2.10 Tests: isolation (business A cannot read business B), unapproved business blocked

## Phase 3 — Catalogue
- [x] P3.1 Domain: Category (ParentId tree), Product, ProductMedia, ProductVariant, PriceSlab, PlatformSettings
- [x] P3.2 Seed categories (Men's, Women's, Kids, Winter wear, Fabrics, Shawls, Innerwear, Home textiles)
- [x] P3.3 Seller API: create/edit/delete own products, variants, slabs, media (Manufacturer + ApprovedBusiness only)
- [x] P3.4 Slab validation: no overlapping ranges, first slab MinQty = Moq
- [x] P3.5 Public API: GET /categories, GET /products (search, category, price, city filters, paging), GET /products/{slug}
- [x] P3.6 ShippingRates table (From city, To city, PerKg/PerPiece, Rate, MinCharge, TransitDays) + admin CRUD
- [x] P3.7 Landed cost service + GET /landed-cost?productId=&qty=&cityId= (slab price + freight/unit + GST + platform fee)
- [x] P3.8 Tests: slab selection, landed cost math, seller cannot edit another seller's product

## Phase 4 — Cart and orders
- [x] P4.1 Domain: CartItem (business-level cart), Order, SellerOrder, OrderItem (snapshots), OrderStatusHistory
- [x] P4.2 Cart API: get, add, update qty, remove
- [x] P4.3 Checkout: validate MOQ, approved business, stock; compute slab price, shipping, GST, commission on server; split into SellerOrders
- [x] P4.4 Retailer API: list/get own orders with status timeline
- [x] P4.5 Seller API: list own SellerOrders, accept, reject (reason), dispatch (LR number)
- [x] P4.6 Shipments table + status updates (In transit, Reached Kashmir, Delivered) + retailer confirm delivery
- [x] P4.7 Notifications table + `INotificationSender` (fake in Development); queue on every status change
- [x] P4.8 Tests: checkout split by seller, price tamper rejected, isolation on orders

## Phase 5 — Payments and launch readiness
- [x] P5.1 Payments table; Razorpay order create; client payment flow endpoint
- [x] P5.2 Webhook endpoint: signature verify, idempotent, marks Payment paid
- [x] P5.3 PayoutAccounts (gateway linked account id only; no bank numbers stored)
- [x] P5.4 Settlements: release service releases transfer after delivery confirm or N days without dispute
- [x] P5.5 Refunds for rejected SellerOrders
- [x] P5.6 Invoices (seller-to-retailer, platform commission) persisted with invoice number
- [x] P5.7 AuditLogs via EF SaveChanges interceptor
- [x] P5.8 Rate limiting on auth/search; CORS for frontend domain only
- [ ] P5.9 BLOCKED: needs accounts (MSG91 and WhatsApp)

## Phase 6 — Frontend PWA (client/)
- [ ] P6.1 React + Vite + TS + Tailwind + shadcn/ui; PWA manifest; API client with cookie + CSRF header
- [ ] P6.2 Login with mobile OTP; business registration + document upload
- [ ] P6.3 Retailer: browse, search, product page with landed cost, cart, checkout, orders
- [ ] P6.4 Manufacturer: product list/editor with slabs and variants, orders inbox, dispatch
- [ ] P6.5 Admin: pending approvals, businesses, orders, settings, shipping rates
- [ ] P6.6 Hindi/Urdu text resources (later), mobile-first check on a real phone

## Phase 7 — After first 20 real orders (do not start early)
- [x] P7.1 Enquiries + Quotations (RFQ) backend foundation
- [x] P7.2 Disputes with evidence upload backend foundation
- [x] P7.3 Reviews and ratings backend
- [x] P7.4 Favourites and one-click reorder backend foundation
- [x] P7.5 Collective buying (GroupBuys) backend foundation
- [x] P7.6 Logistics partner role and dashboard backend foundation

## Open decisions (ask before building)
- Who pays commission in MVP: manufacturer only (recommended) or shared with retailer?
- Settlement release: after delivery confirm, or N days (what N)?
- Brand name and domain.

# PHASES_NEXT.md — append to docs/PHASES.md (after Phase 9)

Decided 2026-11-13: the current build is the MVP skeleton (one order end to end). These phases make it a real,
launchable app for ALL INDIA. Order matters: Phase 10 and 11 are required before launch; 12 and 13 come after.

Ownership in every phase:
- [BE] = backend, Codex, branch main, folder src/ + tests/
- [WEB] = web PWA, Claude Code, branch frontend/pwa, folder client/
Mobile (React Native) screens follow later in Phase 8, using the final design.
One agent per folder at a time. A [WEB] task starts only after its [BE] task is done.

---

## Phase 10 — Launch essentials (MVP+)

### Data contract and auth (do first)
- [x] P10.1 [BE] Explicit response DTOs for all endpoints; no EF entities in API responses; remove JSON cycle-ignore
- [x] P10.2 [BE] Claims refresh after admin approval/rejection without logout (security stamp / cookie re-validation) + test

### All India
- [x] P10.3 [BE] Replace Kashmir-specific data: seed all Indian states + major cities; delivery zones by pincode (zone table: pincode prefix → zone; rate per zone pair, PerKg/PerPiece, min charge, transit days)
- [x] P10.4 [BE] Landed cost uses retailer pincode → zone; "shipping on request" when no rate
- [ ] P10.5 [WEB] Pincode/city picker on home; landed price everywhere uses it

### Retailer
- [x] P10.6 [BE] Search: full-text on title/description/material, filters (category, price range, MOQ, seller city, material, size, colour), sort (popular, price, newest), paging
- [ ] P10.7 [WEB] Search screen with filter bottom sheet and sort
- [x] P10.8 [BE] Address book: multiple delivery addresses, default address
- [x] P10.9 [BE] Invoices: GST-compliant PDF per SellerOrder (seller → retailer) and platform commission invoice (platform → seller); download endpoint
- [ ] P10.10 [WEB] Invoice download on order detail
- [x] P10.11 [BE] Return / damage claim flow on delivered SellerOrder: reason, photos, seller response, admin decision, partial refund
- [ ] P10.12 [WEB] Report a problem screen + claim status
- [x] P10.13 [BE] Notification center: list, unread count, mark read (in-app channel)
- [ ] P10.14 [WEB] Notification center screen + bell badge
- [x] P10.15 [BE] Seller storefront endpoint: business profile, banner, about, city, rating, products
- [ ] P10.16 [WEB] Storefront screen

### Manufacturer
- [x] P10.17 [BE] Seller dashboard API: new orders, pending dispatch, sales this month, top products, payout due, low stock
- [ ] P10.18 [WEB] Seller dashboard screen
- [x] P10.19 [BE] Bulk product upload: CSV/Excel template download, validate rows, preview errors, import; bulk image upload matched by SKU
- [ ] P10.20 [WEB] Bulk upload screen with error preview
- [x] P10.21 [BE] Stock alerts (low stock, out of stock) + duplicate product
- [x] P10.22 [BE] Payout statement: per SellerOrder amount, commission, settled date; CSV export
- [ ] P10.23 [WEB] Payouts screen

### Admin
- [x] P10.24 [BE] Admin dashboard API: GMV, orders, commission earned, new businesses, pending approvals (today / 7 days / 30 days)
- [x] P10.25 [BE] Reports export CSV: orders, payments, settlements, businesses
- [x] P10.26 [BE] Product moderation: list, flag, deactivate with reason; seller notified
- [x] P10.27 [BE] Categories admin (tree, reorder) and home banners admin (image, link, start/end date)
- [x] P10.28 [BE] Suspend / reactivate business and user; suspended cannot log in or order
- [ ] P10.29 [WEB] Admin screens for P10.24–P10.28

### Legal pages
- [ ] P10.30 [WEB] Privacy policy, terms of use, seller agreement, refund and returns policy pages (content placeholders marked [LEGAL REVIEW NEEDED])

---

## Phase 11 — Production readiness (before launch)

- [ ] P11.1 [BE] Real MSG91 OTP sender (DLT template id from config) — needs account: STOP and ask
- [ ] P11.2 [BE] Razorpay live mode + Route linked accounts onboarding flow — needs account: STOP and ask
- [ ] P11.3 [BE] WhatsApp Cloud API sender for order updates (approved templates) — needs Meta verification: STOP and ask
- [ ] P11.4 [BE] File storage on Cloudflare R2 (or Azure Blob) with signed URLs — needs account: STOP and ask
- [ ] P11.5 [BE] Health checks (DB, storage), structured logs (Serilog sink), error tracking (Sentry or App Insights)
- [ ] P11.6 [BE] Deployment: Dockerfile, environment variables, migrations on deploy, daily MySQL backup + restore test, docs/DEPLOY.md
- [ ] P11.7 [BE] CI: build + tests on every push (GitHub Actions)
- [ ] P11.8 [BE] Security pass: rate limits, CORS, headers, secrets check, dependency vulnerabilities, OWASP top 10 checklist in docs/SECURITY_REVIEW.md
- [ ] P11.9 [BE] Load test checkout and search (k6), report p95 latency
- [ ] P11.10 [WEB] Production build, PWA icons from brand, error pages, analytics events (search, add to cart, checkout)

---

## Phase 12 — After launch (growth)

- [ ] P12.1 Coupons and offers (seller-funded and platform-funded)
- [ ] P12.2 Reviews and ratings UI (backend exists)
- [ ] P12.3 Wishlist, recently viewed, product compare (same product type across sellers)
- [ ] P12.4 Quick order: quantities for many products on one screen; reorder whole past order
- [ ] P12.5 Courier integration (Shiprocket / Delhivery): live rates, AWB, tracking webhooks
- [ ] P12.6 Regional languages: Hindi first, then Gujarati, Marathi, Tamil, Bengali, Telugu, Punjabi, Urdu (RTL)
- [ ] P12.7 Referral (retailer invites retailer)
- [ ] P12.8 Seller subscription plans (Basic free, Pro paid: more listings, featured slots)

---

## Phase 13 — Franchise network (later, after Phase 12.8)

- [ ] P13.1 Franchise role, territories (city / pincode list), one franchise per territory
- [ ] P13.2 Attribution: manufacturer linked to the franchise that onboarded it (first come wins)
- [ ] P13.3 Earnings ledger: share of subscription (and optional share of commission), only when manufacturer is approved and paid; clawback if it churns within 90 days
- [ ] P13.4 Payouts to franchise via Razorpay Route linked account
- [ ] P13.5 Franchise dashboard: onboarded sellers, their sales, earnings, payouts
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
