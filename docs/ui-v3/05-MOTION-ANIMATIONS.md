# FABRICWALA — MOTION & ANIMATION PROMPT

## GOAL
Define a premium motion system for FabricWala that feels advanced, cinematic, and refined without harming usability.

## PRINCIPLE
Motion should:
- guide attention
- create delight
- reinforce quality
- support storytelling
- never slow down workflows

---

## APPROVED MOTION TYPES

### 1. SCROLL REVEAL
Use for:
- homepage sections
- featured collections
- supplier highlights
- premium storytelling blocks

Behavior:
- fade + translate
- soft scale
- image reveal masks
- elegant sequencing

### 2. PARALLAX
Use selectively for:
- homepage hero
- featured editorial sections
- premium banners

Do not use heavily in dashboards or forms.

### 3. HORIZONTAL SCROLL STORYTELLING
Use for:
- special fabric collection showcase
- lookbook-style sections
- premium hero sequences

### 4. AUTO-MOVING CAROUSELS / MARQUEES
Use subtly for:
- fabric swatch strips
- partner/supplier/logo bands
- selected promotional rows

Do not overuse.

### 5. SHARED ELEMENT TRANSITIONS
Use for:
- product card to product detail
- supplier card to supplier showroom

These transitions should make the app feel premium and connected.

### 6. SOFT TACTILE MICRO-INTERACTIONS
Use for:
- button presses
- toggle switches
- chips
- save/favorite
- tab switching
- bottom navigation states

### 7. STICKY / PINNED SECTIONS
Use for:
- editorial storytelling
- premium collection walkthroughs
- selected homepage sequences

### 8. IMAGE MASK REVEALS
Use for:
- textile imagery
- collection intros
- hero storytelling
- onboarding visuals

---

## SPECIAL SIGNATURE HERO MOTION
Optional premium signature section:

**The World of Fabric**

Concept:
- a refined globe
- flowing cloth/fabric moving around the globe
- multiple textile forms (saree, tshirt, suit, fabric swatches, etc.)
- soft cinematic movement
- luxury lighting
- elegant entrance/reveal

Use only as:
- homepage hero
- brand campaign section
- special storytelling area

Do not use throughout the app.

---

## MOTION SAFETY RULES
Avoid:
- constant distracting motion
- exaggerated bouncing
- heavy autoplay everywhere
- slow transitions in dashboards
- motion that harms readability

---

## PERFORMANCE RULES
Motion must be:
- smooth
- optimized
- layered carefully
- respectful of weaker devices

Provide reduced-motion alternatives.

---

## WHERE TO USE LESS MOTION
Use minimal motion in:
- dashboards
- chat
- data tables
- RFQ forms
- admin workflows

In these areas, prefer fast and subtle feedback only.

---

## WHERE TO USE MORE MOTION
Use richer motion in:
- homepage hero
- onboarding
- featured collections
- storytelling sections
- premium supplier showcase
- product detail transitions

---

## DELIVERABLES
Generate:
- motion principles
- screen-by-screen motion map
- micro-interaction library
- performance guidelines
- reduced motion rules

Keep FabricWala premium, not gimmicky.


## HERO APPROVAL CLARIFICATION
The World of Fabric is an approved **creative concept**, not a requirement to ship intensive real-time simulation. See 12 for storyboard, fallback tiers, performance and approval gates.


---

## V2 motion art direction
AIDA-inspired *quiet luxury* motion: gentle mask reveals, small editorial parallax, tasteful horizontal galleries, smooth textile card-to-detail continuity. The user's chosen globe concept is a single signature story: pearl globe, sari/suit/T-shirt/kurta and textile swatches with plausible material physics, warm natural light, no neon and no purple-glow sci-fi rendering.
Actual technical delivery tiers, pause/skip and low-end device fallbacks are in 12. No smooth-scroll hijack, no unavoidable autoplay, reduced-motion must preserve content and search functionality. Forms/analytics/chat use immediate functional feedback only.


## V2.1 Implementation Reality Contract — takes precedence over older exploratory wording

- **Verified code baseline (read-only):** `sanfiqo/Fabricwala` `main` at commit `35d0f0e318617e1f8cae2bed82275bbcb7902f24` (retrieved 20 Nov 2026). Source inspection is **not** proof of running behavior; local/unpushed code may differ.
- **Two protected commercial journeys:** retain existing catalog → cart → checkout → paid order → shipment → payout; offer RFQ as an additional sourcing journey only when the `Rfq` feature is enabled. RFQ award gives contacts, **not** a paid order or platform commission.
- **Identity:** mobile OTP / HttpOnly cookie / CSRF, roles `Retailer`, `Manufacturer`, `Admin`. Do not invent password/reset UI, unrestricted role switching or dual role memberships.
- **Unit accuracy:** never assume all goods use meters; support the product's actual API unit and preserve variant/slab/MOQ pricing logic.
- **Visual rules:** Ivory Atelier V2 color intent remains unchanged; treat published token hex values as *candidate* pending visual/contrast review. Preserve Hindi typography fallback and fabric-color accuracy.
- **Scope labels:** `IMPLEMENTED (source)`, `WIRED FRONTEND`, `BACKEND ONLY`, `PROPOSED`, `BLOCKED DECISION`, and `UNVERIFIED RUNTIME` must not be conflated.
- See `19-SOURCE-OF-TRUTH-AND-DECISIONS.md`, `20-SCREEN-TO-CODE-IMPLEMENTATION-MATRIX.md`, `21-API-CONTRACT-RFQ-AND-COMMERCE.md`, `22-PHASED-TASKS-AND-ACCEPTANCE.md`, `23-V2-1-DOCUMENTATION-CONFLICT-AUDIT.md` before using any legacy instructions below. **No implementation authorization is granted by this package.**
