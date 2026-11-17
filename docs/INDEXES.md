# Database indexes for the hot queries

Foreign key columns are indexed by EF automatically (one index per relation, see `docs/FK_ORPHAN_CHECK.sql` for the list of relations).
The composite indexes below were chosen by reading the queries, not by measuring: no MySQL server was available to run `EXPLAIN`.
Re-check them with `EXPLAIN` on a database with real volume (task F5.4 seeds 20,000 products for that).

## Already there

| Index | Query it serves |
| --- | --- |
| `Products (Status, IsDeleted, CategoryId, Id)` | public catalogue list and category filter |
| `ProductVariants (ProductId, IsActive)`, `PriceSlabs (ProductId, PricePerUnit)`, `ProductMedia (ProductId, SortOrder, Id)` | price range, stock and main image of a product card |
| `SellerOrders (ManufacturerBusinessId, Status, Id)` | seller order inbox by tab (new, to pack, shipped, done) |
| `Notifications (UserId, IsRead, CreatedAtUtc)` | unread count and the notification list |
| `BusinessAddresses (BusinessId, AddressType, IsDefault, Id)` | default delivery or pickup address |
| `Orders (OrderNumber)` unique, `Payments (GatewayOrderId)` unique, `Invoices (InvoiceNumber)` unique, `IdempotencyRecords (BusinessId, Scope, Key)` unique | lookups by number or key |

## Added in migration `AddHotQueryIndexes`

| Index | Query it serves |
| --- | --- |
| `Orders (RetailerBusinessId, PlacedAtUtc, Id)` replaces `(RetailerBusinessId, Id)` | retailer order list: `WHERE RetailerBusinessId = ? ORDER BY PlacedAtUtc DESC, Id DESC` (the old index could not give the sort order) |
| `Orders (PaymentStatus, PlacedAtUtc)` | unpaid-order expiry, every minute: `PaymentStatus = 'Pending' AND PlacedAtUtc <= cutoff` |
| `SellerOrders (Status, AcceptDeadlineUtc)` | accept-window expiry, every minute: `Status = 'Pending' AND AcceptDeadlineUtc <= now` |
| `SellerOrders (ManufacturerBusinessId, PlacedAtUtc, Id)` | seller "all" tab ordered by newest and the dashboard sales per month |
| `Settlements (Status, ReleaseAfter)` | payout release, every minute: `Status = 'Pending' AND ReleaseAfter <= now` |
| `Settlements (ManufacturerBusinessId, Status)` | seller payouts summary and list |
| `Refunds (Status, Id)` | refund retry, every minute: `Status = 'Requested' ORDER BY Id`; admin count of stuck refunds |
| `Products (SellerBusinessId, IsDeleted, UpdatedAtUtc)` | seller product list, newest change first |

Full-text search on product title, description and material is a separate task (F5.2).
