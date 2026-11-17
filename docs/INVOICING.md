# INVOICING.md — the GST invoice

Written 17 Nov 2026 from the code. The PDF is made with QuestPDF (Community license, free below USD 1M yearly revenue; the owner chose it, see DECISIONS.md).

## What is issued
- **Tax invoice** (`SellerToRetailer`): from the seller to the retailer, one per seller order, in the name of the seller. Route: `GET /api/v1/invoices/seller-orders/{id}/download`, for the seller or the retailer of that order.
- **Platform fee statement** (`PlatformCommission`): from the platform to the seller, for the seller only. It says "Not a tax invoice". No GST is charged on the platform fee yet (see the open questions).

## When
Only after the seller order is **Dispatched or Delivered**. Before that the route answers `409 invoice_not_ready`. The invoice is made the first time someone asks for it and is stored; later requests return the same file and the same number. A tax invoice never changes after it is issued (a correction would be a credit note, not built).

## Number
`<seller id>/<financial year>/<running number>`, for example `12/2627/00001`: at most 16 characters, one series per seller per financial year (1 April to 31 March, judged by the date in India), starting again at 1 each April. The next number comes from the `InvoiceSequences` table in the same save as the invoice, protected by a concurrency check, so two requests cannot take the same number; a unique index on the number and on (seller order, type) is the second guard. A failed save uses no number. If a PDF was written to storage and the save then failed, an unreferenced file stays in storage (harmless, small).

## What is printed
Seller and buyer name, GSTIN (the buyer shows "not registered" when it has none), address and state, order number, deliver-to address, **place of supply** (the state of the delivery pincode), each line with description, HSN, quantity, rate, taxable value, GST rate and tax, then taxable total, **CGST + SGST** when the seller's state (the first two digits of the GSTIN) is the place of supply or **IGST** when it is another state, delivery charge, total, and the total in words in lakh and crore.
Every number is copied from the order as frozen at checkout (price, GST rate, GST amount, HSN code); the invoice recalculates nothing. If the order total does not equal the lines, the difference is shown as "Round off" instead of being hidden (it is 0 for every order the checkout makes).

## Refused with a message the seller can act on
| Code | Meaning |
| --- | --- |
| `invoice_not_ready` (409) | not dispatched yet |
| `seller_gstin_missing` (409) | the seller has no valid GSTIN in the profile (checked with the check character) |
| `seller_address_missing` (409) | no registered address with a street line (sign-up only asks for the pincode, so the seller must add it in the address book) |
| `place_of_supply_unknown` (409) | the delivery pincode is not in the directory; load the real directory (F7.1) |
| `payment_gateway_unavailable` | not an invoice error, shown for completeness |

## Tests
`InvoiceTests`: amounts in words, financial year boundaries (IST), number length, state codes for every state in the master data, CGST/SGST against IGST, an odd paisa never lost, round off, a real PDF, numbering per seller and per year, same document on repeat, refusals above, no number used on failure. `RetailerExtrasHttpTests` covers the HTTP route end to end. The look was checked by rendering the page to an image once (not checked on every change).

## Open questions for the owner and an accountant (not decided in code)
1. **GST on delivery.** Checkout charges no GST on freight, so the invoice shows delivery without tax. Freight billed with goods is normally part of the supply and taxed at the goods rate. Needs a decision before launch.
2. **GST on the platform fee.** A marketplace fee is normally taxable at 18%. Today the fee statement carries none and is not a tax invoice. Needs a decision, then a real invoice from the platform's own GSTIN.
3. **Who issues the invoice** if the platform collects the payment (e-commerce operator rules, TCS 1%): this is a legal question, not answered here.
4. **Retailer without GSTIN** is allowed and invoiced as "not registered".
5. Not built: credit notes for refunds, e-invoicing (IRN) for sellers above the turnover threshold, e-way bill, a seller logo, a second language.
6. HSN is optional on products, so a line can print "-". A stricter rule (4 digits above a turnover, 6 above another) needs the seller's turnover.

## Before applying the migration `AddGstInvoicing`
It adds `OrderItems.HsnCode` (empty for old orders, so old invoices show "-"), the `InvoiceSequences` table, shortens `Invoices.InvoiceNumber/InvoiceType` and adds a unique index on (seller order, type). If the database has two invoices of the same type for one seller order, remove the duplicate first:
`SELECT SellerOrderId, InvoiceType, COUNT(*) FROM Invoices GROUP BY SellerOrderId, InvoiceType HAVING COUNT(*) > 1;`
