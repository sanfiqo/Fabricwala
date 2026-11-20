# 32 — Additional Features Backlog (ideas, not decisions)
Everything here is a **proposal**. Nothing is approved or built. Already specified elsewhere and therefore not repeated: swatch board, bulk estimate, samples, WhatsApp share, filters, auto-slide banner (`26`), mill profile and reviews (`27`), bulk CSV import/export and international trade (`28`), overseas buyers (`29`), admin buyer review (`30`), account flows (`31`), RFQ (`06`, `13`, `21`).

**Effort:** S = days, M = 1–3 weeks, L = a month or more (one developer with Codex). **Phase:** P1 = early after launch, P2 = next, P3 = later. **BE** = needs new backend work.

## Revenue model warning (read first)
RFQ award shares contacts with **no commission** (protected rule). Catalogue orders may carry commission, but buyers and mills can move deals off-platform. The owner should decide how FabricWala earns: mill subscriptions, featured listings, lead packs, verified-badge fees, or commission on cart orders. Several items below (marked 💰) support that.

## 1. Trust and safety
| Feature | Why | Effort | Phase |
|---|---|---|---|
| Dispute / claim flow (photos, reason, mill reply, admin decision) | Quality claims within 3 days are promised on profiles | M | P1 |
| Supplier trust score (response rate, on-time, claims, reviews) shown as text and number | Better ranking, fewer scams | M | P1 |
| GST and bank verification via provider API | Faster, cheaper than manual | M | P1 |
| Report supplier / report buyer with admin queue | Safety valve | S | P1 |
| Payment protection (hold money until delivery) | Biggest trust builder for new buyers; **regulated, needs legal and payment partner** | L | P3 |
| Physical factory audit badge (third party visit) 💰 | Premium trust tier | M | P3 |
| Anti-bypass monitoring (detect phone numbers in chat/notes) | Protect commission and revenue | M | P2 |

## 2. Buying experience
| Feature | Why | Effort | Phase |
|---|---|---|---|
| Reorder in one tap from past orders | Repeat B2B buying is the core habit | S | P1 |
| Price history chart and price-drop alerts (BE) | Buyers time their purchases | M | P1 |
| Compare fabrics side by side (price, MOQ, GSM, width, lead time) | Decision help | S | P1 |
| Counter-offer on quotes and negotiation thread | Real B2B behaviour | M | P2 |
| Landed cost calculator (price + freight + GST) | Removes surprise at checkout | M | P1 |
| Saved suppliers lists and private notes | Retailers manage many mills | S | P1 |
| "Looking for" requirement board (buyers post, mills respond) | Demand side marketplace | M | P2 |
| Purchase order (PO) PDF and GST tax invoice | Needed by businesses and accountants | M | P1 |
| Team accounts (owner, buyer, accountant roles) | Bigger retailers have staff | L | P2 |
| Credit terms (net 15/30) | Common in Indian textile trade; **financial regulation and risk** | L | P3 |
| Recurring / scheduled orders | Steady buyers | M | P3 |

## 3. Communication
| Feature | Why | Effort | Phase |
|---|---|---|---|
| In-app chat buyer ↔ mill with product cards and photo/voice notes | Keeps deals on the platform; WhatsApp is the competitor | L | P1 |
| WhatsApp Business notifications (order, quote, alert) via official API | Where Indian users actually read messages | M | P1 |
| Notification centre and email digest | Central place for alerts | M | P1 |
| Auto-translate chat (Hindi, Gujarati, English) | Cross-state trade | M | P3 |
| Call masking (call without sharing numbers) | Privacy and anti-bypass | M | P3 |
| Mill auto-reply and business hours / holiday mode | Sets expectations | S | P2 |

## 4. Search and discovery
| Feature | Why | Effort | Phase |
|---|---|---|---|
| Saved searches with alerts | Re-engagement | S | P1 |
| "Similar fabrics" and "more from this mill" | Browsing depth | S | P1 |
| Search by photo (camera button exists in UI; needs matching engine) | Hero feature of Alibaba-style apps | L | P2 |
| Colour match: pick a colour or upload a swatch, find close fabrics | Fabric-specific | L | P3 |
| Voice search in Hindi | Accessibility for many users | M | P2 |
| AI assistant: describe the fabric in Hinglish, get matches and RFQ drafted | Differentiator | L | P3 |
| Public SEO pages for categories, cities and mills | Free traffic from Google | M | P1 |

## 5. Mill / manufacturer tools
| Feature | Why | Effort | Phase |
|---|---|---|---|
| Mill dashboard analytics (views, enquiries, quotes, conversion) | Shows value, drives subscriptions 💰 | M | P1 |
| Lead inbox (CRM-lite: status, notes, follow-up reminders) | Mills lose leads in WhatsApp | M | P1 |
| Quotation builder with saved templates | Faster, more consistent quotes | M | P1 |
| Stock / ready-stock management with low-stock alerts | Ready stock is a key tab | M | P2 |
| Bulk price update (CSV) | Rates change often | S | P1 |
| Catalogue PDF and showroom link / QR code | Mills share with their own customers | S | P1 |
| Featured listing, boosts and ads 💰 | Core revenue line | M | P2 |
| Subscription plans (Free / Pro / Premium) 💰 | Recurring revenue | M | P2 |
| Team members and roles for the mill | Staff handle enquiries | M | P2 |
| Seasonal collections and new-arrival announcements to followers | Uses the follow feature | S | P2 |

## 6. Logistics
| Feature | Why | Effort | Phase |
|---|---|---|---|
| Courier / transporter integration (rates, booking, tracking) | Pincode estimate becomes real | L | P2 |
| LR (lorry receipt) upload and tracking link | Textile freight runs on LR | S | P1 |
| E-way bill helper | Required above value limits | M | P2 |
| Packing list and shipment documents | Needed for dispatch | M | P2 |
| Transit insurance option | Reduces claim friction | M | P3 |
| Third-party QC inspection booking | Trust for large orders 💰 | M | P3 |

## 7. Payments and finance
| Feature | Why | Effort | Phase |
|---|---|---|---|
| Payment links and milestone payments (advance, balance) | Matches "30% advance" terms | M | P2 |
| Ledger / khata between buyer and mill | How Indian traders think | M | P2 |
| GST reports, TDS/TCS handling, auto invoices | Accountant-ready | L | P2 |
| Refunds and credit notes | Needed once disputes exist | M | P2 |
| Multi-currency display for overseas (USD, AED) | Overseas buyers | M | P3 |
| Export documents (proforma invoice, commercial invoice, packing list) | Exporters need them | M | P3 |
| Payout schedule and statements for mills | Trust for mills | M | P1 |

## 8. Growth and community
| Feature | Why | Effort | Phase |
|---|---|---|---|
| Referral programme (mill invites mill, retailer invites retailer) | Cheapest growth | M | P1 |
| Verified badge tiers (Basic, Gold) 💰 | Monetise trust | S | P2 |
| Market price index (yarn, cotton rates) and trend reports | Daily reasons to open the app | M | P2 |
| Fabric glossary (GSM, count, weave, finish) | Educates new buyers, SEO | S | P1 |
| Broker / agent module (agents earn on introductions) | Matches the broker-hub idea; needs commission rules | L | P3 |
| Events and trade-fair listings | Community | S | P3 |
| Loyalty points for buyers | Retention | M | P3 |

## 9. Intelligence and automation
| Feature | Why | Effort | Phase |
|---|---|---|---|
| AI quote comparison summary ("cheapest, fastest, best rated") | Faster decisions | M | P2 |
| Auto-tag product photos (colour, pattern, fabric type) | Cleaner catalogue, better search | L | P3 |
| Duplicate product and price-anomaly detection | Catalogue quality | M | P2 |
| Smart RFQ matching (rank mills by capacity, category, history) | Better quotes | M | P2 |
| Demand insights for mills ("what buyers are searching") 💰 | Premium analytics | M | P3 |

## 10. Admin and platform
| Feature | Why | Effort | Phase |
|---|---|---|---|
| CMS for home banners (manage the auto-slide, schedule, order) | Owner changes banners without code | M | P1 |
| Support tickets / helpdesk with FAQ | Needed from day one | M | P1 |
| Dispute console and refund tools | Pairs with disputes | M | P1 |
| Feature-flag dashboard | Safe rollouts | S | P1 |
| Moderation tools (reviews, photos, reports) | Quality | M | P1 |
| Audit log viewer and exports | Compliance | S | P1 |
| Bulk messages (SMS/WhatsApp/email) with templates and consent | Announcements | M | P2 |
| Analytics for the owner (signups, GMV, RFQ conversion, SLA) | Run the business | M | P1 |

## 11. App and UX
| Feature | Why | Effort | Phase |
|---|---|---|---|
| Push notifications (PWA now, React Native later) | Alerts | M | P1 |
| Low-data mode and offline cache of viewed items | Patchy networks in mill towns | M | P2 |
| More languages: Gujarati, Marathi, Tamil, Bengali | Mills and buyers across India | M | P2 |
| App lock with biometrics | Business data on phones | S | P3 |
| Desktop / tablet layouts for admin and bulk work | Mills use PCs for catalogues | M | P2 |
| React Native app (already planned) | Store presence | L | P2 |

## 12. Compliance
| Feature | Why | Effort | Phase |
|---|---|---|---|
| DPDP Act consent, data download and delete requests | Indian law | M | P1 |
| GDPR requests for EU/UK buyers | Needed once overseas buyers sign up | M | P1 |
| Terms and policy versioning with re-consent | Legal hygiene | S | P1 |
| Sanctions screening and blocked-country config | Required for overseas | S | P1 |
| KYC/AML checks | Only if you hold or move money | L | P3 |

## Recommended order (my suggestion)
**First 10 after launch:** 1) dispute/claim flow, 2) in-app chat plus WhatsApp notifications, 3) PO and GST invoice, 4) mill lead inbox and quotation builder, 5) price alerts and saved searches, 6) mill analytics dashboard, 7) support tickets, 8) CMS for banners, 9) public SEO pages, 10) referral programme. Then subscriptions and featured listings so the platform earns.
**Avoid early:** payment protection, credit terms, KYC/AML, AI image search. They are large, regulated, or need data you do not have yet.

## How to use this file
Pick items, give each an ID (BL-01…), write acceptance rules like in `22`, and run each behind a feature flag. Do not touch cart, checkout, payment, payout, OTP or RFQ award logic without an approved task.
