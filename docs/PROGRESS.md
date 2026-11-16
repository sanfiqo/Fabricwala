2026-11-12 | P1.1 | done | build passed; 0 tests discovered (test project had no tests yet)
2026-11-12 | P1.2 | done | build passed; 4 tests passed
2026-11-12 | P1.3 | done | build passed; 4 tests passed
2026-11-12 | P1.4 | done | build passed; 4 tests passed
2026-11-12 | P1.5 | done | build passed; 4 tests passed
2026-11-13 | P1.6 | done | build passed; 4 tests passed
2026-11-13 | P1.7 | done | build passed; 4 tests passed
2026-11-13 | P1.8 | done | build passed; 4 tests passed; migration/database apply not run (no configured local MySQL secret)
2026-11-13 | P1.9 | done | build passed; 6 tests passed; service flow and five-attempt lock covered without MySQL host
2026-11-13 | P1.10 | done | pending restore/build validation; database migration/apply skipped without MySQL secret
2026-11-13 | P2.1 | done | build passed; 6 tests passed
2026-11-13 | P2.2 | done | build passed; 6 tests passed
2026-11-13 | P2.3 | blocked | build passed; 6 tests passed; EF model added, migration tooling not configured
2026-11-13 | P2.4 | done | build passed; 6 tests passed
2026-11-13 | P2.5 | done | build passed; 6 tests passed
2026-11-13 | P2.6 | done | build passed; 6 tests passed
2026-11-13 | P2.7 | done | build passed; 6 tests passed
2026-11-13 | P2.8 | done | build passed; 6 tests passed
2026-11-13 | P2.9 | done | build passed; 6 tests passed
2026-11-13 | P2.10 | done | build passed; 8 tests passed
2026-11-13 | P3.1 | done | build passed; 8 tests passed
2026-11-13 | P3.2 | done | build passed; 8 tests passed
2026-11-13 | P3.3 | done | build passed; 8 tests passed
2026-11-13 | P3.4 | done | build passed; 9 tests passed
2026-11-13 | P3.6 | done | build passed; 9 tests passed
2026-11-13 | P3.7 | done | build passed; 9 tests passed
2026-11-13 | P3.5 | done | build passed; 10 tests passed
2026-11-13 | P3.8 | done | build passed; 10 tests passed
2026-11-13 | P4.1 | done | build passed; 10 tests passed
2026-11-13 | P4.3 | done | build passed; 11 tests passed
2026-11-13 | P4.8 | done | build passed; 11 tests passed
2026-11-13 | P5.1 | done | build passed; 11 tests passed
2026-11-13 | P5.2 | done | build passed; 12 tests passed
2026-11-13 | P4.7 | done | build passed; 12 tests passed

Handover: Phase 1 complete. Phase 2 complete except P2.3 migration tooling block. Phase 3 complete. Phase 4 has P4.1, P4.3, P4.7, P4.8; P4.2/P4.4/P4.5/P4.6 remain. Phase 5 has P5.1/P5.2; P5.9 is blocked for accounts and P5.3-P5.8 remain. Phases 6-7 remain. Untracked docs/ARCHITECTURE.md is preserved from earlier work.
2026-11-13 | P2.3 | blocked | migrations generated; DB update blocked by MySQL TLS credential authentication; 12 tests passed
2026-11-13 | P3.8 | done | split Identity/Businesses/Catalogue/Checkout/Payments tests; slab selection and price tamper coverage added; 14 tests passed
2026-11-13 | P4.2 | done | EF cart service/API with business isolation; AddCart migration generated; 15 tests passed
2026-11-13 | P4.4-P4.6 | done | EF retailer/seller order APIs, shipment status model and isolation tests; migration generated; 16 tests passed
2026-11-13 | Contract alignment | done | enum-string JSON, auth csrf/me contract, cart totals view, seller detail endpoints and architecture update; 16 tests passed
2026-11-13 | P5.3-P5.6 | done | payout, settlement, refund, invoice EF services and finance migration; 16 tests passed
2026-11-13 | P5.7-P5.8 | done | EF audit interceptor, auth/search rate-limit policies and frontend-origin CORS; 16 tests passed
2026-11-13 | P7.1-P7.6 | done | backend EF foundations and endpoints for RFQ, disputes, reviews, favourites, group buys and logistics; 17 tests passed
2026-11-13 | Config | done | Development-only appsettings.Local.json support; all EF contexts use ConnectionStrings:TextileConnect; 17 tests passed
2026-11-13 | P2.3 | done | Applied Identity and Marketplace migrations, repaired PlatformSettings table, seeded States=1/Cities=12/Categories=8/PlatformSettings=4/Roles=3; API health 200; 17 tests passed
2026-11-13 | Contract products | done | Public product paging/detail and seller routes now use EF and current-business isolation; build passed; 17 tests passed
2026-11-13 | Contract orders/checkout | partial | Added retailer order detail/confirm, seller accept/reject/dispatch aliases, EF checkout split and OrderItems migration; build and 17 tests passed; full MySQL checkout flow still pending product/admin setup
2026-11-13 | Contract payments | partial | Added authenticated payment-order endpoint with Development fake gateway and retailer-order isolation; build and 17 tests passed; webhook/admin settings/shipping/document DTOs remain
2026-11-13 | P1.6 | done | Added Development-only configurable Bootstrap:AdminPhones promotion; build passed; 17 tests passed
2026-11-13 | P2.3 | done | Dropped textileconnect_dev and reapplied all Identity/Marketplace migrations from zero; original PlatformSettings migration worked, repair migration removed; seeds logged States=1/Cities=12/Categories=8/PlatformSettings=4
2026-11-13 | Contract admin/docs | partial | Added EF-backed admin settings and shipping CRUD plus private business-document upload route; build passed; 17 tests passed; signed document download and exact frontend shipping IDs remain
2026-11-13 | P5.10 | done | Added payment webhook completion, private document download, validated shipping DTOs/city references, full-flow integration coverage; 63 tests passed
2026-11-13 | Clean MySQL verification | done | Dropped textileconnect_dev, applied both migration chains from zero, original PlatformSettings migration succeeded, seeds: States=1/Cities=12/Categories=8/PlatformSettings=4; API health 200
2026-11-13 | Real flow fixes | partial | Added environment-over-local precedence, approved-business claim, approval role assignment, and product variant/slab persistence; build passed; 63 tests passed; full flow still blocked by invalid local HMAC lengths
2026-11-14 | P-NIGHT B4.2 | done | landed-cost quote service; 119 tests passed
2026-11-14 | P-NIGHT B5 | done | cart tier pricing, GST fix, add-more savings; 129 tests passed
2026-11-14 | P-NIGHT B6 | done | idempotent checkout + payments + webhook; 137 tests passed
2026-11-14 | P-NIGHT B7 | done | retailer order groups, timeline, confirm delivery; 140 tests passed
2026-11-14 | P-NIGHT B8 | done | seller order tabs/actions, expiry + refund with fake clock; 146 tests passed
2026-11-14 | P-NIGHT B9 | done | seller products: list/filters, draft/publish, edit, toggle, admin hold; 150 tests passed
2026-11-14 | P-NIGHT B10 | done | payouts: hold, 7-day/delivery release, claim hold, summary + CSV; 155 tests passed
2026-11-14 | P-NIGHT B11 | done | Seller dashboard with real counts, 6-month chart, low stock and best sellers; 156 tests passed
2026-11-14 | P-NIGHT B12 | done | Notifications paged list, unread filter/count, read one, read all; 157 tests passed
2026-11-14 | P-NIGHT B13 | done | Problem reports with reason, pieces, 3 photos, payout hold and admin decision; 160 tests passed
2026-11-14 | P-NIGHT B14 | done | Account profile, language en/hi and address book CRUD; 163 tests passed
2026-11-14 | P-NIGHT B15 | done | Admin home stats for today, 7 and 30 days with decision counts; 164 tests passed
2026-11-14 | P-NIGHT C1-C3 | done | FullJourney + expired-acceptance refund + problem-report payout hold over HTTP; 167 tests passed
2026-11-14 | P-NIGHT D1 | done | OpenAPI export to docs/openapi.json with drift check; 168 tests passed
2026-11-14 | P-NIGHT D2 | done | Development-only design seed with demo businesses, products and users; 170 tests passed
2026-11-14 | P-NIGHT D3 | done | docs/RUN_LOCAL.md (secret names only); MySQL start and migrations not verified here
2026-11-14 | P-NIGHT D4 | done | Development-only CORS for the Vite dev server and LAN, strict outside Development; 189 tests passed
2026-11-14 | P-NIGHT E1 | done | Admin lists: orders, claims with photos, sellers, retailers, products, payouts, commission; 194 tests passed
2026-11-14 | P-NIGHT E2 | done | Saved products, order rating, invoice list, pincode check and reorder; 199 tests passed
2026-11-16 | FE-REQ 1-2 | done | GET /states and GET /cities?stateId= added; Development OTP IP limits raised to 200 per 15 min and 1000 per day (per-number 3 per 15 min and 10 per day stay)
2026-11-16 | FE-REQ 3 | done | Business OwnerName (signup, profile, admin review) with migration NightOwnerName; docType upload already existed as a query value, now documented and tested; 203 tests expected
2026-11-16 | FE-REQ 4 | done | Retry-After header (seconds) on every 429: OTP request uses the exact wait from the sliding windows, auth and search limiters use the limiter's reset time; tests added
