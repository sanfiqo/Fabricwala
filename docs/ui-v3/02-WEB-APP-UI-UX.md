# FABRICWALA — WEB APP UI/UX PROMPT

## GOAL
Design the complete premium web experience for FabricWala.

## WEB EXPERIENCE PRINCIPLE
The public marketplace should feel:
- premium
- welcoming
- image-led
- discovery-driven

The logged-in workspaces should feel:
- efficient
- business-ready
- clear
- elegant

---

## CORE WEB STRUCTURE

### PUBLIC WEBSITE
Design:
1. Homepage
2. Explore fabrics
3. Categories
4. Product listing
5. Product detail
6. Supplier directory
7. Supplier showroom/public profile
8. RFQ landing / discovery
9. Pricing / membership if needed
10. Help / contact / about

### AUTH
Design:
1. Login
2. Sign up
3. Buyer registration
4. Supplier registration
5. OTP verification / resend / rate-limit feedback (existing authentication)
6. Session expired / retry (existing authentication); password reset is **not applicable** without an approved auth change
7. Verification / status screens

### BUYER WORKSPACE
Design:
1. Buyer dashboard
2. Saved fabrics
3. Saved suppliers
4. My RFQs
5. RFQ detail
6. Quotations received
7. Quotation comparison
8. Messages
9. Notifications
10. Profile/settings

### SUPPLIER WORKSPACE
Design:
1. Supplier dashboard
2. Product management
3. Add/edit product
4. Buyer enquiries
5. RFQ opportunities
6. Quotations sent
7. Showroom manager
8. Analytics
9. Messages
10. Profile/settings

### ADMIN
Design:
1. Overview dashboard
2. Buyer management
3. Supplier management
4. Product moderation
5. Category management
6. RFQ oversight
7. Quotation oversight
8. Appearance/theme management
9. Reports
10. Settings

---

## HOMEPAGE DIRECTION
Homepage must use:
- floating clean header
- strong editorial hero
- search + discovery balance
- featured fabric categories
- curated product sections
- supplier highlights
- RFQ CTA
- trust sections

Structure:
1. floating top header
2. hero with fabric imagery + search
3. category tiles
4. featured fabrics
5. featured suppliers
6. premium story / textile section
7. RFQ CTA block
8. footer

Do not make the homepage too dense.

---

## HEADER
Use:
- floating white or ivory navigation surface
- FabricWala logo
- Explore
- Suppliers
- RFQs
- Search
- Sign in / Account

Navigation should feel premium and light.

---

## PRODUCT LISTING
Listing pages should prioritize:
- fabric imagery
- clear filters
- clean grid
- premium spacing

Show on cards:
- image
- fabric name
- short subtitle/category
- key specs (minimal)
- MOQ or key metric
- supplier name only where useful

Do not overload cards.

Filters may include:
- material
- category
- color
- GSM
- MOQ
- width
- supplier location
- supplier type

Use filter panels that feel elegant and practical.

---

## PRODUCT DETAIL PAGE
Use:
- large imagery
- gallery thumbnails
- zoom
- texture inspection feel
- sticky action area

Show:
- fabric name
- category/subtitle
- supplier name
- supplier trust/status where applicable
- key specs
- price if supported
- MOQ
- color variants
- composition
- GSM
- width
- weave
- lead time where relevant

Primary CTA:
- Request Quotation (optional RFQ feature; preserve Add to Cart for current products)

Secondary CTA:
- Contact Supplier
- Save
- Share

Add sections:
- related products
- more from this supplier
- recently viewed
- similar fabrics

---

## SUPPLIER SHOWROOM
Supplier showroom should be shareable and premium.

Sections:
1. cover/hero
2. supplier identity
3. company summary
4. featured collections
5. product grid
6. about/capabilities
7. contact/social links
8. similar suppliers

Presentation:
- top section editorial
- lower section catalog-driven

---

## WEB DASHBOARD STYLE
Buyer and Supplier dashboards should:
- keep same brand identity
- be simpler than public hero sections
- use premium white work surfaces
- use soft cards
- avoid clutter
- prioritize action over decoration

---

## SHARE + CONNECTED BROWSING
Include:
- related products
- other products by supplier
- scrollable recommendation rows
- share button
- WhatsApp share
- Instagram/share-sheet support where appropriate
- copy link
- internal linking between product, supplier, category, and RFQ flows

---

## ADMIN THEME CONTROL
Admin should have a section for:
- choosing global accent theme
- previewing color system
- publishing theme changes
- keeping structure unchanged

The admin changes only colors, not layout language.

---

## OUTPUT INSTRUCTION
Create:
- web information architecture
- web wireframes
- high-fidelity concepts
- desktop + tablet responsive notes

Preserve premium white editorial identity in all pages.


## COMPLETE PAGE COVERAGE
See 09 for all public, authentication, buyer, supplier, admin and global screens; see 10 for missing states and 11 for breakpoints. These are planned designs, not proof of implementation.


---

## V2 web visual pass and page-specific art direction
Public desktop: floating porcelain navigation on ivory canvas, generous whitespace, refined serif campaign headlines, Manrope filters/specs, elegant 4:5 textile visuals. Hero photography occupies the larger storytelling area while search and RFQ remain accessible. Use open white cards rather than purple-filled banners.
Product details: image gallery, swatches, supplier identity, specifications and espresso Request Quotation CTA; related products/other supplier products and share actions follow with quiet editorial spacing.
Supplier showroom: editorial cover + product catalog; no fashion rental/checkout language.
Dashboards/admin: porcelain cards, thin sand borders, low shadow, no oversized serif KPIs and no cinematic scroll.
All placeholder supplier names, price and performance numbers must be tagged as sample design content. No implementation claims.


## V2.1 Implementation Reality Contract — takes precedence over older exploratory wording

- **Verified code baseline (read-only):** `sanfiqo/Fabricwala` `main` at commit `35d0f0e318617e1f8cae2bed82275bbcb7902f24` (retrieved 20 Nov 2026). Source inspection is **not** proof of running behavior; local/unpushed code may differ.
- **Two protected commercial journeys:** retain existing catalog → cart → checkout → paid order → shipment → payout; offer RFQ as an additional sourcing journey only when the `Rfq` feature is enabled. RFQ award gives contacts, **not** a paid order or platform commission.
- **Identity:** mobile OTP / HttpOnly cookie / CSRF, roles `Retailer`, `Manufacturer`, `Admin`. Do not invent password/reset UI, unrestricted role switching or dual role memberships.
- **Unit accuracy:** never assume all goods use meters; support the product's actual API unit and preserve variant/slab/MOQ pricing logic.
- **Visual rules:** Ivory Atelier V2 color intent remains unchanged; treat published token hex values as *candidate* pending visual/contrast review. Preserve Hindi typography fallback and fabric-color accuracy.
- **Scope labels:** `IMPLEMENTED (source)`, `WIRED FRONTEND`, `BACKEND ONLY`, `PROPOSED`, `BLOCKED DECISION`, and `UNVERIFIED RUNTIME` must not be conflated.
- See `19-SOURCE-OF-TRUTH-AND-DECISIONS.md`, `20-SCREEN-TO-CODE-IMPLEMENTATION-MATRIX.md`, `21-API-CONTRACT-RFQ-AND-COMMERCE.md`, `22-PHASED-TASKS-AND-ACCEPTANCE.md`, `23-V2-1-DOCUMENTATION-CONFLICT-AUDIT.md` before using any legacy instructions below. **No implementation authorization is granted by this package.**
