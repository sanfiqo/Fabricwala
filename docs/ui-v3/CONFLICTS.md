# CONFLICTS — ui-v3 specs against `docs/AGENTS.md`

Written 20 Nov 2026 while adding these specs to the repo. **Nothing below has been fixed** (except item 0). Rule: if a spec and `docs/AGENTS.md` disagree, `AGENTS.md` wins. Until the owner decides, treat the listed part of each spec as not valid.
Scope: only conflicts with `docs/AGENTS.md`. Found by reading `31`, `33`, `35` fully and by searching `00` to `34` for terms that touch the AGENTS rules; a spec not listed here was not found to conflict, not proven clean.

## 0. Already corrected (step 5 of the task)
| Doc | Was | AGENTS.md rule | Now |
|---|---|---|---|
| `31` section B, B-03 to B-05, E-04, open decision 5 | Retailer verification optional | Business rule 1: a business that is not Approved cannot list or order | Required for every Business; only GSTIN may be empty at signup |
| `33` FM-703 | "Retailer verification queue (optional)" | same | Required |

## 1. Conflicts that need an owner decision or a rewrite
| # | Doc and place | What the spec says | What AGENTS.md says | Suggested direction (not applied) |
|---|---|---|---|---|
| 1 | `29` (Auth section, build order step 3), `31` section D (D-01 to D-05, L-flow with email), `33` FM-605, `35` P17.1 | Overseas buyers sign in with an **email OTP** | Stack: "Auth: Identity + **mobile OTP** + HttpOnly cookie + antiforgery"; the Sanfiqo.Identity contact and OTP code is built for a mobile number. A second channel is a decision "not covered by this file" | Owner decision first (29 itself says wait for approval). Audit auth, then decide. Phase 17 only |
| 2 | `29` (status model, "propose the data model": buyer type `Overseas`, tax id, verification status, reviewer, `reviewed_at`) | A buyer entity with its own status set (Email unverified, Under review, Approved, Rejected, Suspended) and its own review fields | Data rules: "Businesses own everything commercial", never business fields on ApplicationUser; verification is `Business.VerificationStatus`; new tables carry `CreatedAt` and `UpdatedAt`, BIGINT keys, UTC (so `ReviewedAt`, not `reviewed_at`) | An overseas buyer is a Business with a country and a type (as `35` says). Reuse the one verification status and the one admin queue; add the extra states there only after the owner approves |
| 3 | `30` (API sketch) | `GET /api/admin/buyers`, `POST /api/admin/buyers/{id}/approve`, `/request-info`, `/reject`, `/reveal-tax-id` | Naming: routes under `/api/v1/...`, kebab-case. The admin business routes already exist under `/admin/businesses` | Use `/api/v1/admin/businesses/...` and extend them (`35` section 4). Do not build `/buyers` as a second queue |
| 4 | `29` (Under review: "prepare an RFQ"), `31` D-04 | An account that is not yet Approved can browse, save and prepare an RFQ | Rule 1 blocks only listing and ordering, so browsing is allowed, but RFQ is `Feature.Rfq` (off by default) and `docs/RFQ.md` lets only an approved retailer create one | "Prepare, held, not sent" is a new state. Needs an owner decision, or drop it |
| 5 | `32` BL-1.5 "Payment protection (hold money until delivery)", `33` BL-1.5 | The platform holds the buyer's money until delivery | Stack: "Payments: Razorpay Route (split settlement). **Platform never holds a wallet balance.**" (The existing payout hold in `docs/SETTLEMENT.md` is a hold of the seller's share inside Route, not a balance) | Keep as Idea only. 32 already says "avoid early". Owner and legal decision |
| 6 | `32` BL-7.2 "Ledger / khata between buyer and mill", BL-2.10 "Credit terms (net 15/30)", BL-7.1 "Milestone payments (advance, balance)" | Platform-side balances, credit and part payments | Same rule: no wallet balance; checkout is computed once on the server and paid once | Keep as Idea only; each needs an owner decision |
| 7 | `32` BL-2.9 "Team accounts (owner, buyer, accountant roles)", BL-5.8 "Team members and roles for the mill", BL-8.5 "Broker / agent module" | New roles | Roles are `Admin`, `Retailer`, `Manufacturer` (later `Logistics`); a user acts for a business only through `BusinessUsers` with BusinessRole `Owner` or `Staff` | A new role or BusinessRole value is a change to AGENTS.md: ask first. P2.9 (staff by mobile number) is still open and is the base |
| 8 | `13` (permission table: "Create buyer RFQ: only if **buyer role** also granted") | A separate buyer role | The three roles above; a Business is a Retailer or a Manufacturer | Reword as the business type. (`00`, `02`, `03` etc. already say "no dual role memberships") |
| 9 | `13` line 33: "payment **escrow**"; "Accept/reject, order … not scoped" | Mentions escrow; says accept and orders are not built | No wallet balance (see 5). Seller orders accept, reject and dispatch exist (P4.5) | Remove "escrow"; mark as stale. RFQ award still gives contacts only (`docs/RFQ.md`) |
| 10 | `09` rows A02 (password, recovery), A04 Forgot password, A05 Reset password | Password screens in the inventory | Auth is mobile OTP only; no password exists in the stack. (09 line 117 and `18`, `23` say "not applicable" but the rows were left in the table) | Delete the rows A04, A05 and the password part of A02. `31` section X replaces them |
| 11 | `25` (section 5: "Components (all in `css/fabricwala-bw.css`)"), `fabricwala-bw.css` is a plain-class stylesheet | Components written as a separate CSS framework | Frontend: "Tailwind + shadcn/ui" in `client/` | Already settled by `35` D1: a token swap in `client/src/index.css` only. Never load `fabricwala-bw.css` as a second stylesheet in `client/`. The HTML files are visual references only |
| 12 | `32` / `33` BL-11.6 "React Native app (already planned)" | A native app | Frontend: React + Vite + TypeScript **PWA** in `client/` ("do not change without asking") | Keep as Idea; ask before any work |

## 2. Out of date against the repo (not a rule clash, but do not trust the spec)
| Doc | Spec | Repo today |
|---|---|---|
| `31` E-01 and open decision 3 "Consider an extra factor for admins" | Open question | Built (`/auth/mfa/*`), switched by `Auth:AdminMfa`, off by default (AGENTS.md, Roles) |
| `31` F-07 and the Statuses glossary: "Order: Placed, Packed, Shipped, Delivered" | Four steps | Seller order states are Pending, Accepted, Rejected, Dispatched, Delivered, Cancelled (`SellerOrderStatus`); the retailer sees display statuses derived by `OrderStatusRules`. Check before building any status UI. (Rule 6: every status change writes `OrderStatusHistory`) |
| `32` BL-1.1 / `33` BL-1.1 "Dispute / claim flow" | Idea | Built as the problem report (P10.11); only the screen P10.12 is open |
| `31` F-14, `33` FM-303 "Reviews from completed orders, mill reply" | New | Product reviews were deleted on purpose (F6.2); the order rating feeds the seller rating |
| `33` statuses "Spec", "Verify" | Can mean "does not exist" | Read together with `35` section 4 |
| `00` to `24` (Ivory Atelier, Lagoon, glass/pastel direction) | Visual direction | Replaced by `25` (black and white); `css/archive/` keeps the old files for reference only |

## 3. Decisions D1 to D5 of `35` that are still open
D3 (retailer verification required) is settled and applied. D1 (black and white wins), D2 (what to do with `claude/vision-style`), D4 (overseas after launch essentials), D5 (specs in `docs/ui-v3/`) are the owner's to confirm. D5 is done by this commit as a docs-only change.
