# Audit log

Table `AuditLogs`: **who** (`UserId`), **what** (`Action`, `EntityName`, `EntityId`, `Details`), **when** (`CreatedAtUtc`), **from where** (`IpAddress`).

`UserId` is the signed-in user. It is `system` for the per-minute background job (no IP), and `anonymous` when the caller has no sign-in, which is the payment gateway's webhook (the IP is the gateway's).

## What is recorded (each has a test in `AuditTrailTests`)

| Kind | Action | How |
| --- | --- | --- |
| Approval | `POST businesses/{id}/approve`, `.../reject`, `.../suspension`; document review | every successful write under `/api/v1/admin` is recorded by `AdminAuditMiddleware`: user, route, id, IP |
| Moderation | `POST products/{id}/moderate` (also `product.deactivate`, `product.activate`, `product.flag` with the product id) | middleware + an explicit row |
| Configuration | `PUT settings` (also `settings.updated` with the new commission rate and payout days), shipping rates, categories, banners | middleware, plus the explicit row for settings |
| Payment | `payment.paid`, `payment.failed`, `payment.late_refund` | explicit, in the same save as the change |
| Orders | `order.accepted`, `order.rejected`, `order.dispatched`, `order.delivered`, `order.cancelled` | explicit |
| Refunds | `refund.requested`, `refund.processed` (amount in `Details`) | explicit |
| Payouts | `settlement.released` (amount in `Details`, user `system`) | explicit |
| Claims | `claim.decided` (decision and refund amount) | explicit + middleware |

Failed requests (4xx, 5xx), reads and calls by someone who is not an admin leave no row.

## What is never stored

The request body (an admin's note, a rejection reason typed by the admin), OTP codes, document file keys and secrets. `Details` is only a short text made by the code: an amount, a status, an order number. A test checks that a typed note appears nowhere in the table.

## Limits and gaps

- The middleware row is saved right after the admin action, in a second save. If the server dies between the two, the action stays without its row. The explicit rows are saved in the same save as the change.
- Behind a proxy or load balancer the IP is the proxy's until forwarded headers are configured (task F9.5).
- There is no screen or endpoint to read the audit log, and no retention or archiving rule yet.
- A seller own commission rate is set with `PUT /api/v1/admin/sellers/{id}/commission-rate` (`{ "rate": 0.03 }`, null goes back to the default); the old and new value are written as `seller.commission-rate`. It changes orders placed from then on. [AdminAuditAndCommissionHttpTests]
- The log is read with `GET /api/v1/admin/audit-log` (filters: action prefix, entity, user id, from and to dates; newest first; paged).
- The earlier generic interceptor (a row per changed entity, no user, only for synchronous saves, so almost never) was removed.
