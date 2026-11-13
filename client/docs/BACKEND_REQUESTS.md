# Backend requests (written by the frontend night run)

Each item: endpoint, request, response, why. The frontend runs on mock data until these exist.

## 1. Pincode lookup (B1)
- `GET /api/v1/pincodes/{pincode}`
- Response: `{ pincode, cityId, cityName, stateId, stateName }`. 404 with code `PINCODE_NOT_FOUND` when unknown.
- Why: pincode fills city and state at signup and for landed prices (UX_RULES 6).

## 2. Token refresh (A1)
- `POST /api/v1/auth/refresh` (cookie in, new cookie out, 204). The client tries it once on a 401 and gives up quietly if it is 404/405.

## 3. Error `code` on every error body (A1)
- Body `{ code, message }` or ProblemDetails with `extensions.code`. The client maps `code` to English and Hindi text (see `errors` in `src/i18n/en.ts` for the codes it knows: NOT_APPROVED, MOQ_NOT_MET, OTP_INVALID, GST_DUPLICATE, ORDER_STATE, NOT_PAID, ...). Unknown codes show a calm generic message.

## 4. Idempotency-Key (A1)
- The client sends `Idempotency-Key` (a UUID) on `POST /checkout` and `POST /payments`. The same key must return the same result and never create a second order or charge.

## 5. Business signup (B1)
- Owner name ("Your name") is in the design but not in `CreateBusinessInput`. Add `ownerName`.
- GST and documents must be optional at signup. The client sends `gstin: ""` when it is empty.
- Optional: send the OTP over WhatsApp (`POST /auth/otp/request` with `channel: "whatsapp"`).

## 6. Notifications (B2c)
- `GET /api/v1/notifications` returns a plain array of `{ id, type: "order"|"payment"|"account"|"system", title, body, createdAt, isRead, link? }`, newest first (a page is fine, the client reads `items` or the array).
- `GET /api/v1/notifications/unread-count` returns `{ count }`. `POST /notifications/{id}/read` returns 204.
- Missing today: `POST /api/v1/notifications/read-all`.
- Titles and bodies should be sent in the user's language (`Accept-Language: en|hi`).

## 7. Problem reports (B2c)
- `POST /api/v1/seller-orders/{sellerOrderId}/claims` with `{ reason: "Damaged"|"WrongItem"|"LessQuantity"|"NotAsShown"|"Other", pieces, note? }`. Validate the reason, pieces (1 to ordered), note length (500). Return `{ id, sellerOrderId, reason, pieces, status, createdAt }`.
- `POST /api/v1/claims/{id}/photos` (multipart `file`, at most 3, 5 MB each).
- Admin needs a list endpoint for open reports (see API_SCREENS.md "Admin problem reports").

## 8. Order items need `variantId` and an image (Home "Order again", Orders list)
- `OrderItem.variantId` and `OrderItem.imageUrl`.

## 9. Seller dashboard (B4a)
- `GET /api/v1/seller/dashboard` returning `{ newOrders:{count,value,cities[],acceptBy}, toPack, salesThisMonth, salesChangePct, payoutDue, nextPayoutDate, rating, ratingCount, salesByMonth:[{month,amount}] (6 months), lowStock:[{productId,title,variant,qty}], topProducts:[{productId,title,pieces,amount}] }`. All money computed by the server.
- Seller order list needs `commissionRate` and `commissionAmount` (already used) and a paged response.

## 10. Seller products and payouts (B4b)
- On/off toggle: `PUT /api/v1/seller/products/{id}/status` with `{ status: "Active" | "Inactive" }`, returns the product summary. Also needed: `GET /seller/products/{id}` and `PUT /seller/products/{id}` (update) — API_SCREENS.md says edit has no update endpoint.
- Product summary needs a stock figure (`totalStock`) so "Low stock" does not depend on the dashboard.
- `GET /api/v1/seller/payouts` returning `{ next:{amount,date}, onHold, paidThisMonth, rows:[{id,orderNumber,buyer,placedAt,deliveredAt,amount,status:"Paid"|"Scheduled"|"OnHold",date}] }`, and `?csv=true` returning `text/csv`. Linked bank account status is a separate missing endpoint (never return the account number).

## 11. Admin (B5)
- `GET /api/v1/admin/dashboard?days=1|7|30` returning `{ days, gmv, orders, commission, newBusinesses, pendingApprovals, decisions:[{kind:"business"|"claim",title,sub,link}] }` (today only one window).
- GST check result on review: `GET /admin/businesses/{id}/gst-check` returning `{ valid, legalName, status }`. The client only shows a reminder to check the number until this exists.
- Admin list of open problem reports (`GET /admin/claims?status=open`), then the decide route can be used from a review screen.
