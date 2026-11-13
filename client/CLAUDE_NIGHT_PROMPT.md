# Claude Code night run: make the PWA work end to end, then polish

You work alone tonight. Nobody will answer questions. Decide yourself, write the decision in client/DECISIONS.md, keep going.

## 0. Hard rules (never break)
- Follow CLAUDE.md, AGENTS.md, client/CLAUDE_CODE_UI_PROMPT.md and client/docs/UX_RULES.md (if the file is missing, follow the first three and say so in the Log).
- Scope: Phase 6, files under client/ only. Branch frontend/pwa. Never edit src/, tests/, migrations or docs/PHASES.md backend ticks. Never touch sibling folders. When you commit, add only client/ (git add client/, check git status first).
- The backend (Codex) works in another folder. Do not edit it. If you need a backend change, write it into client/docs/BACKEND_REQUESTS.md (endpoint, request, response, why) and keep going with mock.
- No secrets in code or logs. Never read or print appsettings.Local.json.
- No force push, no git reset --hard, no deleting branches. Do not delete files you did not create unless UI_MIGRATION.md says so.
- No flag emoji or flag icons anywhere. No paid UI kits. Do not use the GoalOrbit skill.
- Keep all existing routes, hooks and features. Keep the first JS load under 200 KB gzip.
- Design source: client/design-ref (V2 files first) and tokens.json. Do not invent a new look.

## 1. How you run (self-prompting loop)
State lives in client/docs/NIGHT_STATE.md. Create it in step 0 if it does not exist.

Repeat until a stop condition:
1. Read client/docs/NIGHT_STATE.md. Pick the first task that is not done.
2. Do that task only. If it is too big, split it into sub-tasks in the state file.
3. Run build and lint. Run the smoke script for the screens you touched (English at 390px, Hindi at 360px). Fix overflow and console errors.
4. If green: commit (one task = one commit, message "FE-NIGHT <id>: <what>"), tick the task with the commit id, add one line to the Log.
5. If red after 3 tries: revert only your own uncommitted change, mark BLOCKED with the reason, go to the next task.
6. Go back to step 1.

Stop conditions: all tasks done, or 3 tasks BLOCKED in a row, or build/lint is red and you cannot fix it in 3 tries. When you stop, write the Morning report (section 5) at the top of the state file.

If your session is restarted, the user will say "Read client/docs/NIGHT_STATE.md and continue." Do exactly that, no questions.

## 2. Backend contract
Look for client/docs/api/API_SCREENS.md and client/docs/api/openapi.json (the user copies them from the backend folder). Also look for a running API (base URL in client/docs/api/RUN_LOCAL.md). Rules:
- If the contract exists: generate or write typed API functions from it. Use its DTO names and error codes.
- If the API is reachable: set the real-API mode as default in development and test real flows.
- If not reachable or no contract: stay in mock mode, build the adapter layer anyway (one file per area: auth, business, catalog, cart, orders, seller, payouts, notifications, account, admin) so switching is one setting. Write MSW or similar handlers that match the contract shapes. Log it.
- Errors: map backend error `code` to English and Hindi messages in the i18n files. Unknown code -> a calm generic message. Never show raw server text.
- Money: never calculate prices on the client. Show what the server sends.

## 3. Task queue (copy into client/docs/NIGHT_STATE.md as checkboxes in step 0)
Step 0: read CLAUDE.md, the UI prompt, client/docs/UI_MIGRATION.md, client/DECISIONS.md. Create the state file with this queue.

A. Foundation for real data
- A1. API layer: one typed client, auth header, refresh on 401 once, retry only safe GETs, Idempotency-Key on checkout and payment calls, timeouts, error code mapping (EN + HI).
- A2. Mock/real switch per area, with mock data matching the contract and the seed names (Sharma Textiles, Kapoor Knits, Shree Kurti House...).
- A3. TanStack Query setup: keys per area, stale times, optimistic updates only for cart quantity and notification read.
- A4. Auth store: token storage, role-based route guards (retailer / seller / admin), session expired screen and redirect.

B. Build missing screens needed for the journey (from client/design-ref, same tokens, floating pill nav). Rebuild UI only, keep logic.
- B1. Login, OTP, Business details (pincode lookup, GST validation, retailer or manufacturer), Waiting for approval.
- B2. Search with filter sheet. Orders list (with empty and offline states). Track order. Account. Notifications. Report a problem.
- B3. Checkout restyle (no design exists: keep it working, use the same components), payment result states (success, failed, pending).
- B4. Seller: Home, Orders (accept / cannot do / ship with LR number), Products (list, toggle), Add/Edit product, Payouts.
- B5. Admin (laptop layout, tables, keyboard friendly): Home, Approvals list, Review (approve, reject with required note).
Each screen: loading skeleton, empty state, error state with retry, offline state, Hindi tested at 360px and 390px.

C. Wire real flows (only for areas where the API is reachable; otherwise mock + adapter ready)
- C1. Auth + business + approval.
- C2. Catalog: home, search, product, cart.
- C3. Checkout and orders and tracking and delivery confirm.
- C4. Seller orders, products, payouts. Admin approvals.
- C5. Notifications and report a problem and account.

D. Proof that it works
- D1. Playwright script `npm run e2e` that runs the full journey in the browser: retailer signs up -> admin approves -> seller publishes (or uses seed) -> retailer searches, adds to cart, checks out -> seller accepts and ships -> retailer confirms delivery -> seller payout shows. Run it against the real API if reachable, otherwise against mock. Save screenshots of each step in client/docs/e2e/.
- D2. Smoke script covers every screen in English 390px and Hindi 360px/390px: no horizontal overflow, no console errors, no text cut off.

E. Polish pass (only after A-D, or if blocked on backend; do not skip D)
- E1. Screenshot Home, Product, Cart at 390px next to the matching design files. Write every visible difference to client/docs/POLISH_LIST.md. Fix all of them using tokens.json values.
- E2. Look and feel: fonts really load, tap feedback (press scale 120 ms), no layout jump, skeletons match final layout, soft shadow ellipse under product images, rounded covers, lazy images with blur-up, safe areas, content padding above the floating nav.
- E3. Advanced features, mark each done / not done: recent searches and suggestions, filter sheet with live result count, quick quantity chips and tier hint, undo toast on cart removal, pull to refresh, swipe-down to close sheets, haptics where allowed, PWA install prompt and offline cache for Home and Orders, update prompt when a new version is ready, font preload.
- E4. Image slot: client/public/photos/<productId>-<n>.jpg is used when present, otherwise placeholder. Layout must not change when photos arrive.

## 4. Quality bar
- Touch targets at least 44px. Body text at least 15px, nothing below 12px. Contrast AA.
- Every interactive element has a visible focus state and an accessible name.
- Animate only transform and opacity. Respect reduced motion. Keep the light profile for weak phones.
- Forms: React Hook Form + Zod, errors under the field in plain words, never lose typed data on error.
- Language switch must not wipe a half-filled form (fix the known reload issue in A-tasks if cheap).
- No console errors. No unused dependencies added.

## 5. Morning report (top of client/docs/NIGHT_STATE.md, max 25 lines)
- Done: task ids + commit ids.
- BLOCKED: task id + reason in one line.
- Build, lint, smoke, e2e: pass or fail. Real API or mock for each area.
- Screens still missing or different from the design.
- Backend requests you wrote (client/docs/BACKEND_REQUESTS.md).
- What the user must do (copy contract files, run the backend, send product photos).
- How to open it on the phone (dev command and URL pattern).
