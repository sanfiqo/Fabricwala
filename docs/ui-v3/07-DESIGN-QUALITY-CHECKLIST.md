# FABRICWALA — DESIGN QUALITY CHECKLIST PROMPT

## GOAL
Evaluate every FabricWala screen and component against this checklist.

---

## 1. PREMIUM FEEL
- Does the screen feel premium?
- Does it avoid generic SaaS styling?
- Does it preserve white editorial luxury?
- Is the visual hierarchy calm and refined?

## 2. FABRIC-FIRST EXPERIENCE
- Are fabrics the hero?
- Are product images respected?
- Are textile colors preserved accurately?
- Does the UI support textile discovery clearly?

## 3. BRAND CONSISTENCY
- Does the screen match FabricWala’s approved identity?
- Are colors, shadows, radii, spacing, and typography consistent?
- Is admin theme control supported without breaking structure?

## 4. CLARITY
- Is the page easy to understand?
- Are primary actions clear?
- Is the page uncluttered?
- Are forms easy to complete?

## 5. BUSINESS PRACTICALITY
- Does the flow support real buyer/supplier needs?
- Is “Request Quotation” handled clearly?
- Are supplier and RFQ workflows practical?
- Is product/business information accessible?

## 6. CARD QUALITY
- Are cards premium and not overloaded?
- Are product cards image-first?
- Are supplier cards trustworthy?
- Are RFQ cards structured?

## 7. MOTION QUALITY
- Is animation purposeful?
- Is it premium, subtle, and smooth?
- Is motion lighter in dashboards and forms?
- Are rich effects limited to storytelling/special sections?

## 8. MOBILE QUALITY
- Is it touch-friendly?
- Is bottom navigation clear?
- Are sticky actions useful?
- Are product details immersive but efficient?

## 9. ACCESSIBILITY
- Is text readable?
- Is contrast sufficient?
- Are targets tap-friendly?
- Is reduced motion considered?

## 10. FINAL DECISION TEST
Ask:
- Would this screen look good in a premium design case study?
- Would a supplier proudly share this?
- Would a buyer trust it?
- Does it still feel like FabricWala?

---

## REJECTION RULES
Reject any output that:
- looks generic
- feels too busy
- is too flat and boring
- is too dark-heavy
- overuses gradients
- overuses glass/neumorphism
- hides business actions
- feels like a generic clothing shopping app
- loses FabricWala’s premium textile-marketplace identity

---

## FINAL INSTRUCTION
Before approving any design, review it against all checklist items above.

If something feels visually inconsistent, generic, unclear, or not premium enough, revise it before approval.


## EVIDENCE-BASED AUDIT
See 15 for measurable criteria and 16 for documentation-only audit. Spec existence is not equal to verified implementation.


---

## V2 explicit visual rejection criteria
Reject renderings with purple/navy neon hero, cocoa-filled dashboard, gradient buttons, fashion-rental checkout/Book Dress, heavy 3D controls, generic Bootstrap card clutter, unreadable decorative serif forms, altered fabric color or fake supplier metrics.
Acceptance requires side-by-side comparison with user-provided AIDA image for airy ivory mood (without copying its brand), V2 palette validation (08), layout/role coverage (09), motion fallback (12), and per-screen measurable QA (15). Mark high-fidelity art **unapproved** until reviewed; this text does not certify production readiness.


## V2.1 Implementation Reality Contract — takes precedence over older exploratory wording

- **Verified code baseline (read-only):** `sanfiqo/Fabricwala` `main` at commit `35d0f0e318617e1f8cae2bed82275bbcb7902f24` (retrieved 20 Nov 2026). Source inspection is **not** proof of running behavior; local/unpushed code may differ.
- **Two protected commercial journeys:** retain existing catalog → cart → checkout → paid order → shipment → payout; offer RFQ as an additional sourcing journey only when the `Rfq` feature is enabled. RFQ award gives contacts, **not** a paid order or platform commission.
- **Identity:** mobile OTP / HttpOnly cookie / CSRF, roles `Retailer`, `Manufacturer`, `Admin`. Do not invent password/reset UI, unrestricted role switching or dual role memberships.
- **Unit accuracy:** never assume all goods use meters; support the product's actual API unit and preserve variant/slab/MOQ pricing logic.
- **Visual rules:** Ivory Atelier V2 color intent remains unchanged; treat published token hex values as *candidate* pending visual/contrast review. Preserve Hindi typography fallback and fabric-color accuracy.
- **Scope labels:** `IMPLEMENTED (source)`, `WIRED FRONTEND`, `BACKEND ONLY`, `PROPOSED`, `BLOCKED DECISION`, and `UNVERIFIED RUNTIME` must not be conflated.
- See `19-SOURCE-OF-TRUTH-AND-DECISIONS.md`, `20-SCREEN-TO-CODE-IMPLEMENTATION-MATRIX.md`, `21-API-CONTRACT-RFQ-AND-COMMERCE.md`, `22-PHASED-TASKS-AND-ACCEPTANCE.md`, `23-V2-1-DOCUMENTATION-CONFLICT-AUDIT.md` before using any legacy instructions below. **No implementation authorization is granted by this package.**
