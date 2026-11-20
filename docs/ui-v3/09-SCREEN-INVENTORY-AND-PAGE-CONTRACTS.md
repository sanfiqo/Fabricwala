# 09 — Complete Screen Inventory & Page-by-Page Contracts

## Classification
**Established** = mentioned and included in previous FabricWala discussions. **Proposed** = screen/pattern to complete design scope, not an existing feature assertion. This document does not assert these screens exist in code.

## Cross-screen contract
Every page must specify: route/entry point; actor/permission; purpose; input/data dependencies; primary CTA; secondary navigation; empty/loading/error/permission/unauthenticated states; responsive layout; social/share behavior if public; analytics event definition if required; SEO and accessibility where public. Preserve search/filter and previous scroll position on back navigation where feasible.

## A. Public and discovery screens
| ID | Screen | Required sections / actions |
|---|---|---|
| P01 | Landing/Home | Floating header; cinematic textile globe hero; prominent fabric/supplier search; featured categories; fabrics; suppliers; RFQ CTA; trust; footer |
| P02 | Search overlay/results | Query suggestions, recent search (optional), filters, sort, result count, empty/no results, link to products/suppliers |
| P03 | Category landing | Category intro, subcategory chips, featured products, browse related categories |
| P04 | Fabric catalog | Sidebar filters desktop, sheet mobile, grid/list if justified, sort, pagination/incremental load, product save/share |
| P05 | Product detail | Image gallery, fullscreen/zoom, specs, swatches, MOQ, indicative price if available, supplier, RFQ, contact, related products, share |
| P06 | Supplier directory | Supplier search, region/speciality filters, verified labels only when verified, card to showroom |
| P07 | Public supplier showroom | Cover, logo, capabilities, optional verified indicator, collections, products, about, contact/social, share, similar suppliers |
| P08 | Category/collection editorial | Hero, category explanation, image-first products, horizontal curated showcase |
| P09 | Public RFQ discovery / explanation | How RFQs work, buyer/supplier CTA, visible opportunities only per privacy permission |
| P10 | Pricing / plans (proposed) | Plan comparison and subscribe CTA only if subscription model exists; do not invent prices/entitlements |
| P11 | About / help / contact / policies | Clear support navigation, FAQs, official policies (content requires business/legal approval) |

**P05 order:** Hero gallery; title + key specs; supplier credibility; swatches; MOQ/quantity; persistent RFQ/contact actions; detailed composition/GSM/width/weave; shipping/sample terms if provided; similar fabrics; more from supplier; recently viewed; related categories; supplier profile link. Do not place all recommendations before the primary RFQ action.

**P07 order:** Visual cover; company identity + factual trust; primary enquiry; collections; product grid + filtering; about/capabilities; company contact/social opt-ins; related suppliers. Supplier may edit photos/text, never global UI theme.

## B. Authentication & onboarding
| ID | Screen | Required behavior |
|---|---|---|
| A01 | Welcome/onboarding | Optional cinematic/editorial introduction; skip/replay controls; meaningful CTA |
| A02 | Sign in | Email/mobile/login identifier per backend; password; show/hide; recovery; errors |
| A03 | Registration | Shared identity; role onboarding; terms link; no invented verification requirements |
| A04 | Forgot password | Confirm submission without disclosing account existence |
| A05 | Reset password | Token state, expiry, password feedback, success |
| A06 | Verification / pending | Clear verification method/status where actually supported |
| A07 | Profile completion | Buyer details or supplier business profile with draft/resume |
| A08 | Switch workspace | Display only roles actually granted; preserve draft work/context safely |

## C. Buyer workspace
| ID | Screen | Required behavior |
|---|---|---|
| B01 | Dashboard | RFQs/quotes/messages/saves; action: create RFQ; recent activity |
| B02 | Saved fabrics | Filterable list; remove/unsave; opens product |
| B03 | Saved suppliers | Supplier list, open showroom, remove |
| B04 | My RFQs | Status tabs and filters; create new; list/detail links |
| B05 | RFQ wizard | Fabric requirement → quantity/delivery → supplier preference/review; draft and prefill |
| B06 | RFQ detail | Specs, attachments, suppliers invited, statuses, associated quotes, edit constraints |
| B07 | Quotation inbox | Group by RFQ; received date/status, supplier identity |
| B08 | Quotation detail | Price basis, MOQ, terms, lead time, validity, attachments, contact |
| B09 | Quote comparison | Like-for-like breakdown, unit normalization warning, explicit comparison basis |
| B10 | Messages | Conversation list, thread, attachments, RFQ context, notifications |
| B11 | Notifications | Unread/read, categories, deep links and states |
| B12 | Account & preferences | Identity, contact, business profile, workspace switch |

## D. Supplier workspace
| ID | Screen | Required behavior |
|---|---|---|
| S01 | Dashboard | New enquiries, active products, quotes, showroom, quick respond |
| S02 | Product inventory | Search, draft/published status, edit/archive actions |
| S03 | Add/edit product | Images, specs, variants, MOQ, price visibility policy, preview, validation |
| S04 | Supplier inbox/enquiries | Buyer enquiries, filters, clear actions |
| S05 | RFQ opportunities | Matching RFQs, permission-restricted details, quote CTA |
| S06 | RFQ detail / respond | Requirement, attachments, eligibility and quote workflow |
| S07 | Create/edit quotation | Unit price, currency, quantity tiers, tax/shipping terms, lead time, validity, notes |
| S08 | Quotations sent | Status/date filters and details |
| S09 | Showroom editor | Cover, logo, about, collections, products, links; preview/publish |
| S10 | Collections manager | Create, reorder, curate products; empty states |
| S11 | Analytics | Views, leads, engagement with time range and truthful methodology |
| S12 | Business profile | Contact options, manufacturing capabilities, certifications (only if verified) |
| S13 | Messages & notifications | Business context threads, read/unread |

## E. Admin workspace (scope requires implementation confirmation)
| ID | Screen | Required behavior |
|---|---|---|
| M01 | Overview | Operational status, actionable moderation items |
| M02 | Buyer/member management | Search, status, permissions, detail, audit visibility |
| M03 | Supplier management | Review, verification status, suspend/reactivate if authorized |
| M04 | Product/category moderation | Review/edit policy, search, status, reason/action notes |
| M05 | RFQ/quotation oversight | Troubleshooting and moderation without leaking private data |
| M06 | Subscription management (proposed) | Plans, billing/entitlements only if business model confirmed |
| M07 | Appearance/theme manager | Draft palette, preview public+buyer+supplier, contrast validation, publish, rollback |
| M08 | Content management (proposed) | Home banners, featured collections, editorial content |
| M09 | Reports/audit | Activity summaries, filters, exports if permissioned |
| M10 | Settings/access | Roles, system settings, access states |

## F. Global auxiliary screens/states
G01 404/not found; G02 403/access denied; G03 offline/network retry; G04 maintenance/retry; G05 app update notice (mobile where needed); G06 legal consent; G07 file/image upload preview; G08 support/help; G09 notifications center; G10 deep link resolving fallback; G11 image unavailable; G12 empty search results; G13 payment/subscription failure if relevant.

## Navigation architecture
- Public web: floating header; mega categories; search; supplier directory; RFQ explanation; login/account.
- Desktop workspace: collapsible sidebar plus top toolbar; role-specific destinations; breadcrumb and back.
- Buyer mobile tabs (proposed): Home / Explore / RFQ / Inbox / Profile.
- Supplier mobile tabs (proposed): Home / Products / Enquiries / Inbox / Profile.
- Admin is web-first **proposed**, not a restriction on a later mobile admin flow.
- Back returns to previous filters, list and scroll position; deep links request authentication only where necessary.

## Social and related content
- Public share targets: system share, copy URL, WhatsApp, Instagram where device/platform supports it. Do not promise direct posting to Instagram feed or a public API without verification.
- Use canonical HTTPS public URLs and app deep links. Rich link previews require public OG metadata; never leak private RFQ details in previews.
- Cross-link product ↔ supplier, product ↔ category, product → RFQ, RFQ → quote, quote → conversation; related carousels are accessibility-friendly with non-autoplay default except curated decorative bands.

## Open product/business decisions
RFQ broadcast policy and matching algorithm; public/private RFQs; pricing visibility; exact subscription entitlements; who can message whom; supplier verification mechanism; quote acceptance workflow; post-quotation ordering/payments. These are **not approved requirements** until confirmed.


---

## V2 styling attached to every screen contract
Public editorial screens: warm ivory canvas, porcelain cards, 4:5 fabric-focused images, restrained Cormorant Garamond collection headings. Auth: ivory background with thin taupe/sand input outlines and espresso action, no old purple illustration. Buyer/supplier/admin screens: Manrope headings and structured porcelain data modules, semantic status colors. Quote/RFQ forms: visibly labeled fields, accessible contrast and clear progress. Every listed route retains role and data requirements as originally specified; visual updates do not authorize new business rules.


## V2.1 — Inventory corrections (override earlier screen list)

**Existing protected commerce screens must remain in scope**: `/shop/cart`, `/shop/checkout`, `/shop/orders`, `/shop/orders/:id`, `/shop/orders/:id/report`, `/seller/orders`, `/seller/orders/:id`, `/seller/payouts`, `/admin/shipping`, `/session-expired`. Product details retain Add to Cart and optional RFQ, not RFQ-only.

**Current OTP screens**: `/login`, `/register`, `/pending`, `/session-expired`. Password reset/forgot are **NOT APPLICABLE**; role switching requires backend permission redesign. Existing `/` route is an auth redirect, not anonymous public homepage. Newly proposed public landing, supplier directory/showroom, RFQ UI and chat need routes/authorization/APIs as described in `20-SCREEN-TO-CODE-IMPLEMENTATION-MATRIX.md`. Do not claim a backend exists simply because a mockup shows the screen.
