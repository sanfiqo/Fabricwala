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
- [ ] P2.3 BLOCKED: migrations are generated, but local MySQL update fails during TLS credential authentication
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
- [ ] P7.1 Enquiries + Quotations (RFQ)
- [ ] P7.2 Disputes with evidence upload
- [ ] P7.3 Reviews and ratings
- [ ] P7.4 Favourites and one-click reorder
- [ ] P7.5 Collective buying (GroupBuys)
- [ ] P7.6 Logistics partner role and dashboard

## Open decisions (ask before building)
- Who pays commission in MVP: manufacturer only (recommended) or shared with retailer?
- Settlement release: after delivery confirm, or N days (what N)?
- Brand name and domain.
