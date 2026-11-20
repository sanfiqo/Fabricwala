# FABRICWALA — MOBILE APP UI/UX PROMPT

## GOAL
Design the complete premium mobile app experience for FabricWala.

## MOBILE PRINCIPLE
The mobile app should feel:
- native
- elegant
- tactile
- premium
- fast
- uncluttered

It must support both:
- Buyer workspace
- Supplier workspace

Same app, role-based experience.

---

## MOBILE CORE NAVIGATION
Use a **floating bottom navigation bar** with premium styling.

Primary buyer nav suggestion:
- Home
- Explore
- RFQ
- Inbox
- Profile

Primary supplier nav suggestion:
- Home
- Products
- Enquiries
- Inbox
- Profile

If backend-supported multiple memberships are approved later, add a workspace selector; existing role is derived from the user/business and must not be changed by the frontend.

---

## MOBILE SCREENS
Design:
1. Splash/welcome
2. Onboarding
3. Login
4. Sign up
5. Buyer setup
6. Supplier setup
7. Home
8. Search/discovery
9. Categories
10. Product listing
11. Product detail
12. Supplier showroom
13. Saved items
14. RFQ creation
15. RFQ status
16. Quotations
17. Quotation comparison
18. Messages
19. Notifications
20. Buyer dashboard
21. Supplier dashboard
22. Product management
23. Profile/settings
24. Role-aware account; workspace switch is future only after permission-model approval
25. Share experience

---

## MOBILE HOME
Home should feel premium and image-led.

Sections:
- compact top bar
- search
- featured hero
- category row
- featured fabrics
- supplier spotlight
- recommendation rows
- RFQ CTA
- recently viewed

Keep the home elegant, not overloaded.

---

## MOBILE PRODUCT LISTING
Use:
- clean cards
- high-quality fabric imagery
- limited visible metadata
- clear filters via bottom sheet
- soft scrolling experience

Avoid dense list overload.

---

## MOBILE PRODUCT DETAIL
Use:
- large hero imagery
- swipeable gallery
- swipeable thumbnails or small thumbnails
- sticky bottom CTA area

Show:
- fabric title
- key textile attributes
- supplier
- trust/status where relevant
- share
- save
- Request Quotation

Also include:
- related fabrics
- more from this supplier
- similar fabrics

---

## SHARE BEHAVIOR
Support:
- native share sheet
- WhatsApp share
- Instagram/share target where supported
- copy link

Do not crowd product cards with too many icons.
Use share primarily on detail pages and supplier showroom.

---

## MOBILE DASHBOARD STYLE
Buyer and supplier dashboards should use:
- compact KPI summaries
- activity cards
- recent actions
- shortcuts
- uncluttered hierarchy

The mobile dashboard should be quick to scan and act from.

---

## INTERACTION STYLE
Use:
- bottom sheets
- swipeable carousels
- sticky CTA
- soft press feedback
- elegant transitions

Use gestures carefully.
Do not make business workflows confusing.

---

## MOBILE VISUAL STYLE
Keep:
- white/ivory dominant
- cocoa/espresso accents
- premium typography
- tactile controls
- selective soft 3D surfaces
- subtle shadows

Do not use overly childish or flashy mobile UI.

---

## OUTPUT INSTRUCTION
Create:
- mobile information architecture
- mobile wireframes
- high-fidelity app concepts
- Android/iOS-friendly interaction notes

Keep the app premium, fast, and business practical.


## ROLE & RESPONSIVENESS
Same buyer/supplier app was agreed. Exact mobile tab arrangement remains a proposal; refer to 11 for safe areas, keyboard, reduced motion and responsive navigation.


---

## V2 mobile art direction
Use the supplied AIDA three-screen layout as a mood reference, not a direct copy. Onboarding has immersive fabric imagery + editorial headline; marketplace home uses light ivory sections and clean catalog cards; product detail uses elegant tall textile imagery, swatches, minimal icon row and an espresso sticky Request Quotation action. Keep the bottom nav short and readable with selected-state contrast, touch targets >=44px, keyboard and safe-area support. Buyer and supplier operate in **one app** through role-gated screens; switching roles is **not implemented** and remains a future backend decision. No purple active tabs or full-screen dark panels by default.
If Instagram share is unsupported on a device/platform, use native share sheet or copy link. Product colors are never altered by theme presets.


## V2.1 Implementation Reality Contract — takes precedence over older exploratory wording

- **Verified code baseline (read-only):** `sanfiqo/Fabricwala` `main` at commit `35d0f0e318617e1f8cae2bed82275bbcb7902f24` (retrieved 20 Nov 2026). Source inspection is **not** proof of running behavior; local/unpushed code may differ.
- **Two protected commercial journeys:** retain existing catalog → cart → checkout → paid order → shipment → payout; offer RFQ as an additional sourcing journey only when the `Rfq` feature is enabled. RFQ award gives contacts, **not** a paid order or platform commission.
- **Identity:** mobile OTP / HttpOnly cookie / CSRF, roles `Retailer`, `Manufacturer`, `Admin`. Do not invent password/reset UI, unrestricted role switching or dual role memberships.
- **Unit accuracy:** never assume all goods use meters; support the product's actual API unit and preserve variant/slab/MOQ pricing logic.
- **Visual rules:** Ivory Atelier V2 color intent remains unchanged; treat published token hex values as *candidate* pending visual/contrast review. Preserve Hindi typography fallback and fabric-color accuracy.
- **Scope labels:** `IMPLEMENTED (source)`, `WIRED FRONTEND`, `BACKEND ONLY`, `PROPOSED`, `BLOCKED DECISION`, and `UNVERIFIED RUNTIME` must not be conflated.
- See `19-SOURCE-OF-TRUTH-AND-DECISIONS.md`, `20-SCREEN-TO-CODE-IMPLEMENTATION-MATRIX.md`, `21-API-CONTRACT-RFQ-AND-COMMERCE.md`, `22-PHASED-TASKS-AND-ACCEPTANCE.md`, `23-V2-1-DOCUMENTATION-CONFLICT-AUDIT.md` before using any legacy instructions below. **No implementation authorization is granted by this package.**


## V2.2 Mobile Feel — overrides generic mobile styling above (full spec: file 24)
Target: the 2026 native-app feel shown in three user references (frosted-glass portfolio, soft-tactile calm app, blurred-backdrop outfit-picker): **light, airy, glassy, tactile, one dark pill CTA per screen**.
- Screens sit on an **ambient backdrop** (soft champagne/pearl light), not a flat fill.
- **Floating glass tab bar** (pill, inset 16px, safe-area aware): buyer Home/Explore/RFQ/Inbox/Profile; supplier Home/Products/Enquiries/Inbox/Profile. Selected tab = espresso pill with white label.
- **Header:** 44px round white icon buttons left/right, centred serif screen title (Cormorant, 20px). Large left-aligned serif title only on Home/Explore/collection screens.
- **Primary action:** a single sticky dark capsule CTA (56px) above the safe area, over a scrim. Secondary actions are glass or outlined.
- **Choices and options open as a glass bottom sheet** over a blurred, dimmed screen (quantity/slab, filters, sort, quote actions, workspace selector). Sheet has a grabber, close button, row items, one CTA. Never a centred desktop-style modal on mobile.
- **Lists/collections:** horizontal snap rails with peek of next card; vertical lists use row items.
- **Data moments** (quotes received, price trend, order status): one pillow tile with inset bar rails; no chart junk.
- Motion: spring on sheet open and press release (<= 400ms), staggered entrance (60ms steps, once per screen), all disabled under reduced-motion. Never delay a workflow for animation.
- Fabric photos are never glassed over, tinted or filtered; glass labels sit on the lower edge of the image, not across it.
- Performance: max 3 simultaneous backdrop-filter layers on screen; fall back to solid porcelain (`.96`) when unsupported, on low-end devices or `prefers-reduced-transparency`.
- Protected: catalogue -> cart -> checkout, OTP auth, roles and RFQ award behaviour are unchanged; this is a visual layer only.


## V3.0 SUPERSEDES THIS FILE'S STYLE — see `25-BW-SOURCING-UI-SPEC.md` (20 Nov 2026)
Owner approved the **black & white Alibaba-style sourcing UI**. All Ivory/espresso, Lagoon, pastel, glass, neumorphism, lime/Weave and accent-colour guidance in this file is **retired** (history only; old CSS in `css/archive/`). Current tokens: `css/fabricwala-bw.css`. Colour appears only in fabric photographs (and semantic status colours).
