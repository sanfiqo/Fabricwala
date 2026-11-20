# 10 — Authentication, Forms, Modals, Loaders, Feedback, Edge Cases

## Purpose
Complete previously missing global interaction states for all web/mobile pages. UI specification only; identity provider/verification methods must follow existing backend reality, not invented security workflows.

## Form anatomy
Label (always visible), required/optional, example hint, input, helper text, inline validation, error message, accessible association, disabled and submitting states. Required fields explained before submission. Support keyboard navigation, paste, autofill, screen readers, field-level errors and global summary for long forms.

## Authentication states
- Login: idle, filled, submitting, invalid credentials, rate limited where backend supports, offline, expired session, successful redirect, return-to-intended-page.
- Registration: role selection, in-progress, duplicate identifier handled without leaking sensitive details, successful account creation, verified/pending verification as implemented, interrupted resume.
- **Existing OTP-only auth:** request OTP, resend cooldown, invalid/expired OTP, rate limit, pending business approval, session expiration and admin MFA pending. Password/reset controls are **out of scope** until the owner authorizes a new auth mechanism.
- Role-switching: accessible switcher only when both roles granted, explicit context change, no accidental draft loss; keep a clear workspace indicator.

## Dialog and overlay catalog
| UI type | Use | Interaction specification |
|---|---|---|
| Confirmation dialog | Destructive actions: delete/archive | Action-specific heading, consequence, cancel, confirm, loading, failure |
| Form modal | Small contained edit | Focus trap, accessible title, close and draft caution |
| Side drawer | Desktop filters, supplier quick view | Keep parent context, ESC/backdrop close, focus restore |
| Mobile bottom sheet | Filters, sorting, share, selector | Drag/close behavior where accessible, keyboard and safe area |
| Image viewer | Fabric zoom/texture | Pinch zoom, swipe, thumbnails, close, descriptive alt |
| Toast | Completed action | Noncritical success; not sole feedback; avoid exposing private details |
| Banner | Network/maintenance/permission | Persistent until handled, appropriate status role |
| Popover/tooltip | Small auxiliary detail | Hover+focus/tap parity, avoid critical info in tooltip alone |

## Load, empty and error states
| Surface | Loading | Empty | Error/permission |
|---|---|---|---|
| Search/catalog | Skeleton cards preserving grid | No results + clear filters/search alternative | Retry and keep query |
| Product | Gallery/spec skeleton | Unavailable listing and related browse | Image fallback and retry |
| Supplier showroom | Header/product placeholders | New supplier intro and no-products message | Profile unavailable/suspended messaging |
| Buyer dashboard | KPI and recent RFQ skeleton | Create first RFQ guidance | Partial failure per panel |
| Supplier dashboard | Counters + activity skeleton | Add products / respond to first enquiry | Failed enquiries retry |
| RFQ wizard | Input readiness | N/A | Draft preservation, validation summary, retry submit |
| Quotes | Quote placeholders | No quotations yet, current RFQ context | Access denial/expired link |
| Chat | Conversation skeleton | No messages and start-context CTA | Offline draft, send failure, retry |
| Admin tables | Table skeleton | Empty filters | Permission-denied and recoverable fetch error |
| Theme preview | Preview skeleton | Defaults | Invalid contrast blocks publishing |

## Upload and attachment UX
- Show type/size constraints based on server settings, progress, cancel/retry, preview, remove, failed validation and accessible filename.
- Images show correct colors; do not auto-grade fabric assets. Preserve aspect ratio and truthful thumbnail.
- Virus scanning, storage provider, file-size limits and retention are **technical decisions pending backend audit**.

## Destructive/action confirmations
- Save and favorites may be optimistic with undo on failure.
- Publish, suspend, remove, delete or accept quote must confirm business consequence and permission based on backend. No irreversible behavior should be invented in UI spec.
- Quotation actions have distinct statuses: draft, sent, viewed, replied, expired, accepted/rejected **only when business workflow supports them**; status labels should be configurable from actual domain model.

## Chat and notifications
- Conversation list: unread counts, avatar/initials, linked product/RFQ if permitted.
- Thread: timestamps, message delivery state if available, attachments, long-text break, disabled send when offline, reconnect state, retry without duplicate send.
- Notifications: grouping, mark read, deep links, stale/removed target fallback; permission prompts only in context.

## Accessibility & reduced motion
- Screen-reader announcements on validation, form steps, and completion; honor `prefers-reduced-motion` and OS settings.
- No autoplay that blocks core actions; pause decorative animation when offscreen or tab inactive.
- Contrast, keyboard, text resizing, hit targets, captions/alternative text as applicable.

## UX writing conventions
- CTA labels use concrete verbs: Request Quote, Send Enquiry, View Supplier, Compare Quotes.
- Avoid fake claims (Verified, Best Price, Instant Delivery) unless backed by data.
- Success/error copy states the action outcome and next step; do not say 'done' before API confirms.


---

## V2 state specifications
Inputs/modals/drawers/skeletons/toasts use porcelain surfaces `#FFFCF8`, ivory surrounding canvas `#F5F0E9`, fine borders `#E6DDD3`, espresso text/actions `#2E211B`, and Manrope labels. Error, success and warnings keep their semantic accessible colors. Login/registration includes clear role labels, no purple gradients. Skeletons match card image ratio and must not shift layout. Empty/error pages contain actionable next steps; illustrations are optional, never required to understand state.


## V2.1 Implementation Reality Contract — takes precedence over older exploratory wording

- **Verified code baseline (read-only):** `sanfiqo/Fabricwala` `main` at commit `35d0f0e318617e1f8cae2bed82275bbcb7902f24` (retrieved 20 Nov 2026). Source inspection is **not** proof of running behavior; local/unpushed code may differ.
- **Two protected commercial journeys:** retain existing catalog → cart → checkout → paid order → shipment → payout; offer RFQ as an additional sourcing journey only when the `Rfq` feature is enabled. RFQ award gives contacts, **not** a paid order or platform commission.
- **Identity:** mobile OTP / HttpOnly cookie / CSRF, roles `Retailer`, `Manufacturer`, `Admin`. Do not invent password/reset UI, unrestricted role switching or dual role memberships.
- **Unit accuracy:** never assume all goods use meters; support the product's actual API unit and preserve variant/slab/MOQ pricing logic.
- **Visual rules:** Ivory Atelier V2 color intent remains unchanged; treat published token hex values as *candidate* pending visual/contrast review. Preserve Hindi typography fallback and fabric-color accuracy.
- **Scope labels:** `IMPLEMENTED (source)`, `WIRED FRONTEND`, `BACKEND ONLY`, `PROPOSED`, `BLOCKED DECISION`, and `UNVERIFIED RUNTIME` must not be conflated.
- See `19-SOURCE-OF-TRUTH-AND-DECISIONS.md`, `20-SCREEN-TO-CODE-IMPLEMENTATION-MATRIX.md`, `21-API-CONTRACT-RFQ-AND-COMMERCE.md`, `22-PHASED-TASKS-AND-ACCEPTANCE.md`, `23-V2-1-DOCUMENTATION-CONFLICT-AUDIT.md` before using any legacy instructions below. **No implementation authorization is granted by this package.**


## V3.0 SUPERSEDES THIS FILE'S STYLE — see `25-BW-SOURCING-UI-SPEC.md` (20 Nov 2026)
Owner approved the **black & white Alibaba-style sourcing UI**. All Ivory/espresso, Lagoon, pastel, glass, neumorphism, lime/Weave and accent-colour guidance in this file is **retired** (history only; old CSS in `css/archive/`). Current tokens: `css/fabricwala-bw.css`. Colour appears only in fabric photographs (and semantic status colours).
