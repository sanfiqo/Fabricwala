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
