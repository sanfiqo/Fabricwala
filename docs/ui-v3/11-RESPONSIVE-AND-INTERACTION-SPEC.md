# 11 — Responsive Web, Mobile and Interaction Specification

## Principle
One brand and design token system; responsive composition adapts to screen size and input modality. Mobile app is a shared buyer/supplier experience, not a squeezed desktop website.

## Layout contracts
| Area | Desktop >=1024 | Tablet 768–1023 | Mobile <768 |
|---|---|---|---|
| Global header | Floating full navigation and mega menu | Compact header + overflow | Compact brand/search/account, bottom navigation |
| Hero | Editorial split / cinematic globe with text/search accessible | Simplified split/stack | Lightweight cinematic scene or static poster, text/search readable |
| Catalog | 3–4 product columns depending width, sidebar filters | 2–3 columns, toggle filters | 2 columns (1 for accessibility or narrow view), filter sheet |
| Product detail | Gallery left ~55%, details right ~45%, sticky action panel | 50/50 or vertical | Single column, sticky RFQ action above safe area |
| Supplier showroom | Full cover, collection+product grid | Responsive grid | Image hero, compact supplier identity, horizontal collections |
| Buyer/supplier dashboard | Collapsible side nav, dense panels where needed | Reduced nav/2-col | Compact KPI cards, activity prioritization, role-aware tabs |
| RFQ | Guided stepper, wider inputs; drafts | Guided steps | One section per view, visible progress and bottom next |
| Quote compare | Side-by-side normalized columns | Horizontal comparison with fixed attribute labels | Select 2–3 quotes, comparison rows or swipeable cards; never hide baseline |
| Chat | List + thread split where space allows | Switch list/thread | Single-thread view with back and context header |
| Admin | Desktop-first dense workspace | Adaptive tables | Minimal operations only if mobile admin separately approved |

## Scrolling/navigation
- Vertical scrolling is default. Horizontal scroll is constrained to distinct content carousels/swatches; never create unexpected whole-page side scrolling.
- Images in galleries are swipeable; touch direction conflicts handled.
- Preserve catalog sort/filter/scroll on return from product; preserve selected product swatch and RFQ draft as appropriate.
- Autoplay marquee and cinematic motion must pause on reduced-motion, offscreen, or user engagement; provide manual controls if content is essential.
- Sticky CTAs must not overlap bottom navigation, software keyboard, or safe-area insets.
- Desktop hover enhancements cannot contain functionality absent on mobile keyboard/touch.

## Navigation details
- Buyer mobile: Home, Explore, RFQ, Inbox, Profile. Supplier mobile: Home, Products, Enquiries, Inbox, Profile. Tab names and order are **proposed** and require usability review.
- Admin global theme applies to all allowed workspaces; backend authorization determines access. Do not simulate arbitrary workspace switching.
- Deep link product/supplier goes to public details without mandatory login; action RFQ/chat may require sign-in.
- Browser back, OS back, modal close and draft preservation rules are explicitly tested.

## Error and loading performance
- Reserve exact image/media heights to reduce layout shift. Use responsive image sizes and lazy-load below the fold.
- Avoid mobile autoplay heavy 3D; poster fallback should visually align with main hero.
- Maintain stable UI during loading (same card aspect ratios and density).
- Virtualize very large lists only where beneficial; maintain accessible pagination/load-more fallback.

## Interaction acceptance checks
- Smallest supported phone, large text/zoom, narrow tablet, laptop, 1440+ desktop, keyboard-only, touch-only, slow network, reduced motion, landscape, Android/iOS safe area.
- Product image detail link, supplier link (once implemented), RFQ prefill (once implemented), share/copy, back/scroll restoration and OTP login return must work without loops or lost data. Role switch requires separate backend design approval.


---

## V2 adaptive art direction
Maintain the same Ivory Atelier hue family on mobile/tablet/desktop. On mobile, prioritize one-column product details, full-width editorial photography, keyboard-safe sticky RFQ CTA, real image swatches and compact bottom navigation. Desktop has 55/45-ish gallery/detail balance as candidate, flexible not fixed. Reduced-motion and low-power poster for globe; persistent navigation and RFQ actions cannot be covered by animated visual layers. The chosen type pair should resize fluidly; no screenshot-locked font sizes.


## V2.1 Implementation Reality Contract — takes precedence over older exploratory wording

- **Verified code baseline (read-only):** `sanfiqo/Fabricwala` `main` at commit `35d0f0e318617e1f8cae2bed82275bbcb7902f24` (retrieved 20 Nov 2026). Source inspection is **not** proof of running behavior; local/unpushed code may differ.
- **Two protected commercial journeys:** retain existing catalog → cart → checkout → paid order → shipment → payout; offer RFQ as an additional sourcing journey only when the `Rfq` feature is enabled. RFQ award gives contacts, **not** a paid order or platform commission.
- **Identity:** mobile OTP / HttpOnly cookie / CSRF, roles `Retailer`, `Manufacturer`, `Admin`. Do not invent password/reset UI, unrestricted role switching or dual role memberships.
- **Unit accuracy:** never assume all goods use meters; support the product's actual API unit and preserve variant/slab/MOQ pricing logic.
- **Visual rules:** Ivory Atelier V2 color intent remains unchanged; treat published token hex values as *candidate* pending visual/contrast review. Preserve Hindi typography fallback and fabric-color accuracy.
- **Scope labels:** `IMPLEMENTED (source)`, `WIRED FRONTEND`, `BACKEND ONLY`, `PROPOSED`, `BLOCKED DECISION`, and `UNVERIFIED RUNTIME` must not be conflated.
- See `19-SOURCE-OF-TRUTH-AND-DECISIONS.md`, `20-SCREEN-TO-CODE-IMPLEMENTATION-MATRIX.md`, `21-API-CONTRACT-RFQ-AND-COMMERCE.md`, `22-PHASED-TASKS-AND-ACCEPTANCE.md`, `23-V2-1-DOCUMENTATION-CONFLICT-AUDIT.md` before using any legacy instructions below. **No implementation authorization is granted by this package.**
