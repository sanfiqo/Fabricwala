# Codex task: make the backend ready for the 27 new app screens

## 0. Rules (read first)
- Follow AGENTS.md and docs/UX_RULES.md. They are the source of rules.
- Backend only (src/, tests/, migrations, docs/). Do NOT touch client/ (Claude Code owns it, branch frontend/pwa).
- Do NOT inspect the whole project. Read only: AGENTS.md, docs/PHASES.md, docs/PHASES_NEXT.md, docs/UX_RULES.md, the API controllers folder, and the DTOs you need. Use search (rg) before opening files.
- Never read, print, edit or commit appsettings.Local.json. No secrets in code or chat.
- One small task = one commit with tests. No force push, no reset --hard, no deleting branches.
- Existing tests must stay green. Run the test suite once at the end of each task, not after every edit.
- Work on a new branch: backend/api-for-screens (from the current backend branch).

## 1. Goal
The PWA has 27 designed screens. Make sure every screen has a real, stable API behind it, with DTOs the frontend can rely on.

## 2. Step A: audit only (STOP after this and report)
Create docs/API_SCREENS.md. One table row per screen with: screen, endpoint(s), request DTO, response DTO, status (exists / partly / missing).

Screens:
- Retailer: Home (categories, best sellers, search + filter), Product (price tiers by quantity, landed price, colours/sizes/stock), Cart, Checkout, Orders list, Track order, Search + filters, Account, Notifications (list, mark all read, unread count), Report a problem (reason, pieces, photos, note).
- Auth: Login (mobile), OTP, Business details (type retailer/manufacturer, name, pincode lookup, GST optional), Waiting for approval (status).
- Seller: Dashboard (new orders, sales, next payout, 6 month chart, low stock, best sellers), Orders (new / to pack / shipped / done, accept, reject, ship), Products (list, search, filters all/low stock/draft, on/off toggle), Add product (photos, tiers, MOQ, sizes, colours + stock, save draft, publish, Excel upload), Payouts (next payout, on hold, paid, per order list, CSV download).
- Admin: Home (GMV, orders, commission, new businesses, needs-a-decision counts, Today/7/30 days), Approvals list (pending/approved/rejected), Review (details, GST check result, documents, approve, reject with required note), problem reports.

Do not write code in step A. Report: how many rows exist / partly / missing, and the list of missing ones. Then stop and wait for my answer.

## 3. Step B: fill the gaps (after I say go)
For each missing or partly done row, in this order: auth and retailer, then seller, then admin. Small commits. Each endpoint needs: validation, permission check (retailer / seller / admin), tests (happy path + permission + validation).

## 4. Rules the API must follow
- Money: landed price = factory price + delivery + GST, computed on the server. The client never calculates money.
- Seller order response includes order value, platform fee 2% and "you get". Fee percentage comes from config, not hard-coded.
- Seller is paid after the shop confirms delivery, or auto after 7 days. Show status: on hold / releases on <date> / paid.
- Seller must accept within the time limit, otherwise the order is cancelled and the shop is refunded in full.
- Payouts API never returns a bank account number. Only "linked bank account". The platform never holds wallet funds.
- Checkout and payment calls are idempotent (Idempotency-Key header).
- GST number: validate 15 characters format. Optional at signup. Admin review shows the GST check result.
- Pincode lookup returns city and state.
- Report a problem: reason enum, pieces count (1..ordered quantity), up to 3 photos, optional note. Payment stays on hold while a report is open.
- Reject in admin requires a note (server validates).

## 5. Contract quality
- Error body is always: { code, message, fields? }. code is a stable string (for Hindi translation on the client).
- Lists are paged (cursor or page + size) and sorted stably.
- Export OpenAPI (swagger.json) to docs/openapi.json and keep DTO names stable. Tell me if you rename any DTO.
- Add seed data that matches the designs: Sharma Textiles (Indore, retailer), Kapoor Knits (Ludhiana, seller), Shree Kurti House, Gupta Garments, Mehta Stores, Raj Woollens (pending), products: Quilted puffer jacket (tiers 50+ ₹480, 200+ ₹459, 500+ ₹440), Fleece hoodie, Sherpa jacket, Wool cardigan (draft). Seed only in Development.

## 6. Mock mode for the frontend
Frontend has a mock mode. Keep the response shapes in docs/API_SCREENS.md identical to what the real API returns, so Claude Code can switch from mock to real by changing one setting.

## 7. Report format (short)
- Done: tasks and commit ids.
- Tests: count passed.
- Missing or blocked: list.
- Questions: max 3.
Update docs/PHASES.md ticks only for backend tasks you finished.
