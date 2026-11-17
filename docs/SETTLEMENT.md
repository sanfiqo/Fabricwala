# Settlement rules

How money is split and when the seller is paid. Every rule below has a test (the file name is in brackets). Where the code does less than the rule needs, it is marked **Gap**.

## 1. Who pays what

- The retailer pays one amount per order: goods + GST + freight. The platform fee is never added on top.
- **Commission** = goods subtotal x rate, charged to the seller and taken from the seller's payout. GST and freight are not part of the base. [SettlementRulesTests]
- The rate is the seller's own rate (`Business.CommissionRateOverride`) if there is one, else the platform setting `Commission.DefaultRate` (0.02 if the setting is missing). It is fixed on the seller order when the order is placed; a later change of the setting affects new orders only. [SettlementRulesTests]
- Example: 100 pieces at 480 = 48,000 goods, 5% GST = 2,400, freight 2,000. The retailer pays 52,400. Commission at 2% = 960. The seller's payout is 51,440.
- **[OWNER DECISION]** Who bears the commission (today: the seller) and the default rate are still open in `PHASES.md`.

## 2. Rounding

One rule everywhere: round to 2 decimals, a half goes **away from zero** (`ProductQuote.Money`). It is used for GST, commission and every line. Razorpay amounts are whole paise, converted with the same rule (`RazorpayPaymentGateway.Paise`). [SettlementRulesTests]

## 3. The payout record

When the retailer's payment is confirmed, each seller order gets one `Settlement`: `Amount` = seller order total - commission, `CommissionAmount`, status **Pending**, and `ReleaseAfter` = `Settlements.NoReleaseDate` (no date yet). The order must be paid before the seller can act on it.

Status values (`SettlementStatus`): Pending (held), Released, Cancelled.

## 4. When the payout can be released

| Event | What happens to `ReleaseAfter` |
| --- | --- |
| Payment confirmed | no date (held) |
| Seller dispatches | set to dispatch time + `Settlement.AutoReleaseDays` (platform setting, default 7, allowed 1 to 90), if that is earlier than the current value [SettlementRulesTests] |
| Retailer confirms delivery | set to now (release at the next run) |
| Seller marks the shipment | no change: the seller can only add progress notes (InTransit, ReachedCity), never delivery |

A job runs every minute (`OrderMaintenanceWorker`). It releases every settlement that is Pending, whose `ReleaseAfter` has passed, and that has **no open claim** (`ReturnClaim.ResolvedAtUtc` is null). Released sets `SettledAt` and tells the seller. [PayoutHttpTests]

**Gap:** release only changes the status. No money moves to the seller yet. The Razorpay Route transfer is task F3.5.

## 5. Cancelled and rejected orders

A seller order that is rejected by the seller, or cancelled because it was not accepted within 24 hours (`Seller.AcceptWindowHours`) or not paid within 60 minutes (`Orders.UnpaidExpiryMinutes`):
- its settlement becomes **Cancelled**;
- stock goes back on the shelf;
- if it was paid, a full refund of that seller order's total is requested (goods + GST + freight) and processed through the gateway; a failed refund stays `Requested` and is retried every minute;
- the order's payment status becomes PartiallyRefunded, or Refunded when every seller order in it is dead;
- a payment that arrives after the order was cancelled is refunded, not kept. [SellerOrderHttpTests, JourneyVariantsHttpTests]

## 6. Problem reports (claims)

- A retailer can report a problem on a shipped or delivered seller order. While the report is open the payout is **held**, even after delivery and after the 7 days. [PayoutHttpTests, ReturnClaimHttpTests]
- The admin decides: Rejected (no refund), Approved or PartiallyRefunded with a refund amount (more than 0 and not more than the seller order total).
- A refund creates a `Refund` for the retailer and **reduces the held payout by the same amount, never below 0**. [SettlementRulesTests]
- The commission is **not** reduced when goods are refunded. **[OWNER DECISION]**
- **Gap:** a payout that was already Released is not taken back; the refund is then paid by the platform. There is no clawback or negative balance yet.

## 7. What is not done

- No real transfer to the seller and no seller bank or linked account onboarding (F3.5).
- No settlement report in rupees per day for the platform (the admin payouts list shows the rows).
- Partial refunds are separate `Refund` rows; there is no check that all refunds of one payment together stay within the amount paid beyond the per-claim limit.
