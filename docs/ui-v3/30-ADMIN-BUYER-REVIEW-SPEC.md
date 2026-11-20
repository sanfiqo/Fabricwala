# 30 — Admin Buyer Review Queue Spec (V3.5)
Reference: `css/admin-buyer-review.html` (working demo with sample data; open on a phone-width window and a desktop window) plus `css/admin-buyer-review.png` (mobile) and `css/admin-buyer-review-desktop.png`. Completes the review step in `29`. Nothing here exists in the repo yet. Role: **Admin** only (existing role model; no new role needed in v1).

## Layout
- **Mobile:** tabs, then a list; tapping a buyer opens the detail full-screen with "Back to list".
- **Desktop (≥ 800 px):** list on the left (360 px), detail on the right; the selected buyer is marked with a thick outline and `aria-current`.
- Tabs with live counts: **Pending, Needs info, Approved, Rejected**. Search by company, country or email.
- The list is sorted **oldest first** so nothing is forgotten.

## List item
Company, waiting time, country, business type, email domain, and flag chips. Waiting text: `5 h waiting` (under 24 h), `Due soon · 30 h` (24–48 h), `Overdue · 52 h` (over 48 h). Flags use text plus icon, never colour only.

## Flags and checks (computed by the server, never by the browser)
| Check | Meaning | Effect |
|---|---|---|
| Blocked country | Country is on the admin-managed list (`29`) | **Approve is disabled.** Admin can reject; an override needs the owner or a super admin and is logged |
| Duplicate tax ID | Same tax ID used by another buyer | Warning; admin decides |
| Free-mail address | gmail, yahoo, outlook, hotmail and similar | Warning; ask for a company document |
| Website not reachable / none | Site did not load or was not given | Warning |
The demo uses the fake country "Demoland" as the blocked example. The real list is configuration, not code.

## Detail
Country, business type, email, tax ID (masked), website, what they buy, yearly volume, checks list, history (submitted, every admin action with name, time and reason).
- **Reveal tax ID** shows the full value and writes a log entry ("Tax ID viewed", who, when). Reveal is allowed for Admin only, and rate-limited.
- No phone, address or documents are shown on this screen unless the buyer uploaded them for review.

## Actions (open buyers only)
| Action | Rules | Result |
|---|---|---|
| **Approve** | Confirm step. Disabled for blocked countries | Status Approved, buyer emailed, can send RFQs to export-ready mills |
| **Request more info** | Message required (min 10 chars) | Status Needs info, buyer emailed the message; buyer replies by updating details, status returns to Pending |
| **Reject** | Reason required (could not verify business, incomplete details, country not supported, duplicate account, other); a note is required for "Other" | Status Rejected, buyer emailed the reason; buyer may edit and resubmit |
Closed reviews are read-only. Reopening needs a super admin and is logged. Errors show beside the field in text (`role="alert"`) and clear when edited. Sheets close with Esc and the backdrop; focus returns to the button.

## Audit log (required)
Table `buyer_review_log`: buyer id, action, admin id, time, reason or message, previous status, new status, IP. Append-only; never edited or deleted from the app. Show the full log in the history block.

## Emails sent (templates to write)
Approved; More information needed (includes the admin message); Rejected (includes the reason and a resubmit link); Reminder after 7 days with no reply. Keep wording plain and short; do not reveal which internal check failed (for example, never say "sanctions"; use "country not supported").

## Metrics for the owner (later)
Median time to decision, share overdue (> 48 h), approval rate by country, number of reopened reviews.

## API sketch (for Codex to confirm against the repo)
`GET /api/admin/buyers?status=pending&q=` · `GET /api/admin/buyers/{id}` · `POST /api/admin/buyers/{id}/approve` · `/request-info` · `/reject` · `POST /api/admin/buyers/{id}/reveal-tax-id`. All require the Admin role, CSRF token and the same session rules as the existing admin area. Each action POST accepts an idempotency key so a double tap cannot double-send emails.

## Tests required
Blocked-country approval refused server-side (not only the disabled button); reject without reason refused; non-admin gets 403; every action writes one log row; reveal is logged; double submit does not send two emails; status transitions follow the table above only.

## Build order for Codex
Audit admin area and role checks (no code) → data model + log table → list/detail read API → actions with tests → emails → UI. Do not touch cart, checkout, domestic OTP or RFQ award logic.
