# Codex night run: make the backend work end to end

You work alone tonight. Nobody will answer questions. Decide yourself, write the decision down, keep going.

## 0. Hard rules (never break)
- Follow AGENTS.md and docs/UX_RULES.md. If they disagree with this file, they win.
- Backend only: src/, tests/, migrations, docs/. Never touch client/ or any sibling folder.
- Never read, print, edit or commit appsettings.Local.json. No secrets, keys, passwords or tokens in code, logs or docs.
- No force push, no git reset --hard, no deleting branches, no deleting files you did not create.
- Do not use the GoalOrbit skill. Do not use full-access or "yolo" mode.
- Branch: backend/api-for-screens (create it from the current backend branch if missing). Commit only here.
- Dev only: OTP uses a fixed dev code (123456) when Development. Payments use test or fake gateway. Never call real SMS or real money.
- Bank account numbers are never stored or returned. Platform never holds wallet funds.
- Money is always computed on the server.
- Do not read the whole project. Use rg to find things. Open only the files you need.

## 1. How you run (self-prompting loop)
State lives in docs/NIGHT_STATE.md. Create it in step 0 if it does not exist.

Repeat until a stop condition:
1. Read docs/NIGHT_STATE.md. Pick the first task that is not done.
2. Do that task only. Keep it small. If it is too big, split it into sub-tasks in the state file.
3. Add or update tests. Run the tests that cover your change. Run the full suite after every 3 tasks and at the end.
4. If green: commit (one task = one commit, message "P-NIGHT <id>: <what>"), tick the task in the state file with the commit id, add one line under "Log" (what you did, what you decided).
5. If red: fix it. If still red after 3 tries, revert only your own uncommitted change, mark the task BLOCKED with the reason, and go to the next task.
6. Go back to step 1.

Stop conditions: all tasks done, or 3 tasks BLOCKED in a row, or the full suite is red and you cannot fix it in 3 tries. When you stop, write the Morning report (section 5) at the top of docs/NIGHT_STATE.md.

If your session is restarted, the user will say "Read docs/NIGHT_STATE.md and continue." Do exactly that, no questions.

## 2. Task queue (copy this into docs/NIGHT_STATE.md as checkboxes in step 0)
Step 0 (do first): read AGENTS.md, docs/API_SCREENS.md, docs/PHASES.md. Create docs/NIGHT_STATE.md with this queue, a Log section and an empty Morning report.

A. Contract
- A1. If docs/API_SCREENS.md has no "Gaps" section, add it: one line per partly/missing item (screen | what is missing | size). Also add rows for the 33 not-yet-designed screens (see docs/CODEX_API_FOR_SCREENS_PROMPT.md section 2) if missing.
- A2. Error body everywhere: { code, message, fields? } with stable string codes. Add a list of codes to docs/API_SCREENS.md.

B. The core journey. Build or complete in this order. Each item: endpoint(s), validation, permission check, tests.
- B1. Auth: send OTP, verify OTP (dev code 123456 in Development), refresh, logout. Role: retailer, seller, admin.
- B2. Business profile: create, get, pincode lookup (city + state), GST format validation (15 chars, optional), status (pending / approved / rejected with note).
- B3. Admin approvals: list (pending/approved/rejected, paged), review detail (details, GST check result, documents), approve, reject (note required).
- B4. Catalog for retailer: home (categories, best sellers), search with filters (category, price, MOQ, state) and paging, product detail (images, price tiers by quantity, landed price for the retailer pincode, colours, sizes, stock).
- B5. Cart: get, add, change quantity, remove, tier price and "add N more to save" data.
- B6. Checkout: address, create order, payment (test gateway), all idempotent (Idempotency-Key). Split by seller.
- B7. Orders for retailer: list (active / delivered / problems), detail, tracking timeline, confirm delivery.
- B8. Seller orders: list by status (new / to pack / shipped / done), accept, reject, ship with LR/tracking number. Response has order value, platform fee (from config, default 2%), "you get". Accept window expires -> auto cancel + full refund (background job + test with a fake clock).
- B9. Seller products: list with filters (all / low stock / draft), search, on/off toggle, create, edit, save draft, publish, price tiers, MOQ, sizes, colours + stock.
- B10. Payouts: hold until delivery confirmed or 7 days, then release. Endpoint: next payout, on hold, paid this month, per-order list, CSV download. Never return a bank account number.
- B11. Seller dashboard: new orders count, sales this month, next payout, 6 month chart data, low stock alert, best sellers.
- B12. Notifications: list, unread count, mark one read, mark all read.
- B13. Report a problem: reason enum, pieces (1..ordered), up to 3 photos, note. Payment stays on hold while open.
- B14. Account: profile, addresses (list, add, edit, delete), language preference (en/hi).
- B15. Admin home: stats for today/7/30 days (GMV, orders, commission, new businesses), needs-a-decision counts.

C. Proof that it works
- C1. One integration test "FullJourney": retailer signs up -> admin approves -> seller publishes product -> retailer searches, adds to cart, checks out -> seller accepts and ships -> retailer confirms delivery -> payout is released. It must call the real HTTP endpoints (test server), not services directly. This is the most important test of the night.
- C2. Second test: seller does not accept in time -> order cancelled and refunded.
- C3. Third test: retailer reports a problem -> payout stays on hold.

D. Frontend handoff
- D1. Export OpenAPI to docs/openapi.json. Keep DTO names stable. List any DTO you renamed in the Log.
- D2. Development-only seed matching the designs: Sharma Textiles (Indore, retailer), Kapoor Knits (Ludhiana, seller), Shree Kurti House, Gupta Garments, Mehta Stores, Raj Woollens (pending), products: Quilted puffer jacket (tiers 50+ 480, 200+ 459, 500+ 440), Fleece hoodie, Sherpa jacket, Wool cardigan (draft). Admin user for dev.
- D3. docs/RUN_LOCAL.md: how to start the API locally with seed data in Development, base URL, dev OTP, dev users (mobile numbers), without any secret. Say where the user must put their own secrets (user-secrets) without showing values.
- D4. CORS for the frontend dev origin (http://localhost:5173 and the LAN dev host) in Development only.

E. Nice to have (only if A-D are all done)
- E1. Admin lists: orders, problem reports (list + detail + resolve), sellers, retailers, product moderation, payouts, commission report.
- E2. Saved products, rate order, GST invoice list, address pincode check, reorder.

## 3. Quality bar
- Every endpoint: input validation, correct HTTP status, permission test (wrong role gets 403), not-found test.
- Lists are paged and sorted stably.
- No N+1 queries on list endpoints. Add the needed indexes in a migration.
- Do not weaken or delete existing tests to make things pass.
- Update docs/PHASES.md ticks only for backend tasks you actually finished.

## 4. Do not
- Do not refactor unrelated code. Do not rename existing public DTOs unless needed, and log it.
- Do not add new NuGet packages unless needed. If you add one, write why in the Log.
- Do not leave the working tree dirty when you stop.

## 5. Morning report (top of docs/NIGHT_STATE.md, max 25 lines)
- Done: task ids + commit ids.
- BLOCKED: task id + reason in one line.
- Tests: total passed / failed. FullJourney test: pass or fail.
- Decisions you made alone.
- What the user must do (secrets, services, anything outside code).
- Next 3 tasks.
