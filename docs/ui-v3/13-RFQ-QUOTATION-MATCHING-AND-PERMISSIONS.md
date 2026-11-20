# 13 — RFQ, Quotation, Matching, Roles and Workflow Detail

## Source-of-truth and caution
The discussion confirms a marketplace with buyers, suppliers, RFQ, quotes, conversations and role-aware UX. It **does not** settle matching algorithm, quote acceptance, order/payment workflow, subscription specifics or public RFQ exposure. Everything below distinguishes proposed UX from pending business policy; no technical schema is assumed.

## Actors and permission matrix (design-level, needs backend confirmation)
| Action | Public | Buyer | Supplier | Admin |
|---|---|---|---|---|
| Browse public products/showrooms | Yes | Yes | Yes | Yes |
| Save products | Sign-in prompt | Yes | If supported | Review only if scoped |
| Create buyer RFQ | Sign-in prompt | Yes | Only if buyer role also granted | No routine creation |
| See private buyer RFQ | No | Owner | Invited/eligible if authorized | Moderation by role |
| Send quotation | No | No | Yes when eligible | No routine quoting |
| Edit supplier catalog | No | No | Own catalog | Policy-controlled moderation |
| Publish global palette | No | No | No | Yes |

## RFQ wizard – candidate field blueprint
Step 1 Fabric: category/material, composition, weave, GSM, width, color/swatches, customization, upload reference; product-specific prefill where known. Step 2 Need: quantity+unit, delivery city/region, target delivery date, optional budget range, additional notes, sample request. Step 3 Recipient/review: choose direct supplier and/or matching visibility **pending approval**, verify privacy, validate data, submit; draft autosave recommended.

## RFQ visibility choices — pending explicit business decision
A. Direct supplier invitation only; B. Discoverable to eligible suppliers only; C. Both, buyer selects visibility at submit. Previously recommended **C**, but not yet explicitly approved. Do not present C as a finalized backend rule. Avoid public exposure of buyer identity, contact or attachments by default.

## RFQ state diagram (candidate)
Draft → Submitted/Open → (Quotations received) → Closed/Cancelled/Expired. Clarifications and edited RFQ versions need explicit handling; never assume editable fields after supplier response. Business-owned status dictionary to be agreed before code.

## Supplier quotation field blueprint
Product/RFQ reference, unit and currency, unit price/tier price, taxes inclusion, freight inclusion, MOQ, validity date, lead time, sample availability/cost, delivery/incoterms where applicable, terms/notes, attachments. Quotation draft/preview before send; submit confirmation; subsequent revisions versioned with timestamp where allowed.

## Buyer quote comparison
Normalize values before showing ranking: unit (m/yard/kg/piece), quantities, currency, tax and freight inclusion. When not comparable, show **Not directly comparable** instead of a fake cheapest label. Mobile compare mode holds attribute labels visible, supports 2–3 quotes and expands terms. Avoid estimated all-in price without complete input.

## Quote acceptance & commercial completion
Accept/reject, order, payment escrow, dispute resolution and delivery tracking are **not scoped/confirmed**. UX should label 'Discuss with Supplier' or 'Continue Conversation' until the business explicitly authorizes order/acceptance functionality.

## Messaging and supplier contact
Conversation thread links to product, enquiry or RFQ; attachment permissions scoped; personal contact or WhatsApp shown only if supplier opted to publish it. Share links for public products/showrooms are public; RFQ sharing defaults private and expires/authorizes as appropriate.

## Subscription/billing
Business plan mentions subscriptions, but exact plan pricing, role gates, free limits and payment provider are undecided. Design a configurable subscription entitlement model without inventing charges. UI needs plan comparison, current status, upgrade/downgrade notices, invoice/payment screens only after billing scope approved.

## Audit-ready questions
1. Can buyers register and become suppliers in the same identity, or require approval?
2. Direct vs broadcast RFQ; what qualifies matching supplier?
3. Is price public, 'price on request', tiered, or mixed by supplier?
4. What quote and RFQ statuses are supported?
5. Can suppliers contact buyers first?
6. Are subscriptions required, and for which actions?
7. Is platform only lead generation or also orders and payments?
8. What verification documents and badge policy apply?

These questions are implementation blockers, not reasons to stop design mockups.


---

## V2 clarity and permissions carry-forward
Visual revision does not change eligibility, recipient privacy, matching strategy, communication permissions or quote acceptance requirements. Supplier contact and verification must be based on real configured privileges/data. All RFQ/quote UI uses high-contrast Manrope business controls against porcelain with espresso primary actions; no marketing visual should conceal confidential buyer details.


## V2.1 — Authoritative implemented RFQ contract (supersedes speculative text above)

Source: `docs/RFQ.md` on the audited repository main branch. **IMPLEMENTED (source)**: approved retailer creates RFQ (maximum 10 simultaneously open), approved manufacturer sees open requests and submits one quotation per RFQ (can revise/withdraw before final), buyer can award exactly one quote (concurrency conflict is `409`), admin can list/close. Request states: **Open**, **Expired (computed, not rewritten)**, **Awarded**, **Cancelled**, **Closed**. Quote states: **Submitted**, **Withdrawn**, **Accepted**, **Rejected**.

Manufacturer sees only delivery city/state before award and only its own quote; manufacturer identity is visible to the retailer with quote, and **winner's contact details are exchanged only upon award**. Supplier-wide public broadcast feed exists: **do not advertise buyer-selected direct invites**. In-app category alerts notify at most 50 matching active manufacturers; no WhatsApp/SMS alerts built. Feature switch `Feature.Rfq` is off by default in production and seeded on for Development. A feature-off endpoint returns `404 feature_off`. No attachments or in-RFQ messages exist.

**Award DOES exist today**; it is not a payment action. After award, platform reveals each side's contact and the transaction continues outside platform; **no custom paid order, platform commission, gateway charge or invoice is created from RFQ**. Follow-up conversion, price/tax enrichment, attachments, direct invites, subscriptions and chat are **PROPOSED** and separately approved. RFQ wizard UX can be 3 steps but submit only API-supported fields; derive detailed DTOs from current OpenAPI/endpoint implementation before code.

For authoritative route matrix and states see `21-API-CONTRACT-RFQ-AND-COMMERCE.md`. This section overrides older statements above saying RFQ status/award are undecided.
