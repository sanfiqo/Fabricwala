# 35 — Repo Reconciliation and One Plan (read this before anything else)
Checked on 20 Nov 2026 against GitHub `sanfiqo/Fabricwala` (private), `main` at `35d0f0e` ("Merge pull request #7 … backend/mysql-suite-and-perf"). Read-only check; nothing was changed in the repo.

## 1. The answer to "should we combine backend and frontend?"
**No. They are already combined.** One repo holds both:
- `src/` and `tests/` = backend (.NET 10, ASP.NET Core minimal APIs, MySQL).
- `client/` = frontend (React + Vite + TypeScript PWA, Tailwind v4, shadcn-style components).
- `docs/` = the project's own documents (rules, phases, API, data model).
The repo's own rule (`docs/AGENTS.md`): **one agent per folder at a time, and a [WEB] task starts only after its [BE] task is done.** That rule is what feels like "two separate projects". Keep it. Merging into one codebase would not fix the confusion; a single plan will.

## 2. What actually causes the confusion (4 findings)
1. **My V3 documents live outside the repo** (zip files: specs 25 to 34 and reference HTML). The repo has its own plan (`docs/PHASES.md`, `docs/PROGRESS.md`). Two plans exist. **Rule from now: `docs/PHASES.md` is the only task list.** My docs become design/spec references stored in the repo, not a second plan.
2. **Three design directions exist at once:**
   - `main`: "charcoal ink" (tokens in `client/src/index.css`: background `#FAF9F6`, ink `#1C1B19`, mute `#5C5A55`, line `#E9E7E2`; files in `client/design-ref/`).
   - Branch `claude/vision-style` (19 Nov, **not merged**, 5 commits, 40 files): "Vision Pro style tokens (cream, emerald, peach)".
   - Your final choice (20 Nov): black and white sourcing UI (spec `25`, ink `#0A0A0A`).
3. **Unmerged work is sitting on `claude/vision-style`**: screens for bulk upload, RFQ (retailer and seller), saved products, seller storefront, legal pages, invoice rows, address book, order rating, help centre. These are valuable and match open tasks P10.x. Only its colour tokens clash with your decision.
4. **Some of my "new features" already exist in the backend**, and a few of my rules conflict with the repo (section 4 and 5).
`frontend/pwa` and `backend/mysql-suite-and-perf` are already fully merged into `main` (nothing left to merge there).

## 3. Decisions (my recommendation first; you only need to say yes or change)
| # | Decision | Recommendation |
|---|---|---|
| D1 | Which design wins? | **Black and white (spec 25).** Implement as a token swap in `client/src/index.css` only. It is close to charcoal ink (`#0A0A0A` vs `#1C1B19`, white vs `#FAF9F6`), so most components need no edits. Do **not** merge the cream/emerald/peach tokens. |
| D2 | What to do with `claude/vision-style`? | **Rebase its screens onto `main`, drop its colour-token commit (`b50a321`)**, then apply D1. Review each screen before merge; its API calls may still be mock-only. |
| D3 | Retailer verification | **Required**, as in the repo (`AGENTS.md` rule 1: a business not Approved cannot list or order). My doc `31` called retailer verification optional; that is **wrong for this repo**. GSTIN can be optional at signup, approval cannot. |
| D4 | Overseas buyers, export profile, globe | **Keep in the plan but after launch essentials (Phase 11).** The repo says "not ready for real customers" (no real SMS, storage, payouts). Adding a second market now spreads the work thin. |
| D5 | Source of truth for design docs | Copy my spec files into `docs/ui-v3/` in the repo (docs-only commit) so Codex reads them from the repo. |

## 4. Feature map against the repo (what exists, what to extend, what is new)
**Exists in backend (do not rebuild):**
| My feature | Repo status |
|---|---|
| OTP login, cookie, CSRF, refresh, admin MFA | `/auth/otp/request`, `/auth/otp/verify`, `/auth/csrf`, `/auth/me`, `/auth/refresh`, `/auth/mfa/*` (real SMS sender missing: P11.1) |
| Register as Retailer or Manufacturer | `POST /businesses`, documents upload, address book, staff |
| Mill profile | `GET /storefronts/{businessId}` (P10.15); WEB screen open (P10.16) |
| Swatch board / saved fabrics | `/saved-products` (PUT, GET, DELETE) |
| Reorder | `POST /orders/{id}/reorder` |
| Bulk estimate and pincode delivery | landed cost (P3.7, P4.2, P10.4), pincode check |
| Invoices and credit notes | `/invoices`, GST PDF (F4.1) |
| Notifications | `/notifications` (list, unread count, read, read all); sending to WhatsApp/SMS not wired (F3.3) |
| Disputes / claims | **"Problem report"** with photos, seller reply, admin decision (P10.11). So backlog item BL-1.1 "dispute flow" is already built in the backend; only the WEB screen (P10.12) is open |
| Reviews | Deleted in F6; the **order rating (1 to 5)** feeds the seller rating. My "reviews from completed orders" must build on this (Phase 12.2) |
| RFQ | Built 18 Nov behind `Feature.Rfq` (off by default): `/rfqs`, `/seller/rfqs`, quotations accept, admin close (`docs/RFQ.md`). Contacts are shown after award |
| Hindi / English | language setting in account (P-NIGHT B14), i18n files in `client/src/i18n` |
| Admin approve / reject / suspend business | `/admin/businesses/*`; audit log through `AdminAuditMiddleware` |
| Banners, categories admin | P10.27 |
| Payout CSV, reports CSV | P10.22, P10.25 |

**Extend (small additions to existing things):**
| My feature | Extend what |
|---|---|
| Admin buyer review queue (spec 30) | Add to `/admin/businesses`: flags (duplicate tax id, free-mail domain, no website), waiting time, a `NeedsInfo` status, request-more-info message. Do **not** build a second queue |
| Mill verification checklist (spec 27) | Add verification records to `BusinessDocument`/Business (type, checked_at, status) and return them in the storefront |
| Mill profile stats (capacity, lead time, response rate) | New fields on Business + computed metrics |
| CSV bulk import (spec 28 Part A) | P10.19 (BE) and P10.20 (WEB). Repo says **CSV only**, same as my spec |
| Data export | Products and orders CSV beside the existing payout and report CSVs |
| Home banners auto-slide | Backend banners exist (P10.27); WEB carousel is new |
| Price alerts | Builds on `/saved-products`; needs a job and sender (needs F3.3 first) |

**New (not in the repo at all):** lost-access flow, device sessions list, sample orders, follow a mill, in-app chat, support tickets, PO PDF, mill analytics/lead inbox, overseas buyer signup and email OTP, IEC and export profile, globe component, import tags and HS codes.

## 5. Conflicts to fix in my docs (honest list)
1. `31` B-03/B-05: retailer verification "optional". Repo: required for ordering. Fix to match the repo.
2. `31` and `29`: new roles/status names. Use the repo's: roles `Admin`, `Retailer`, `Manufacturer`; **everything commercial belongs to a `Business`**, never fields on the user. An overseas buyer is a Business with a country and a type, not a new user kind.
3. `32` BL-1.1 dispute flow: already exists as problem reports.
4. `33` feature statuses must be read together with this file; "Spec" in `33` can mean "Exists" in the repo.
5. Repo naming rules: API routes under `/api/v1/...` in kebab-case; money `decimal(18,2)`; UTC times; enums stored as int; new tables get `CreatedAt` and `UpdatedAt`.
6. The repo launch plan is **one category first (winter wear), all India**. My samples used generic fabrics; keep content flexible.

## 6. The one plan (new rows to add to `docs/PHASES.md`, in the repo's own format)
Follow the repo workflow: first unchecked task, plan in 5 lines, build and test, commit `<id>: <title>`, tick it, BE before WEB.
**Phase 14 — Design V3 (black and white)** `[WEB]`
- [ ] P14.1 Token swap to spec 25 in `client/src/index.css` and `client/design-ref/tokens.json`; run lint, build, e2e; screenshots before and after
- [ ] P14.2 Rebase `claude/vision-style` screens onto `main`, drop the colour-token commit, review each screen against the real API
- [ ] P14.3 Home: auto-slide banner, shop-by-use, filters sheet (spec 26) using existing endpoints
**Phase 15 — Account and verification** (spec `31`, `30`)
- [ ] P15.1 [BE] Lost-access cases (`X-01` to `X-06`) and admin screen data; [WEB] screens
- [ ] P15.2 [BE] Verification records and `NeedsInfo` status on businesses; [WEB] admin queue upgrade
- [ ] P15.3 [BE] Mill profile fields and verification list in storefront; [WEB] profile screen (spec 27)
**Phase 16 — Catalogue tools** (spec `28` Part A)
- [ ] P16.1 [BE] P10.19 bulk CSV import with row validation; [WEB] P10.20
- [ ] P16.2 [BE] products and orders CSV export (CSV-injection guard)
**Phase 17 — After launch essentials (Phase 11 done first)**
- [ ] P17.1 Overseas buyer signup with email OTP (audit auth first; spec `29`)
- [ ] P17.2 Export profile, IEC verification, export-ready matching for RFQ (spec `28` Part B)
- [ ] P17.3 Export globe on overseas signup and mill profile (spec `34`)
- [ ] P17.4 Samples, price alerts, chat, support tickets, PO (from `32`, one by one)
Do **not** start Phase 17 while launch blockers remain (P5.9, P11.1 to P11.9).

## 7. How to work without confusion (the routine)
1. One repo, one task list (`docs/PHASES.md`), one active agent per folder.
2. A feature = one ID (for example `P16.1`) with a `[BE]` part first and a `[WEB]` part second. Never start the WEB part first.
3. Branch names: `backend/<topic>` and `frontend/<topic>` as the repo already does; merge to `main` by pull request; delete merged branches.
4. Before any task: `git status` clean on `main`; if there are uncommitted changes you did not make, stop and ask (the repo's own rule).
5. After a task: update `docs/PROGRESS.md` (one dated line) and tick `PHASES.md` in the same commit.
6. My spec documents are references. If a spec and `AGENTS.md` disagree, **`AGENTS.md` wins**.

## 8. Next five steps (in this order)
1. You confirm D1 to D5 above (five yes/no answers).
2. Add my specs to the repo as docs only: `docs/ui-v3/` (needs write access for me, or you copy the folder and commit).
3. Add the Phase 14 to 17 rows to `docs/PHASES.md` (docs-only commit).
4. Finish the real launch blockers that need your accounts: MSG91 (SMS), file storage (R2), Razorpay live and Route, WhatsApp. Nothing else matters if real customers cannot sign in or pay.
5. Then run P14.1 (design token swap), P14.2 (vision-style screens), and continue Phase 15 onward.
