# 29 — Overseas Buyer Signup Spec (V3.4)
**Owner decision (20 Nov 2026): overseas buyers can sign up in v1.** Reference: `css/overseas-signup.html` (working demo, demo OTP 123456) and `css/overseas-signup.png`. Extends `28` Part B. Nothing here exists in the repo yet and **no existing auth code may be changed** until the audit and approval steps below.

## Flow (4 steps)
1. **Account:** country, company name, business email, optional WhatsApp/phone (dial code fills from country).
2. **Verify email:** 6-digit code, paste support, resend after 30 s.
3. **Business:** type (importer / wholesaler / retailer / brand / sourcing agent), tax or business registration no. (label changes by country: TRN, VAT, EIN, ABN, BIN/TIN, PAN, etc.), website or LinkedIn (optional), what they buy, yearly volume, destination port (optional), preferred Incoterm (FOB / CIF / EXW), terms and privacy consent.
4. **Review:** status "Under review", expected 24–48 working hours, clear list of what works now vs after approval.

## Status model
| Status | Can do | Cannot do |
|---|---|---|
| Email unverified | nothing beyond signup | browse as signed-in user |
| Under review | browse, swatch board, **prepare** an RFQ (held, not sent) | send RFQs, see mill contacts, request samples |
| Approved | send RFQs to **export-ready** mills only, receive quotes, contacts shared on award (no commission, same as domestic RFQ), request samples | — |
| Rejected | read the reason, edit details and resubmit | send RFQs |
| Suspended | read-only | everything else |
Status is always shown as text plus icon, never colour alone.

## Auth: what must be decided before coding (protected area)
Existing auth is mobile OTP + HttpOnly cookie + CSRF for domestic users. Overseas buyers have no Indian mobile, so v1 needs an **email OTP channel**. Codex must:
1. Audit the current OTP/session code and report how a second channel (email) can be added **without changing the domestic phone-OTP behaviour** (same cookie/CSRF rules, same expiry, same attempt limits).
2. Propose the data model: buyer type `Overseas`, country, company, tax id (+ type), verification status, reviewer, reviewed_at, reject_reason.
3. Wait for owner approval before implementing. Add tests: domestic login unchanged, email OTP expiry, attempt lockout, resend throttle.
Rules for the email OTP: 6 digits, single use, 10 min expiry, max 5 attempts, resend after 30 s and max 5 per hour, same response whether the email exists or not (no account enumeration), rate limit per IP and per email.

## Admin review queue (designed: see `30-ADMIN-BUYER-REVIEW-SPEC.md`)
List of pending buyers with country, company, email domain, tax id, website, submitted time. Actions: Approve, Reject (reason required), Request more info. Checks the admin can see: free-mail domain warning, country on blocked list, duplicate tax id or company, website reachable. Every action is logged (who, when, why). Target decision time 48 working hours.

## Compliance and risk (business decisions needed)
- **Blocked countries / sanctions:** maintain an admin-managed list; signup from a listed country is refused with a neutral message. Owner to decide the list and get legal advice. Do not hard-code it.
- **Data protection:** consent checkbox is required (privacy policy must name the data collected, purpose, retention, and who sees it). EU/UK buyers may need GDPR handling (access and deletion requests). Get legal review before launch.
- **Tax ID:** stored encrypted, never shown to mills, shown masked to admin by default. It is not a substitute for real verification; admin may request a company document.
- **Fraud:** free-mail addresses are allowed but flagged; one account per email; rate-limit signups; hold the first RFQ for manual review.
- **Payments:** FabricWala does not handle international payments or customs in v1 (see `28`). Say so in the UI wherever quotes are shown.
- **Language:** English only for overseas screens in v1. No Hindi toggle there.

## UI rules
- Errors appear next to the field, in text, with `role="alert"`; they clear when the user edits the field. First invalid field gets focus.
- One column, 48 px inputs, numeric keypad for code and phone, `autocomplete` attributes set (`one-time-code`, `organization`, `email`, `tel-national`).
- Back button goes to the previous step without losing entered data.
- Indian users see a link "Based in India? Sign in with your mobile number".

## Build order for Codex
1. Audit auth and report (no code). 2. Data model + migration proposal. 3. Email OTP channel behind a feature flag, with tests. 4. Signup API and the four screens. 5. Admin review queue. 6. Hook approved status into RFQ (export-ready matching from `28`). Do not touch cart, checkout, domestic OTP or RFQ award logic.


---
**V3.9:** step 1 of signup now has an export globe hero; see `34-EXPORT-GLOBE-COMPONENT.md`. The page must work fully without it.
