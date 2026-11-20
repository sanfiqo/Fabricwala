# 21 — API Contract: Existing Commerce and RFQ

**Evidence:** `sanfiqo/Fabricwala / main / 35d0f0e318617e1f8cae2bed82275bbcb7902f24`, especially `docs/RFQ.md`, `client/src/App.tsx`, `client/src/api/areas/*`, `docs/ARCHITECTURE.md`. Route lists summarized from repository RFQ documentation. **Request/response JSON must be verified against `docs/openapi.json` or endpoint DTO files before implementation.** None of the listed backend behavior was re-executed here.

## A. Existing commerce — protected path
Authenticated retailer: catalog/list/detail → server-price cart/variant → checkout with idempotency key → payment → seller orders → accept/reject/dispatch → buyer delivery confirm → payout hold/release and refund/report path. Preserve policies: approved business, server-owned price/stock, MOQ/slabs, GST/commission, role+business isolation, CSRF cookie, idempotent webhooks. Frontend already has `client/src/api/areas/catalog.ts`, `cart.ts`, `orders.ts`, `seller.ts`, `payouts.ts` and protected screens. Visual redesign is not permission to replace this flow.

## B. Implemented RFQ (behind feature switch)
| Actor | API route (under `/api/v1`) | UI expectation / controls |
|---|---|---|
| Retailer | `POST /rfqs` | Create request, validate server-supported fields |
| Retailer | `GET /rfqs?status=&page=&pageSize=` | Own RFQs with computed expiry |
| Retailer | `GET /rfqs/{id}` | Own detail, own quote list |
| Retailer | `POST /rfqs/{id}/cancel` | Cancel with confirmation; state/authorization enforced |
| Retailer | `POST /rfqs/{id}/quotations/{quotationId}/accept` | Award one quote; 409 race handling; contacts revealed on award |
| Manufacturer | `GET /seller/rfqs?categoryId=&q=` | Approved manufacturer open RFQ feed, city/state only |
| Manufacturer | `GET /seller/rfqs/{id}` | Eligible detail, no buyer identity before award |
| Manufacturer | `PUT /seller/rfqs/{id}/quotation` | One quote per request; revise while not final |
| Manufacturer | `DELETE /seller/rfqs/{id}/quotation` | Withdraw quote |
| Manufacturer | `GET /seller/quotations?status=` | Manufacturer's own quotes only |
| Admin | `GET /admin/rfqs?status=` | Moderation list |
| Admin | `POST /admin/rfqs/{id}/close` | Close request with audit trail |
| Everyone | `GET /features` | Check enabled features |
| Admin | `PUT /admin/features/{name}` | Only admin toggles feature |

### Exact existing states
- Request: `Open`; `Expired` is computed when open request passes end date (row not rewritten); `Awarded`; `Cancelled`; `Closed`.
- Quote: `Submitted`; `Withdrawn`; `Accepted`; `Rejected`.
- Maximum retailer open RFQs: **10**; manufacturer **1 quotation per RFQ**; quote price > 0; lead time **1–365 days**; offered quantity no more than asked quantity; open duration **1–30 days**, default 14.
- Manufacturer sees delivery city and state, not buyer name/contact until win; manufacturer never sees competitor quotes. Retailer sees manufacturer identity with quotes, and winner contacts after award. 409 on concurrent awards; audit log and notifications per `docs/RFQ.md`.
- Open requests visible to approved manufacturers; matching-category notification to up to 50 manufacturers with active product. **Not a direct-invitation system.**
- `Feature.Rfq` is off unless admin enables it, except Development seeds on. UI must not expose active RFQ controls if feature is off. Catch `404 feature_off` and guide to catalog; no blanket treatment of all 404s as feature-off.
- RFQ award **does not** create catalog/custom order, collect money, calculate commission or create invoice. UI copy must read 'Quotation accepted / contact supplier' not 'Paid / Order placed'.

### UI adapter design (no code yet)
New `client/src/api/areas/rfq.ts` should use existing fetch/CSRF/error utilities from `client/src/api/client.ts`; typed DTOs from authoritative API; query keys scoped by role/business and feature flag; no mock interface claiming back-end-supported fields absent from DTO. Add grouped pages: `shop/rfqs`, `seller/rfqs`, `admin/rfqs`; role `Gate` protection and feature disabled states. Price/tax comparisons must reflect data availability; no invented all-in quote.

### Explicitly unimplemented/independent future features
Direct invite list; uploaded spec/reference files; in-RFQ chat and private document sharing; custom payable orders; payment/commission for awarded quotes; Instagram auto-publication; subscriptions and pricing plans. These require independent contracts/approval.

## C. Data correctness checklist
1. Read OpenAPI/DTO before UI types. Do not copy speculative 3-step wizard fields into API requests.
2. Reuse backend errors: `feature_off`, `rfq_limit_reached`, `rfq_not_open`, `quotation_not_available`, `quotation_final`, `rfq_changed`, `pincode_invalid`, `pincode_unknown`.
3. Preserve `Retailer`/`Manufacturer` permissions and business isolation; do not let cards or share previews leak buyer identity.
4. Verify quantity unit conversion before comparing quotes; never implicitly display all products as meters.
5. Ensure no success message before backend confirms award; handle 409 and refresh list/detail.
