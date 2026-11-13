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
- [ ] P10.4 [BE] Landed cost uses retailer pincode → zone; "shipping on request" when no rate
- [ ] P10.5 [WEB] Pincode/city picker on home; landed price everywhere uses it

### Retailer
- [ ] P10.6 [BE] Search: full-text on title/description/material, filters (category, price range, MOQ, seller city, material, size, colour), sort (popular, price, newest), paging
- [ ] P10.7 [WEB] Search screen with filter bottom sheet and sort
- [ ] P10.8 [BE] Address book: multiple delivery addresses, default address
- [ ] P10.9 [BE] Invoices: GST-compliant PDF per SellerOrder (seller → retailer) and platform commission invoice (platform → seller); download endpoint
- [ ] P10.10 [WEB] Invoice download on order detail
- [ ] P10.11 [BE] Return / damage claim flow on delivered SellerOrder: reason, photos, seller response, admin decision, partial refund
- [ ] P10.12 [WEB] Report a problem screen + claim status
- [ ] P10.13 [BE] Notification center: list, unread count, mark read (in-app channel)
- [ ] P10.14 [WEB] Notification center screen + bell badge
- [ ] P10.15 [BE] Seller storefront endpoint: business profile, banner, about, city, rating, products
- [ ] P10.16 [WEB] Storefront screen

### Manufacturer
- [ ] P10.17 [BE] Seller dashboard API: new orders, pending dispatch, sales this month, top products, payout due, low stock
- [ ] P10.18 [WEB] Seller dashboard screen
- [ ] P10.19 [BE] Bulk product upload: CSV/Excel template download, validate rows, preview errors, import; bulk image upload matched by SKU
- [ ] P10.20 [WEB] Bulk upload screen with error preview
- [ ] P10.21 [BE] Stock alerts (low stock, out of stock) + duplicate product
- [ ] P10.22 [BE] Payout statement: per SellerOrder amount, commission, settled date; CSV export
- [ ] P10.23 [WEB] Payouts screen

### Admin
- [ ] P10.24 [BE] Admin dashboard API: GMV, orders, commission earned, new businesses, pending approvals (today / 7 days / 30 days)
- [ ] P10.25 [BE] Reports export CSV: orders, payments, settlements, businesses
- [ ] P10.26 [BE] Product moderation: list, flag, deactivate with reason; seller notified
- [ ] P10.27 [BE] Categories admin (tree, reorder) and home banners admin (image, link, start/end date)
- [ ] P10.28 [BE] Suspend / reactivate business and user; suspended cannot log in or order
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
