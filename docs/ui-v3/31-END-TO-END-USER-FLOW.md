# 31 — End-to-End User Flow (Correct Sequence)
**Purpose:** one numbered source for every journey: register, login, OTP, lost access (the "forgot password" case), profile, documents, mill/manufacturer onboarding, overseas buyers, admin review, and the trade flow. Every step has a sequence number. Use these numbers in tickets, tests and Codex prompts ("implement A-04").

**Status words:** `EXISTS?` = described in V2.1 docs as existing, **Codex must verify in the repo**. `NEW` = proposed, not built. `PROTECTED` = do not change logic without owner approval.
**Roles:** Guest, Retailer (buyer), Manufacturer (mill / maker), Overseas buyer, Admin. Admin can never be chosen at signup.
**Visual/spec references:** screens in `25`–`30`, RFQ in `06`, `13`, `21`, auth UI states in `10`.

---
## 0. Key decision: there is no password
FabricWala login is **mobile OTP + HttpOnly cookie + CSRF** (`PROTECTED`). Overseas buyers use the same pattern with **email OTP** (`29`). With no password, a "Forgot password" screen does not exist and must not be added. The real problems are: *OTP did not arrive* and *I changed my number / lost my SIM*. Flow **A-LOST** below handles both. (If the owner later wants passwords, that is a separate decision and a security review.)

---
## Master sequence (what a user meets, in order)
| Seq | Screen / step | Who | Flow |
|---|---|---|---|
| A-01 | Landing / Home (guest browsing allowed) | Guest | A |
| A-02 | Choose: Sign in / Create account | Guest | A |
| A-03 | Create account: pick role | Guest | A |
| A-04 | Enter mobile number | Retailer, Manufacturer | A |
| A-05 | Enter OTP | Retailer, Manufacturer | A |
| A-06 | Basic details (name, business name, city) | Retailer, Manufacturer | A |
| A-07 | Land in role home | Retailer, Manufacturer | A |
| L-01…L-06 | Sign in (returning user) | All domestic | Login |
| X-01…X-06 | Lost access (OTP not received / number changed) | All | Lost access |
| B-01…B-08 | Retailer profile and verification (required: every Business must be Approved) | Retailer | B |
| C-01…C-12 | Mill / manufacturer onboarding, documents, review, listing | Manufacturer | C |
| D-01…D-05 | Overseas buyer signup and review | Overseas buyer | D |
| E-01…E-06 | Admin reviews (mills, overseas buyers, lost access) | Admin | E |
| F-01…F-14 | Trade flow: browse → cart → order → shipment → payout; RFQ | Buyer, Manufacturer | F |
| S-01…S-05 | Session, settings, sign out, delete account | All | S |

---
## A. Registration (domestic: Retailer and Manufacturer) — `EXISTS?` verify, then align
| Seq | Step | Rules / outcome |
|---|---|---|
| A-01 | Guest opens app. Can browse home, fabrics, mills (names, city, badges), product pages and prices. | No login needed to browse. Price slabs visible. Contact details, RFQ, cart checkout, samples need login. |
| A-02 | Guest taps a gated action (cart checkout, Request quote, Contact, Save). | Show "Sign in to continue". After login return to the same screen (return URL, validated to be an internal path only). |
| A-03 | "Create account": choose **I buy fabric (Retailer)** or **I make / sell fabric (Manufacturer)**. | Role is stored on the server. Cannot be changed later by the user; change = support request. Admin role is never offered. |
| A-04 | Enter **mobile number** (+91, 10 digits, starts 6–9). Consent: Terms and Privacy. | Server validates; rate-limit per IP and per number. Same response whether the number is already registered or not (no enumeration); if registered, the OTP message tells them to sign in. |
| A-05 | Enter **6-digit OTP**. | 10-minute expiry, single use, max 5 attempts, resend after 30 s, max 5 sends per hour per number. Success sets HttpOnly cookie session and CSRF token. `PROTECTED` |
| A-06 | **Basic details**: full name, business / shop name, city, state. Manufacturer also: business type (mill, weaver, dyer, processor, trader). | Required before continuing. Business name shown on profile; city shown publicly for mills. |
| A-07 | Land on role home. Retailer → Home with search. Manufacturer → Dashboard with a "Complete your profile" checklist (C-02). | Account status = Active; verification status = Not verified. |

---
## L. Login (returning domestic user)
| Seq | Step | Rules |
|---|---|---|
| L-01 | Sign in screen: mobile number. Link "Don't have an account? Create one". | Same number/OTP rules as A-04. |
| L-02 | Enter OTP. | Same rules as A-05; wrong code shows an inline text error and keeps the number. |
| L-03 | Success → return URL if present, else role home. | New session cookie; previous sessions stay unless the user chose "sign out other devices" (S-03). |
| L-04 | Too many wrong codes. | Lock that number for 15 minutes, show remaining wait, offer X-01. |
| L-05 | Account suspended. | Show a neutral message and support contact; no data shown. |
| L-06 | New device sign-in. | Optional notice by SMS/WhatsApp "New sign-in"; list devices in S-03. |

---
## X. Lost access — replaces "Forgot password"
| Seq | Step | Rules |
|---|---|---|
| X-01 | On the OTP screen: "Didn't get the code?" | First: wait 30 s then **Resend**. Show tips (signal, DND, check number). |
| X-02 | After 2 failed resends: "Try another way". | Options decided by the owner: voice-call OTP or WhatsApp OTP (needs provider; `NEW`). If not enabled, go to X-03. |
| X-03 | "I changed my number / lost my SIM": enter **old number**, **new number**, business name, GSTIN (if any), a short note. | Creates a support case; no data is revealed. Rate-limited. |
| X-04 | OTP to the **new** number to prove ownership. | Same OTP rules. |
| X-05 | Admin verifies (E-05): matches business name, GSTIN, past order, or a document. | Target 1 working day. Decision logged. |
| X-06 | Approved → number updated, **all old sessions revoked**, user notified on the old and new number. Rejected → reason shown, can retry with proof. | Never auto-approve. A number change always logs who, when and why. |

---
## B. Retailer profile and verification (required: every Business must be Approved)
> Correction (20 Nov 2026, see `35` D3 and `docs/AGENTS.md` rule 1): a business whose VerificationStatus is not Approved cannot list products or place orders. This applies to every Retailer as well as every Manufacturer. Only the GSTIN may be left empty at signup; approval itself cannot be skipped.
| Seq | Step | Rules |
|---|---|---|
| B-01 | Profile home: name, shop name, city, mobile (read-only), email (optional). | Edit allowed; mobile change goes through S-04. |
| B-02 | **Addresses:** add shipping and billing addresses (name, phone, line, city, state, pincode, default flag). | Pincode 6 digits; needed at checkout. |
| B-03 | **GSTIN** (may be left empty at signup): enter 15-character GSTIN. | Format check in the app; server validates format; admin or provider check = "GST ✓". Needed for GST invoice and the GST ✓ badge. Leaving it empty does not skip approval. |
| B-04 | **Documents:** GST certificate, shop photo, whatever the admin asks for during review. | PDF/JPG/PNG, max 5 MB each; scanned for type and size; stored privately. |
| B-05 | Status: Not verified / Under review / Verified (Approved) / Needs info / Rejected, as text plus icon. | **Only an Approved business can order.** Not verified, Under review, Needs info and Rejected can browse but cannot place orders or post requests. GST ✓ is an extra badge and gives higher RFQ priority for mills (owner decision). |
| B-06 | Preferences: categories of interest, alerts (price drop, restock), language (English / हिन्दी). | Alerts need the new backend (`26`). |
| B-07 | Orders, RFQs, swatch board, saved mills: lists with status. | Reuse existing order/RFQ lists. |
| B-08 | Support, Terms, Sign out. | S-02. |

---
## C. Mill / Manufacturer onboarding and documents
| Seq | Step | Rules |
|---|---|---|
| C-01 | After A-07: dashboard shows **profile strength** meter and checklist (`27`). | Manufacturer can prepare but **cannot publish** until C-09. |
| C-02 | **Business profile:** logo, cover, about (min 150 chars), specialities, year established, employees, monthly capacity, looms/machines, lead time, trade terms (payment, sample, returns, customisation). | Saved as draft any time. |
| C-03 | **Contact and address:** factory address, city, state, pincode, phone, WhatsApp, hours, languages. | Full address shown only to signed-in buyers (`27`). |
| C-04 | **Documents (required):** GST certificate (GSTIN), PAN, business address proof, authorised signatory ID (kept private). | PDF/JPG/PNG, max 5 MB; stored privately; never shown to buyers. Government ID number is not stored as text; the file is access-controlled. |
| C-05 | **Bank details for payouts:** account holder, account number, IFSC, cancelled cheque image. | Name must match the business or signatory. Account number stored encrypted, shown masked. Penny-drop check if a provider is chosen (owner decision). |
| C-06 | **Proof of operations (recommended):** factory video (30–90 s), 3+ factory photos. | Moderated; used for the "Factory video ✓" check. |
| C-07 | **Certificates (optional):** OEKO-TEX, GOTS, ISO 9001, MSME/Udyam. | Each shows Verified / Self-declared / Not provided with date. |
| C-08 | **Export (optional):** IEC code, countries, ports, Incoterms, export MOQ (`28`). | IEC verified by admin; badge "Export ready" only after that. |
| C-09 | **Submit for verification.** | Status Under review; summary shows what is missing. |
| C-10 | Admin review (E-02): approve, request more info, reject with reason. | Target 2 working days. Emails/notifications on each decision. |
| C-11 | **Approved:** Verified badge and GST ✓ shown; publishing enabled. Rejected → fix and resubmit. | Unverified mills never show a Verified badge. |
| C-12 | **Add products:** one by one or **bulk CSV import** (`28`), tier prices, unit (m / kg / piece), MOQ, photos. Imported items without photos stay draft. | Publish → appears in search and on the mill profile. Then receive RFQs and orders (F). |

---
## D. Overseas buyer (email OTP) — details in `29`
| Seq | Step | Rules |
|---|---|---|
| D-01 | Account: country, company, business email, optional WhatsApp. | Blocked-country list is config, not code. |
| D-02 | Verify email: 6-digit code. | Same OTP rules as A-05 on the email channel. Needs audit first (`PROTECTED`). |
| D-03 | Business details: type, tax / registration no., website, what they buy, volume, port, Incoterm, consent. | Tax ID encrypted, masked to admin, hidden from mills. |
| D-04 | Status **Under review** (24–48 working hours). Can browse, save, prepare RFQ. | Cannot send RFQ or see contacts yet. |
| D-05 | Admin decision (E-03). Approved → can send RFQs to export-ready mills only; Rejected → edit and resubmit. | Reject wording must not reveal internal checks. |
Overseas sign-in later uses the same email-OTP login (L-flow with email). Lost access = support case via E-05.

---
## E. Admin (role Admin only) — `30`
| Seq | Step | Rules |
|---|---|---|
| E-01 | Admin signs in (same OTP; admin accounts created only by an existing admin or seeded, never by public signup). | Consider an extra factor for admins (owner decision). |
| E-02 | **Mill verification queue:** documents, GST, bank match, factory video, flags; approve / info / reject. | Every action logged with reason. |
| E-03 | **Overseas buyer queue** (`30`). | Blocked-country approval refused server-side. |
| E-04 | **Retailer verification queue** (GST, documents) — required: every Business must be Approved before it can order. | Same pattern. |
| E-05 | **Lost-access cases** (X-05). | Reveal of personal data is logged. |
| E-06 | Reports and moderation: reviews, reported suppliers, imports. | Admin changes are append-only in the audit log. |

---
## F. Trade flow (after login and verification) — `PROTECTED`
| Seq | Step | Rules |
|---|---|---|
| F-01 | Search / browse: filters, tabs, shop by use. | `26`. |
| F-02 | Product detail with tier price table, MOQ, unit, estimate, sample option. | Never assume metres. |
| F-03 | Mill profile (`27`). | |
| F-04 | **Catalogue path:** add to cart. | Existing cart rules. |
| F-05 | Cart → address (B-02) → shipping/delivery estimate. | |
| F-06 | Checkout and payment. | Existing checkout and payment logic unchanged. |
| F-07 | Order placed → mill confirms. | Status: Placed, Packed, Shipped, Delivered. |
| F-08 | Shipment tracking. | |
| F-09 | Delivery confirmed → payout to the mill's bank (C-05) per existing payout rules. | Commission per existing rules; RFQ awards carry no commission. |
| F-10 | **RFQ path (optional, `Feature.Rfq`):** buyer posts a request (fabric, quantity, unit, pincode, note). | Domestic or "Outside India" fields (`28`). |
| F-11 | Matching mills receive it and send quotes. | Rules in `13`. |
| F-12 | Buyer compares quotes. | |
| F-13 | **Award:** contacts are shared; deal continues off-platform. No commission. | Unchanged. |
| F-14 | Reviews: after a completed order the buyer can review the mill (`27`). | One review per order. |

---
## S. Session and account
| Seq | Step | Rules |
|---|---|---|
| S-01 | Session: HttpOnly cookie, absolute expiry (owner decides, suggest 30 days idle 7 days), CSRF on every state-changing request. | `PROTECTED` |
| S-02 | **Sign out** current device. | Clears cookie server-side. |
| S-03 | **Devices:** list sessions, "Sign out other devices". | NEW |
| S-04 | **Change mobile number:** OTP to the **new** number, then confirm with OTP on the **old** number if reachable; otherwise X-03. | Notify both numbers; revoke other sessions. |
| S-05 | **Delete account:** confirm with OTP; blocked while orders or payouts are open; data kept as law requires, rest erased. | Owner and legal to define retention. |

---
## Statuses (one glossary)
- **Account:** Active · Suspended · Deleted.
- **Verification (mill / retailer):** Not verified · Under review · Needs info · Verified · Rejected.
- **Overseas buyer:** Email unverified · Under review · Approved · Rejected · Suspended.
- **Order:** Placed · Packed · Shipped · Delivered (· Cancelled).
- **RFQ:** Open · Quotes received · Awarded · Closed.
Show each as text plus icon, never colour alone.

## Security checklist (applies to every flow)
Rate-limit per IP and per identifier; identical responses for existing and non-existing accounts; OTP stored hashed, never logged; no OTP or tokens in URLs; return URL must be internal; CSRF on POST/PUT/DELETE; file uploads checked by type, size and content, stored outside the web root; private documents served only through authorised endpoints; all admin actions in an append-only audit log; personal data masked by default.

## Owner decisions still open
1. Voice/WhatsApp OTP provider for X-02. 2. Session lifetime. 3. Admin extra factor. 4. Penny-drop bank check provider. 5. ~~Retailer verification: optional or required for large orders.~~ **Decided: required for every Business** (`docs/AGENTS.md` rule 1, `35` D3). 6. Account deletion retention. 7. Retailer GST check: provider API or admin manual.

## Build order (sequence for Codex)
1. **Audit** current auth, roles, registration, profile, documents (read-only report; map to the Seq numbers above).
2. Align A-03 to A-07 and L-01 to L-06 with the spec (no logic change to OTP/cookie/CSRF).
3. X-01 to X-06 lost-access flow + admin case screen (E-05).
4. B profile: addresses, GSTIN, documents.
5. C-01 to C-09 mill onboarding with document uploads and bank details.
6. E-02 mill verification queue, then C-10 to C-12.
7. Bulk import (`28`), mill profile (`27`).
8. D-01 to D-05 overseas buyers with admin queue (E-03).
9. S-03 to S-05 session management.
Each step ships behind a feature flag with tests; do not touch cart, checkout, payment, payout or RFQ-award logic.
