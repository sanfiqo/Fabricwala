# Decisions

- Brand remains `TextileConnect` as the placeholder.
- Manufacturer pays commission: `Commission.DefaultRate = 2%`; retailer platform fee is `0`.
- Settlement auto-release default is `7` days.
- Development uses fake OTP, payment, and local private-file implementations; production adapters require secrets/accounts.
- Collective buying and logistics remain intentionally minimal until the post-MVP phase.
- The frontend contract's explicit `{ token }` CSRF response and expanded `auth/me` profile are authoritative; existing backend domain models remain the source for product and order persistence until their full detail DTOs are implemented.

## 17 Nov 2026 - Commission bearer
Decided by the owner: the **seller bears the commission**. The retailer pays goods + GST + freight; the commission is taken from the seller payout. This is what the code already does (see SETTLEMENT.md), so no code change.

## 17 Nov 2026 - Payout delay
Decided by the owner: keep the current rule. Payout is released when the retailer confirms delivery, or 7 days after dispatch, and is held while a problem report is open.

## 17 Nov 2026 - PDF library
Decided by the owner: QuestPDF for the GST invoice (Community license, free below USD 1M yearly revenue; revisit the license when revenue passes it).

## 17 Nov 2026 - Log sink and error tracker
Decided by the owner: Sentry for errors and Better Stack for logs. Needs an account and a DSN or source token from the owner, to be kept in secrets; not wired yet.

## 17 Nov 2026 - Pincode source
Decided by the owner: the India Post all-India pincode directory from data.gov.in. The owner downloads the CSV; then `import-pincodes <csv>` loads it and the counts are pinned (F7.1).

## 17 Nov 2026 - Brand
Decided by the owner: the public name is **Fabricwala**. The domain is still to be bought by the owner. Code and project names (TextileConnect) stay as they are.

## 17 Nov 2026 - GST on delivery
Decided by the owner: keep the current rule, no GST on the delivery charge. Risk noted: freight billed with goods is normally taxed at the goods rate, so the seller could be questioned by the tax office. To be confirmed with an accountant before launch.

## 17 Nov 2026 - GST on the platform fee
Decided by the owner: charge 18% GST on the platform fee and issue a platform tax invoice to the seller (fee + GST taken from the payout). Needs the platform company legal name, GSTIN and address; not built yet.

## 18 Nov 2026 - Requests for quotation come back (chosen by Claude, to confirm)
The owner deleted the RFQ stub in F6 and later asked Claude to build the planned features one by one without questions. Claude therefore built B2B-2 (docs/RFQ.md) as a real feature behind the switch `Feature.Rfq`, which stays off until the owner turns it on. An accepted quotation does not become a payable order yet: the owner has to decide about commission on these deals.
