# 12 — Signature Cinematic Hero: The World of Fabric

## Status
Creative concept approved in discussion; production feasibility, assets, legal rights, and quality benchmarks are **not implemented or verified**. This is a storyboard/implementation brief, not a claim that full cloth simulation is required.

## Creative idea
**The World of Fabric** — From fabric to fashion, across the world. A luminous pearl globe surrounded by physically believable textiles and selected finished garments. **Fabric remains the hero**; apparel is secondary proof of what textiles create. Warm ivory/espresso brand typography remains readable throughout. No neon, sci-fi UI or distracting product-color manipulation.

## Visual assets and distinct physical language
| Type | Visual behavior | Purpose |
|---|---|---|
| Silk saree | Flowing long ribbon, graceful pallu arc, natural silk sheen | Indian textile identity and movement |
| Cotton T-shirt | Garment gently turns, soft cotton folds, no unnatural rubber stretch | Everyday garment destination |
| Suit / blazer | Structured, measured rotation, gravity-respecting shoulder silhouette | Premium tailoring |
| Kurta / dress | Light flutter, fabric weight consistent with material | Variety of textile applications |
| Linen/cotton swatches | Cascading segments, weave close-up as camera passes | Fiber/textile detail |
| Denim | Slower heavier movement, stiffer creases | Material contrast |
| Chiffon | Translucent subtle float, lighter dynamics | Lightness and craft |
| Fabric roll | Controlled slow orbit; true roll geometry | B2B sourcing anchor |

## 6-beat narrative (suggested durations are *timing targets*, not a fixed autoplay video)
1. **Arrival:** Pearl-white globe emerges from soft atmosphere; headline and search remain accessible without waiting for sequence.
2. **Textile Origin:** Natural fabric rolls / swatches unfold and reveal fiber textures.
3. **Fashion Transformation:** Saree, shirt, suit, kurta and dress appear as a few floating accents; no rapid distracting morphing.
4. **Global Connection:** Elements follow distinct orbits around the globe, behind/in front of camera with controlled parallax and depth.
5. **Marketplace Reveal:** Globe reduces visual prominence; discovery search and featured categories become dominant.
6. **Resting state:** Slow ambient orbit or static accessible composition. Do not continuously consume high CPU/GPU.

## Scroll/motion behavior
- Hero entry animation triggered on first view, with content present regardless of animation.
- Optional scroll-linked camera travel and fabric orbit for desktop; **do not hijack scrolling** or lock user in a long pinned sequence.
- Use scroll-triggered mask reveals only in editorial sections. Product cards/detail require reliable tap, swipe and fast load.
- Supply a visible pause/skip when extended animation exists.

## Product-quality implementation ladder
- **Tier 0 (required fallback):** static art-directed poster with text+search as HTML; no video/3D dependency.
- **Tier 1 (recommended MVP):** pre-rendered optimized transparent/opaque loop or animation sequence, responsive crop and reduced-motion poster.
- **Tier 2 (optional enhanced):** real-time 3D orbit with pre-authored cloth meshes/rigs, camera path and lightweight physics approximation.
- **Tier 3 (research-only):** dynamic real-time cloth simulation and cloth-to-garment morphs, pursued only after device benchmarks and art-direction proof.
- Critical: three-dimensional globe should not prevent meaningful access to search/CTAs under poor connectivity or low-performance phones.

## Technology evaluation (not mandatory choices)
- GSAP ScrollTrigger or native Scroll-driven Animations for choreography; WebGL via Three.js/React Three Fiber for enhanced 3D; pre-rendered WebM/MP4/Lottie-like assets where appropriate. Check licensing and actual bundle/performance before selecting.
- Avoid arbitrary third-party runtime calls for hero loading; prefer own hosted assets/CDN.

## Performance/accessibility acceptance targets (proposals)
- Contentful hero copy and search must render without waiting for animation.
- Offer `prefers-reduced-motion` poster fallback; pause on background/offscreen.
- Aim for 60fps on capable desktops, gracefully step down quality on mobile; measure fps and main-thread load across representative devices.
- Asset budgets chosen through measurement, not guessed; aim to preserve Core Web Vitals including LCP, INP and CLS.
- Screen reader reads meaningful hero copy, not dozens of decorative 3D garment objects; art has suitable alternative description where informative.

## Definition of approval
Before build, approve: art direction storyboard + 3 still frames, garment count/physics style, mobile poster, motion intensity, asset-production approach, fallback, and measurable test devices. Until then this is a creative specification.


---

## V2 cinematography override — Ivory Atelier
**Reject purple space/neon Earth renders** shown in earlier exploration; those were illustrative and not approved. Production mood: pearl, matte ivory, subtle warm gold speculars, soft studio shadow and refined depth; globe must not dominate content after the first scene. Keep colored fabric/garments *naturally realistic* (do not turn genuine textile colors beige to match theme). Opening scene should work on a light website rather than requiring a full dark interface. The main copy/search/RFQ CTA remain visually and programmatically accessible; pause, skip, poster and reduced-motion routes mandatory. No claim that physically simulated garments are production-ready.


## V2.1 Implementation Reality Contract — takes precedence over older exploratory wording

- **Verified code baseline (read-only):** `sanfiqo/Fabricwala` `main` at commit `35d0f0e318617e1f8cae2bed82275bbcb7902f24` (retrieved 20 Nov 2026). Source inspection is **not** proof of running behavior; local/unpushed code may differ.
- **Two protected commercial journeys:** retain existing catalog → cart → checkout → paid order → shipment → payout; offer RFQ as an additional sourcing journey only when the `Rfq` feature is enabled. RFQ award gives contacts, **not** a paid order or platform commission.
- **Identity:** mobile OTP / HttpOnly cookie / CSRF, roles `Retailer`, `Manufacturer`, `Admin`. Do not invent password/reset UI, unrestricted role switching or dual role memberships.
- **Unit accuracy:** never assume all goods use meters; support the product's actual API unit and preserve variant/slab/MOQ pricing logic.
- **Visual rules:** Ivory Atelier V2 color intent remains unchanged; treat published token hex values as *candidate* pending visual/contrast review. Preserve Hindi typography fallback and fabric-color accuracy.
- **Scope labels:** `IMPLEMENTED (source)`, `WIRED FRONTEND`, `BACKEND ONLY`, `PROPOSED`, `BLOCKED DECISION`, and `UNVERIFIED RUNTIME` must not be conflated.
- See `19-SOURCE-OF-TRUTH-AND-DECISIONS.md`, `20-SCREEN-TO-CODE-IMPLEMENTATION-MATRIX.md`, `21-API-CONTRACT-RFQ-AND-COMMERCE.md`, `22-PHASED-TASKS-AND-ACCEPTANCE.md`, `23-V2-1-DOCUMENTATION-CONFLICT-AUDIT.md` before using any legacy instructions below. **No implementation authorization is granted by this package.**
