# 33 — Feature Map (master index linking every feature to its spec, screen and flow)
**One file to find everything.** Each feature has an ID, its status, the document that defines it, the working reference screen (open in a browser), and the flow Seq numbers from `31-END-TO-END-USER-FLOW.md`. Backlog IDs (`BL-x.y`) are generated from `32-ADDITIONAL-FEATURES-BACKLOG.md`, so numbers match that file. Put this file in the same folder as the other docs so the relative links work.

**Status words:** `Reference built` = clickable HTML reference exists (placeholders and demo data, not the real app). `Spec` = written rules, no screen yet. `Verify` = described in V2.1 docs as existing; Codex must check the repo. `Idea` = backlog proposal, not approved. `Protected` = do not change logic without owner approval.
**Nothing below is implemented in the real app until Codex builds it and tests pass.**

---
## 1. Core product features (specified or designed)
### 1.1 Account, login and sessions → flow doc [31](31-END-TO-END-USER-FLOW.md)
| ID | Feature | Status | Spec | Reference screen | Flow Seq |
|---|---|---|---|---|---|
| FM-101 | Role choice and mobile registration (Retailer, Manufacturer) | Verify | [31](31-END-TO-END-USER-FLOW.md) | — | A-01…A-07 |
| FM-102 | Mobile OTP login, return-to-page, lockout | Verify, Protected | [31](31-END-TO-END-USER-FLOW.md), [10](10-AUTH-FORMS-MODALS-AND-UI-STATES.md) | — | L-01…L-06 |
| FM-103 | Lost access (OTP not received, number changed). Replaces "forgot password" | Spec | [31](31-END-TO-END-USER-FLOW.md) | — | X-01…X-06 |
| FM-104 | Sessions, devices, sign out, change number, delete account | Spec | [31](31-END-TO-END-USER-FLOW.md) | — | S-01…S-05 |

### 1.2 Home, search and discovery → [26](26-HOME-SCREEN-FEATURES-SPEC.md)
| ID | Feature | Status | Spec | Reference screen | Flow Seq |
|---|---|---|---|---|---|
| FM-201 | Home layout: header, search, tabs, quick filters | Reference built | [26](26-HOME-SCREEN-FEATURES-SPEC.md), [25](25-BW-SOURCING-UI-SPEC.md) | [home-screen.html](css/home-screen.html) | F-01 |
| FM-202 | Auto-slide banner (pause, reduced motion, 4 s) | Reference built | [26](26-HOME-SCREEN-FEATURES-SPEC.md) | [home-screen.html](css/home-screen.html) | F-01 |
| FM-203 | Filters sheet (width, GSM, composition, colour, unit) with live count | Reference built | [26](26-HOME-SCREEN-FEATURES-SPEC.md) | [home-screen.html](css/home-screen.html) | F-01 |
| FM-204 | Shop by use (saree, kurti, shirting, suiting, upholstery, lehenga) | Reference built | [26](26-HOME-SCREEN-FEATURES-SPEC.md) | [home-screen.html](css/home-screen.html) | F-01 |
| FM-205 | Swatch board with price alerts and WhatsApp share | Reference built, needs backend | [26](26-HOME-SCREEN-FEATURES-SPEC.md) | [home-screen.html](css/home-screen.html) | B-06 |
| FM-206 | Bulk estimate (tier price, total, next-tier hint, pincode delivery) | Reference built, needs backend | [26](26-HOME-SCREEN-FEATURES-SPEC.md) | [home-screen.html](css/home-screen.html) | F-02 |
| FM-207 | Order sample button | Reference built, needs backend | [26](26-HOME-SCREEN-FEATURES-SPEC.md) | [home-screen.html](css/home-screen.html) | F-02 |
| FM-208 | Hindi / English toggle (key strings only) | Reference built | [26](26-HOME-SCREEN-FEATURES-SPEC.md) | [home-screen.html](css/home-screen.html) | B-06 |
| FM-209 | Order tracking strip and active RFQ card (buyer) | Reference built | [26](26-HOME-SCREEN-FEATURES-SPEC.md) | [home-screen.html](css/home-screen.html) | F-07, F-12 |
| FM-210 | Results list and product detail with tier price table | Reference built | [25](25-BW-SOURCING-UI-SPEC.md) | [reference-screens.html](css/reference-screens.html) | F-01, F-02 |

### 1.3 Mills and manufacturers → [27](27-SUPPLIER-PROFILE-SPEC.md)
| ID | Feature | Status | Spec | Reference screen | Flow Seq |
|---|---|---|---|---|---|
| FM-301 | Mill profile: identity, key numbers, sticky tabs | Reference built | [27](27-SUPPLIER-PROFILE-SPEC.md) | [supplier-profile.html](css/supplier-profile.html) | F-03 |
| FM-302 | Verification checklist (GST, address, factory video, certificates, IEC) | Reference built | [27](27-SUPPLIER-PROFILE-SPEC.md) | [supplier-profile.html](css/supplier-profile.html) | C-04…C-08 |
| FM-303 | Reviews from completed orders, mill reply | Reference built, needs backend | [27](27-SUPPLIER-PROFILE-SPEC.md) | [supplier-profile.html](css/supplier-profile.html) | F-14 |
| FM-304 | Contact gating (masked phone/address, reveal when signed in) | Reference built | [27](27-SUPPLIER-PROFILE-SPEC.md) | [supplier-profile.html](css/supplier-profile.html) | F-03 |
| FM-305 | Request quote to one mill (sheet) | Reference built | [27](27-SUPPLIER-PROFILE-SPEC.md), [21](21-API-CONTRACT-RFQ-AND-COMMERCE.md) | [supplier-profile.html](css/supplier-profile.html) | F-10 |
| FM-306 | Follow mill, similar mills, report supplier | Reference built, needs backend | [27](27-SUPPLIER-PROFILE-SPEC.md) | [supplier-profile.html](css/supplier-profile.html) | F-03 |
| FM-307 | Mill onboarding: profile, documents, bank, factory proof, submit | Spec | [31](31-END-TO-END-USER-FLOW.md) | — | C-01…C-12 |
| FM-308 | Profile strength meter for mills | Spec | [27](27-SUPPLIER-PROFILE-SPEC.md) | — | C-01 |

### 1.4 Retailer profile → [31](31-END-TO-END-USER-FLOW.md)
| ID | Feature | Status | Spec | Reference screen | Flow Seq |
|---|---|---|---|---|---|
| FM-401 | Profile, addresses, GSTIN, documents, verification status | Spec | [31](31-END-TO-END-USER-FLOW.md) | — | B-01…B-05 |
| FM-402 | Preferences, alerts, orders, RFQs, saved mills | Spec | [31](31-END-TO-END-USER-FLOW.md) | — | B-06…B-08 |

### 1.5 Trade flow (cart, checkout, order, shipment, payout) — Protected
| ID | Feature | Status | Spec | Reference screen | Flow Seq |
|---|---|---|---|---|---|
| FM-501 | Catalogue to cart to checkout to order to shipment to payout | Verify, Protected | [18](18-EXISTING-REPOSITORY-ROUTE-AND-GAP-AUDIT.md), [19](19-SOURCE-OF-TRUTH-AND-DECISIONS.md), [21](21-API-CONTRACT-RFQ-AND-COMMERCE.md) | — | F-04…F-09 |
| FM-502 | RFQ post, match, quote, compare, award (contacts, no commission) | Verify, Protected | [06](06-BUYER-SUPPLIER-RFQ-FLOWS.md), [13](13-RFQ-QUOTATION-MATCHING-AND-PERMISSIONS.md), [21](21-API-CONTRACT-RFQ-AND-COMMERCE.md) | — | F-10…F-13 |

### 1.6 Import / export and international trade → [28](28-IMPORT-EXPORT-MODULE-SPEC.md), [29](29-OVERSEAS-BUYER-SIGNUP-SPEC.md)
| ID | Feature | Status | Spec | Reference screen | Flow Seq |
|---|---|---|---|---|---|
| FM-601 | Bulk product import by CSV (template, row validation, publish, log) | Reference built | [28](28-IMPORT-EXPORT-MODULE-SPEC.md) | [import-export.html](css/import-export.html) | C-12 |
| FM-602 | Data export (products, orders, quotes, payouts) with CSV-injection guard | Reference built | [28](28-IMPORT-EXPORT-MODULE-SPEC.md) | [import-export.html](css/import-export.html) | — |
| FM-603 | Mill export profile: countries, ports, Incoterms, IEC, "Export ready" badge | Reference built | [28](28-IMPORT-EXPORT-MODULE-SPEC.md) | [supplier-profile.html](css/supplier-profile.html) | C-08 |
| FM-604 | RFQ for "Outside India" (country, Incoterm, currency, port) | Spec | [28](28-IMPORT-EXPORT-MODULE-SPEC.md) | — | F-10 |
| FM-605 | Overseas buyer signup with email OTP and review status | Reference built | [29](29-OVERSEAS-BUYER-SIGNUP-SPEC.md) | [overseas-signup.html](css/overseas-signup.html) | D-01…D-05 |
| FM-606 | Imported-fabric tags and HS codes | Spec (later phase) | [28](28-IMPORT-EXPORT-MODULE-SPEC.md) | — | — |
| FM-607 | Export globe (hero on overseas signup, Export block on mill profile) | Reference built | [34](34-EXPORT-GLOBE-COMPONENT.md) | [export-globe.html](css/export-globe.html), [overseas-signup.html](css/overseas-signup.html) | D-01, C-08 |

### 1.7 Admin → [30](30-ADMIN-BUYER-REVIEW-SPEC.md)
| ID | Feature | Status | Spec | Reference screen | Flow Seq |
|---|---|---|---|---|---|
| FM-701 | Overseas buyer review queue (SLA, flags, approve, info, reject, audit log) | Reference built | [30](30-ADMIN-BUYER-REVIEW-SPEC.md) | [admin-buyer-review.html](css/admin-buyer-review.html) | E-03 |
| FM-702 | Mill verification queue | Spec | [31](31-END-TO-END-USER-FLOW.md) | — | E-02 |
| FM-703 | Retailer verification queue (required: every Business must be Approved) | Spec | [31](31-END-TO-END-USER-FLOW.md) | — | E-04 |
| FM-704 | Lost-access cases | Spec | [31](31-END-TO-END-USER-FLOW.md) | — | E-05 |
| FM-705 | Reports and moderation | Idea | [32](32-ADDITIONAL-FEATURES-BACKLOG.md) | — | E-06 |

### 1.8 Design system
| ID | Feature | Status | Spec | Reference |
|---|---|---|---|---|
| FM-801 | Black-and-white sourcing UI tokens and components | Reference built | [25](25-BW-SOURCING-UI-SPEC.md), [08](08-DESIGN-TOKENS-AND-THEME-CONTRACT.md) | [fabricwala-bw.css](css/fabricwala-bw.css), [check-bw.py](css/check-bw.py) |

---
## 2. Backlog features (ideas from [32](32-ADDITIONAL-FEATURES-BACKLOG.md))
Top-10 stars: ★1 dispute flow · ★2 chat and WhatsApp notifications · ★3 PO and GST invoice · ★4 lead inbox and quotation builder · ★5 price alerts and saved searches · ★6 mill analytics · ★7 support tickets · ★8 banner CMS · ★9 SEO pages · ★10 referral programme.

### Group 1 — Trust and safety
| ID | Feature | Effort | Phase | Top-10 | Status |
|---|---|---|---|---|---|
| BL-1.1 | Dispute / claim flow (photos, reason, mill reply, admin decision) | M | P1 | ★1 | Idea |
| BL-1.2 | Supplier trust score (response rate, on-time, claims, reviews) shown as text and number | M | P1 |  | Idea |
| BL-1.3 | GST and bank verification via provider API | M | P1 |  | Idea |
| BL-1.4 | Report supplier / report buyer with admin queue | S | P1 |  | Idea |
| BL-1.5 | Payment protection (hold money until delivery) | L | P3 |  | Idea |
| BL-1.6 | Physical factory audit badge (third party visit) 💰 | M | P3 |  | Idea |
| BL-1.7 | Anti-bypass monitoring (detect phone numbers in chat/notes) | M | P2 |  | Idea |

### Group 2 — Buying experience
| ID | Feature | Effort | Phase | Top-10 | Status |
|---|---|---|---|---|---|
| BL-2.1 | Reorder in one tap from past orders | S | P1 |  | Idea |
| BL-2.2 | Price history chart and price-drop alerts (BE) | M | P1 | ★5 | Idea |
| BL-2.3 | Compare fabrics side by side (price, MOQ, GSM, width, lead time) | S | P1 |  | Idea |
| BL-2.4 | Counter-offer on quotes and negotiation thread | M | P2 |  | Idea |
| BL-2.5 | Landed cost calculator (price + freight + GST) | M | P1 |  | Idea |
| BL-2.6 | Saved suppliers lists and private notes | S | P1 |  | Idea |
| BL-2.7 | "Looking for" requirement board (buyers post, mills respond) | M | P2 |  | Idea |
| BL-2.8 | Purchase order (PO) PDF and GST tax invoice | M | P1 | ★3 | Idea |
| BL-2.9 | Team accounts (owner, buyer, accountant roles) | L | P2 |  | Idea |
| BL-2.10 | Credit terms (net 15/30) | L | P3 |  | Idea |
| BL-2.11 | Recurring / scheduled orders | M | P3 |  | Idea |

### Group 3 — Communication
| ID | Feature | Effort | Phase | Top-10 | Status |
|---|---|---|---|---|---|
| BL-3.1 | In-app chat buyer ↔ mill with product cards and photo/voice notes | L | P1 | ★2 | Idea |
| BL-3.2 | WhatsApp Business notifications (order, quote, alert) via official API | M | P1 | ★2 | Idea |
| BL-3.3 | Notification centre and email digest | M | P1 |  | Idea |
| BL-3.4 | Auto-translate chat (Hindi, Gujarati, English) | M | P3 |  | Idea |
| BL-3.5 | Call masking (call without sharing numbers) | M | P3 |  | Idea |
| BL-3.6 | Mill auto-reply and business hours / holiday mode | S | P2 |  | Idea |

### Group 4 — Search and discovery
| ID | Feature | Effort | Phase | Top-10 | Status |
|---|---|---|---|---|---|
| BL-4.1 | Saved searches with alerts | S | P1 | ★5 | Idea |
| BL-4.2 | "Similar fabrics" and "more from this mill" | S | P1 |  | Idea |
| BL-4.3 | Search by photo (camera button exists in UI; needs matching engine) | L | P2 |  | Idea |
| BL-4.4 | Colour match: pick a colour or upload a swatch, find close fabrics | L | P3 |  | Idea |
| BL-4.5 | Voice search in Hindi | M | P2 |  | Idea |
| BL-4.6 | AI assistant: describe the fabric in Hinglish, get matches and RFQ drafted | L | P3 |  | Idea |
| BL-4.7 | Public SEO pages for categories, cities and mills | M | P1 | ★9 | Idea |

### Group 5 — Mill / manufacturer tools
| ID | Feature | Effort | Phase | Top-10 | Status |
|---|---|---|---|---|---|
| BL-5.1 | Mill dashboard analytics (views, enquiries, quotes, conversion) | M | P1 | ★6 | Idea |
| BL-5.2 | Lead inbox (CRM-lite: status, notes, follow-up reminders) | M | P1 | ★4 | Idea |
| BL-5.3 | Quotation builder with saved templates | M | P1 | ★4 | Idea |
| BL-5.4 | Stock / ready-stock management with low-stock alerts | M | P2 |  | Idea |
| BL-5.5 | Bulk price update (CSV) | S | P1 |  | Idea |
| BL-5.6 | Catalogue PDF and showroom link / QR code | S | P1 |  | Idea |
| BL-5.7 | Subscription plans (Free / Pro / Premium) 💰 | M | P2 |  | Idea |
| BL-5.8 | Team members and roles for the mill | M | P2 |  | Idea |
| BL-5.9 | Seasonal collections and new-arrival announcements to followers | S | P2 |  | Idea |

### Group 6 — Logistics
| ID | Feature | Effort | Phase | Top-10 | Status |
|---|---|---|---|---|---|
| BL-6.1 | Courier / transporter integration (rates, booking, tracking) | L | P2 |  | Idea |
| BL-6.2 | LR (lorry receipt) upload and tracking link | S | P1 |  | Idea |
| BL-6.3 | E-way bill helper | M | P2 |  | Idea |
| BL-6.4 | Packing list and shipment documents | M | P2 |  | Idea |
| BL-6.5 | Transit insurance option | M | P3 |  | Idea |
| BL-6.6 | Third-party QC inspection booking | M | P3 |  | Idea |

### Group 7 — Payments and finance
| ID | Feature | Effort | Phase | Top-10 | Status |
|---|---|---|---|---|---|
| BL-7.1 | Payment links and milestone payments (advance, balance) | M | P2 |  | Idea |
| BL-7.2 | Ledger / khata between buyer and mill | M | P2 |  | Idea |
| BL-7.3 | GST reports, TDS/TCS handling, auto invoices | L | P2 |  | Idea |
| BL-7.4 | Refunds and credit notes | M | P2 |  | Idea |
| BL-7.5 | Multi-currency display for overseas (USD, AED) | M | P3 |  | Idea |
| BL-7.6 | Export documents (proforma invoice, commercial invoice, packing list) | M | P3 |  | Idea |
| BL-7.7 | Payout schedule and statements for mills | M | P1 |  | Idea |

### Group 8 — Growth and community
| ID | Feature | Effort | Phase | Top-10 | Status |
|---|---|---|---|---|---|
| BL-8.1 | Referral programme (mill invites mill, retailer invites retailer) | M | P1 | ★10 | Idea |
| BL-8.2 | Verified badge tiers (Basic, Gold) 💰 | S | P2 |  | Idea |
| BL-8.3 | Market price index (yarn, cotton rates) and trend reports | M | P2 |  | Idea |
| BL-8.4 | Fabric glossary (GSM, count, weave, finish) | S | P1 |  | Idea |
| BL-8.5 | Broker / agent module (agents earn on introductions) | L | P3 |  | Idea |
| BL-8.6 | Events and trade-fair listings | S | P3 |  | Idea |
| BL-8.7 | Loyalty points for buyers | M | P3 |  | Idea |

### Group 9 — Intelligence and automation
| ID | Feature | Effort | Phase | Top-10 | Status |
|---|---|---|---|---|---|
| BL-9.1 | AI quote comparison summary ("cheapest, fastest, best rated") | M | P2 |  | Idea |
| BL-9.2 | Auto-tag product photos (colour, pattern, fabric type) | L | P3 |  | Idea |
| BL-9.3 | Duplicate product and price-anomaly detection | M | P2 |  | Idea |
| BL-9.4 | Smart RFQ matching (rank mills by capacity, category, history) | M | P2 |  | Idea |
| BL-9.5 | Demand insights for mills ("what buyers are searching") 💰 | M | P3 |  | Idea |

### Group 10 — Admin and platform
| ID | Feature | Effort | Phase | Top-10 | Status |
|---|---|---|---|---|---|
| BL-10.1 | CMS for home banners (manage the auto-slide, schedule, order) | M | P1 | ★8 | Idea |
| BL-10.2 | Support tickets / helpdesk with FAQ | M | P1 | ★7 | Idea |
| BL-10.3 | Dispute console and refund tools | M | P1 | ★1 | Idea |
| BL-10.4 | Moderation tools (reviews, photos, reports) | M | P1 |  | Idea |
| BL-10.5 | Audit log viewer and exports | S | P1 |  | Idea |
| BL-10.6 | Bulk messages (SMS/WhatsApp/email) with templates and consent | M | P2 |  | Idea |
| BL-10.7 | Analytics for the owner (signups, GMV, RFQ conversion, SLA) | M | P1 |  | Idea |

### Group 11 — App and UX
| ID | Feature | Effort | Phase | Top-10 | Status |
|---|---|---|---|---|---|
| BL-11.1 | Push notifications (PWA now, React Native later) | M | P1 |  | Idea |
| BL-11.2 | Low-data mode and offline cache of viewed items | M | P2 |  | Idea |
| BL-11.3 | More languages: Gujarati, Marathi, Tamil, Bengali | M | P2 |  | Idea |
| BL-11.4 | App lock with biometrics | S | P3 |  | Idea |
| BL-11.5 | Desktop / tablet layouts for admin and bulk work | M | P2 |  | Idea |
| BL-11.6 | React Native app (already planned) | L | P2 |  | Idea |

### Group 12 — Compliance
| ID | Feature | Effort | Phase | Top-10 | Status |
|---|---|---|---|---|---|
| BL-12.1 | DPDP Act consent, data download and delete requests | M | P1 |  | Idea |
| BL-12.2 | GDPR requests for EU/UK buyers | M | P1 |  | Idea |
| BL-12.3 | Terms and policy versioning with re-consent | S | P1 |  | Idea |
| BL-12.4 | Sanctions screening and blocked-country config | S | P1 |  | Idea |
| BL-12.5 | KYC/AML checks | L | P3 |  | Idea |

---
## 3. Connections between features (what depends on what)
| Feature | Needs first |
|---|---|
| Reviews (FM-303) | Completed orders (FM-501) |
| Swatch board alerts (FM-205), saved searches, price alerts | Notification centre and push or WhatsApp provider (BL-3.2, BL-3.3) |
| In-app chat | Notification centre; moderation tools; anti-bypass rules |
| Disputes and refunds | Order and payout rules (FM-501); admin dispute console |
| Overseas RFQ (FM-604) | Overseas buyers approved (FM-605, FM-701) and mills with IEC verified (FM-603) |
| Mill publishing (FM-307, FM-601) | Mill verification queue (FM-702) |
| Sample orders (FM-207) | Sample price and policy decision; courier option |
| Bulk estimate delivery days (FM-206) | Courier or transporter integration (BL-6.1) |
| Banner CMS | Auto-slide banner (FM-202) |
| Subscriptions, featured listings | Mill analytics (BL-5.1) and a decided revenue model |
| GST reports, auto invoices | PO and GST invoice (BL-2.8) |

## 4. How to keep this map correct
1. When a feature becomes a task, copy its ID into the ticket and the Codex prompt ("implement FM-206").
2. When built and tested, change Status to `Built (tested)` and add the pull request link in a new column.
3. When a backlog item is approved, change `Idea` to `Approved`, give it acceptance rules like `22`, and keep its BL ID.
4. Never delete an ID. If a feature is dropped, mark it `Dropped` with a reason.
5. Owner decisions that block many features are listed at the end of [31](31-END-TO-END-USER-FLOW.md) and in [28](28-IMPORT-EXPORT-MODULE-SPEC.md) and [29](29-OVERSEAS-BUYER-SIGNUP-SPEC.md).
