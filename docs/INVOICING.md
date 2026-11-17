# INVOICING.md — the GST invoice

Written 17 Nov 2026 from the code. The PDF is made with QuestPDF (Community license, free below USD 1M yearly revenue; the owner chose it, see DECISIONS.md).

## What is issued
- **Tax invoice** (`SellerToRetailer`): from the seller to the retailer, one per seller order, in the name of the seller. Route: `GET /api/v1/invoices/seller-orders/{id}/download`, for the seller or the retailer of that order.
- **Platform fee invoice** (`PlatformCommission`): from the platform to the seller, for the seller only.
  - When the order carries GST on the fee (the platform company was set up when the order was placed) it is a **tax invoice** in its own series `PF/<financial year>/<number>`: one line "Platform fee for order ...", service code 998599 (to be confirmed with an accountant), taxable value = the fee, GST 18% (frozen on the order), CGST+SGST when the platform GSTIN and the seller GSTIN are in the same state, else IGST. The place of supply is the state of the seller GSTIN. Like the seller invoice it needs a dispatched order, a valid seller GSTIN and a seller street address.
  - For an order without GST on the fee it is the plain "Platform fee statement", marked "Not a tax invoice".
  - The platform company is entered by the admin: `GET/PUT /api/v1/admin/platform-company` (legal name, GSTIN checked with the check character, address, pincode that must be in the directory, GST rate). The change is written to the audit log. Until it is complete, new orders carry no GST on the fee and a fee invoice for an order that does carry it answers `409 platform_details_missing`.

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
| `platform_details_missing` (409) | fee invoice only: the platform company is not set up in the admin settings |

## Tests
`InvoiceTests`: amounts in words, financial year boundaries (IST), number length, state codes for every state in the master data, CGST/SGST against IGST, an odd paisa never lost, round off, a real PDF, numbering per seller and per year, same document on repeat, refusals above, no number used on failure. `RetailerExtrasHttpTests` covers the HTTP route end to end. The look was checked by rendering the page to an image once (not checked on every change).

## Open questions for the owner and an accountant (not decided in code)
1. **GST on delivery: decided to keep none** (17 Nov 2026), although freight billed with goods is normally taxed at the goods rate. Confirm with an accountant before launch; changing it later means changing checkout, the order totals and this invoice.
2. **GST on the platform fee: decided, 18%** (17 Nov 2026). Left to confirm with an accountant: the service code (998599), the rate, and whether the platform may take the GST from the payout this way.
3. **Who issues the invoice** if the platform collects the payment (e-commerce operator rules, TCS 1%): this is a legal question, not answered here.
4. **Retailer without GSTIN** is allowed and invoiced as "not registered".
5. Not built: credit notes for refunds, e-invoicing (IRN) for sellers above the turnover threshold, e-way bill, a seller logo, a second language.
6. HSN is optional on products, so a line can print "-". A stricter rule (4 digits above a turnover, 6 above another) needs the seller's turnover.

## Before applying the migration `AddGstInvoicing`
It adds `SellerOrders.CommissionGst` (0 for old orders, so their payouts do not change), `OrderItems.HsnCode` (empty for old orders, so old invoices show "-"), the `InvoiceSequences` table, shortens `Invoices.InvoiceNumber/InvoiceType` and adds a unique index on (seller order, type). If the database has two invoices of the same type for one seller order, remove the duplicate first:
`SELECT SellerOrderId, InvoiceType, COUNT(*) FROM Invoices GROUP BY SellerOrderId, InvoiceType HAVING COUNT(*) > 1;`
