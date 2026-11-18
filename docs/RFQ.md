# RFQ.md — requests for quotation (reverse marketplace)

Built 18 Nov 2026 as item B2B-2 of `FABRICWALA_B2B_B2C_C2C_OPTIONS.md`. It replaces the stub that was deleted in F6.1: this one is real and tested.
It is behind the feature switch **Rfq**, which is **off** until the owner switches it on (see section 6).

## 1. The idea
A retailer says what it needs ("500 school sweaters in navy, delivered to Indore"). Manufacturers who are interested send a quotation (price for one piece, how many they can make, days to deliver, a note). The retailer compares and accepts one. Both sides are then shown each other's contact.

## 2. States
| Request | Meaning |
| --- | --- |
| Open | waiting for quotations until its end date (1 to 30 days, 14 by default) |
| Expired | shown when an Open request has passed its end date; the row is not rewritten |
| Awarded | the retailer accepted a quotation |
| Cancelled | the retailer cancelled it |
| Closed | an admin closed it (for example it is not a textile request) |

| Quotation | Meaning |
| --- | --- |
| Submitted | sent, waiting; the manufacturer can change or withdraw it |
| Withdrawn | the manufacturer took it back; the retailer no longer sees it; it can be sent again |
| Accepted | chosen |
| Rejected | another one was chosen, or the request was cancelled or closed |

## 3. Rules (each has a test in `RfqHttpTests`)
- Only an approved **retailer** asks, only an approved **manufacturer** quotes, an admin lists and closes.
- A retailer may have **10 open requests** at the same time (`rfq_limit_reached`, 409); expired ones do not count.
- A manufacturer has **one quotation per request**; sending again changes it. A quotation that is Accepted or Rejected is final (`quotation_final`).
- A quotation offers at most the quantity that was asked, price above 0, lead time 1 to 365 days.
- Manufacturers see the request with the delivery **city and state** only. They do not see who the buyer is until their quotation is accepted; then they see the name and phone of the buyer. The retailer sees every quotation (cheapest first) with the name of the manufacturer, and the contact of the winner after the decision.
- A manufacturer sees **only their own** quotation, never those of competitors.
- The status is a concurrency check: two accept calls at the same moment award one quotation and the other gets `409` (tested on MySQL).
- An Open request is visible to every manufacturer; a closed one only to those who quoted on it. Somebody else's request answers 404.
- Every quotation, award, cancellation and close writes an in-app notification to the people of the other business; award, cancel and close are in the audit log.

## 4. Routes (the exact shapes are in `API_REFERENCE.md` and `openapi.json`)
| Who | Route |
| --- | --- |
| Retailer | `POST /api/v1/rfqs`, `GET /api/v1/rfqs?status=&page=&pageSize=`, `GET /api/v1/rfqs/{id}`, `POST /api/v1/rfqs/{id}/cancel`, `POST /api/v1/rfqs/{id}/quotations/{quotationId}/accept` |
| Manufacturer | `GET /api/v1/seller/rfqs?categoryId=&q=` (open requests), `GET /api/v1/seller/rfqs/{id}`, `PUT /api/v1/seller/rfqs/{id}/quotation`, `DELETE /api/v1/seller/rfqs/{id}/quotation`, `GET /api/v1/seller/quotations?status=` |
| Admin | `GET /api/v1/admin/rfqs?status=`, `POST /api/v1/admin/rfqs/{id}/close` |
| Everyone | `GET /api/v1/features` (which optional parts are on) |
| Admin | `PUT /api/v1/admin/features/{name}` with `{ "on": true }` |

Errors use the usual `{ code, message, fields }`. Codes: `feature_off` (404), `validation_failed` (400, with `fields`), `pincode_invalid`, `pincode_unknown`, `rfq_limit_reached`, `rfq_not_open`, `quotation_not_available`, `quotation_final`, `rfq_changed` (all 409).

## 5. What is not built (on purpose)
- **No payable order comes out of an accepted quotation.** A request is for goods that are often not in the catalogue, and an order line needs a catalogue product and variant. Today the two businesses are given each other's contact and deal directly. That means the platform earns no commission on these deals. Turning a quotation into an order (a "custom order" with its own lines, payment, delivery and invoice) is the next step, and a decision for the owner: whether the commission applies and how the order lines are described.
- No attachments (tech pack, photos) and no messages inside the request. B2B-4 and B2B-9 in the plan add milestones and files.
- Alerts go only to manufacturers who have an active product in the category of the request (at most 50, in-app notification); a request without a category is found in the feed only. There is no WhatsApp or SMS alert yet.
- No limit on how many requests a retailer makes in a day beyond the 10 open ones.

## 6. Switching it on
`PUT /api/v1/admin/features/Rfq` with `{ "on": true }` as an admin (it is written to the audit log), or in a database: the platform setting `Feature.Rfq` = `on`. Development seeds it on. Before the owner switches it on in a live system: decide what the support and grievance process is for disputes between a retailer and a manufacturer, and tell both sides in the terms that the platform only connects them for these deals.
Tables: `Rfqs`, `RfqQuotations` (migration `AddRfq`; see `DATA_MODEL.md`).
