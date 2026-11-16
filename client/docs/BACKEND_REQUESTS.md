# Backend requests (real gaps only)

Checked against `docs/api/openapi.json` and the running API (16 Nov). The earlier list of 11 items is mostly done by the backend; those are removed. What is left:

## Still missing
1. **States and cities lists** (`GET /states`, `GET /cities`). Needed by Admin freight rates (from city, to city) and by the signup fallback when a pincode is not found. Freight rates also have no `id` (keyed by from, to, type), which the client works around.
2. **Order list with content.** `GET /orders` and `GET /seller/orders` return summaries only, so the client reads every order again (up to 100 requests). Please add `sellerOrders[].items` (or a lighter `items` array) to the list rows, or accept `?expand=items`.
3. **Product photo upload for one product** (`POST /seller/products/{id}/media`). Only `bulk/images` exists, so the Add/Edit product screen cannot attach photos in real mode. Also `imageUrl` and an `lqip` (tiny blurred preview) on products, cart items and order items.
4. **Signup owner name.** The design asks "Your name"; `BusinessCreateRequest` has no `ownerName`.
5. **Business document upload needs a type.** `POST /businesses/me/documents` takes only `file`; it needs `docType` (GST, PAN, ShopLicence, Udyam). `GET /admin/businesses/{id}` should return the documents with a short-lived URL (not tested live).
6. **Seller rating** (`rating`, `ratingCount`) on the dashboard, and the cart `arrivesBy` date and seller city per group.
7. **Seller delivery marking.** Not needed if the shopkeeper confirms delivery (current client behaviour). Tell us if sellers should mark "reached the city"; the route rejects every status I tried.
8. **GST check result** on the admin review (`valid`, `legalName`, `status`).

## Differences the client already handles (no backend change needed, listed so nobody breaks them)
- Phone is `+91` plus 10 digits; the client shows 10 digits.
- Commission is a fraction (0.02); the client shows percent. Settings are strings in a key map.
- Orders and notifications are paged objects; categories too.
- Problem report reasons are `ShortQuantity` and `QualityIssue`; photo route is `seller-orders/{id}/claims/{claimId}/photos`.
- Seller product on/off is `PUT /seller/products/{id}/active`.
- Dev payment: after `POST /payments` the client calls `payments/{gatewayOrderId}/dev-complete` in development only.

## Not verified live yet (adapter written from the contract, no data to test with)
Notifications list shape, problem report submit and photo, admin freight rates, admin settings save, seller product create/edit, product search filters `material` and `season` (the API audit says they are not supported).
