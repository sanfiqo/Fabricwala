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
