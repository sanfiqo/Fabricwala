# FABRICWALA — BUYER, SUPPLIER, RFQ & QUOTATION FLOWS PROMPT

## GOAL
Design the core business flows of FabricWala with premium usability.

## CORE EXPERIENCE IDEA
Outside:
- premium marketplace

Inside:
- clear business workspace

---

## BUYER FLOW
Key buyer journey:
1. Discover fabrics
2. Open product detail
3. View supplier
4. Save/share products
5. Create RFQ
6. Receive quotations
7. Compare quotations
8. Contact or proceed with supplier

Buyer dashboard should prioritize:
- active RFQs
- quotations received
- saved fabrics
- recent suppliers
- messages
- quick RFQ creation

---

## SUPPLIER FLOW
Key supplier journey:
1. Create account
2. Complete profile
3. Build showroom
4. Add products
5. Receive enquiry or discover RFQ
6. Send quotation
7. Chat with buyer
8. Manage product/business activity

Supplier dashboard should prioritize:
- new enquiries
- RFQ opportunities
- recent quotations
- showroom management
- products
- analytics

---

## SAME APP RULE
Buyer and Supplier must use the same app.
Allow role-aware UI and optional workspace switching.

---

## RFQ CREATION RULE
Use a simple 3-step RFQ flow.

Suggested steps:
1. Fabric requirement
2. Quantity/specifications
3. Supplier preference / summary

If user came from a product page:
- prefill relevant product information

RFQ should be simple and not intimidating.

---

## QUOTATION COMPARISON RULE
Create a premium quotation comparison experience.

Desktop:
- comparison table or structured panel

Mobile:
- swipeable compare cards or stacked compare view

Key points to compare:
- price
- MOQ
- lead time
- sample availability
- notes
- supplier identity
- status

---

## CONNECTED BROWSING RULE
Pages must connect well:
- product → supplier
- supplier → products
- product → related products
- product → similar products
- product → RFQ
- RFQ → quotations
- quotations → supplier chat/showroom

---

## SHARE + SOCIAL RULE
Support:
- product sharing
- supplier showroom sharing
- copy link
- WhatsApp share
- Instagram/share sheet support where appropriate

Show these elegantly, not excessively.

---

## SUPPLIER SHOWROOM RULE
Supplier public page should function like:
- digital showroom
- business profile
- shareable portfolio
- product catalog

It must be premium enough that a supplier would be proud to share it externally.

---

## DASHBOARD VISUAL RULE
Dashboards should be:
- lighter
- practical
- still premium
- easy to scan
- low clutter

Use premium white workspace styling.

---

## DELIVERABLES
Generate:
- buyer flow map
- supplier flow map
- RFQ flow
- quotation comparison flow
- role-switching flow
- share/linking behavior
- dashboard IA

Always maintain FabricWala’s premium identity.


## BUSINESS POLICY CLARIFICATION
Both buyer and supplier use the same app. RFQ is currently discoverable by approved manufacturers, quotation award is implemented, while subscription entitlements and **RFQ-to-payable-custom-order** conversion are not finalized; see 13 before defining these as features.


---

## V2 flow visual consistency
Use Ivory Atelier on both workspaces: porcelain surfaces, Manrope data, espresso primary action and minimal sand dividers. Product pages cross-link to related fabrics, supplier catalog/showroom and RFQ. Quote comparisons prioritize price, MOQ, unit, lead time and supplier, with no invented transaction flow. Direct vs broadcast matching and quote acceptance remain **open business decisions** under 13. Do not implement unverifiable badges, fake stats, checkout, or unsupported Instagram direct-posting.


## V2.1 Implementation Reality Contract — takes precedence over older exploratory wording

- **Verified code baseline (read-only):** `sanfiqo/Fabricwala` `main` at commit `35d0f0e318617e1f8cae2bed82275bbcb7902f24` (retrieved 20 Nov 2026). Source inspection is **not** proof of running behavior; local/unpushed code may differ.
- **Two protected commercial journeys:** retain existing catalog → cart → checkout → paid order → shipment → payout; offer RFQ as an additional sourcing journey only when the `Rfq` feature is enabled. RFQ award gives contacts, **not** a paid order or platform commission.
- **Identity:** mobile OTP / HttpOnly cookie / CSRF, roles `Retailer`, `Manufacturer`, `Admin`. Do not invent password/reset UI, unrestricted role switching or dual role memberships.
- **Unit accuracy:** never assume all goods use meters; support the product's actual API unit and preserve variant/slab/MOQ pricing logic.
- **Visual rules:** Ivory Atelier V2 color intent remains unchanged; treat published token hex values as *candidate* pending visual/contrast review. Preserve Hindi typography fallback and fabric-color accuracy.
- **Scope labels:** `IMPLEMENTED (source)`, `WIRED FRONTEND`, `BACKEND ONLY`, `PROPOSED`, `BLOCKED DECISION`, and `UNVERIFIED RUNTIME` must not be conflated.
- See `19-SOURCE-OF-TRUTH-AND-DECISIONS.md`, `20-SCREEN-TO-CODE-IMPLEMENTATION-MATRIX.md`, `21-API-CONTRACT-RFQ-AND-COMMERCE.md`, `22-PHASED-TASKS-AND-ACCEPTANCE.md`, `23-V2-1-DOCUMENTATION-CONFLICT-AUDIT.md` before using any legacy instructions below. **No implementation authorization is granted by this package.**
