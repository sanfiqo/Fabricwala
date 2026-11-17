# API screen audit

Step A audit from `docs/archive/CODEX_API_FOR_SCREENS_PROMPT.md`. Status describes whether the
current API covers the complete screen contract, not merely whether one related route exists.

The prompt lists **23 screens** in section 2. The follow-up adds **15 not-yet-designed
screens**, for **38 screens audited**.

Night audit A1: the night prompt mentions **33** additional screens, but section 2
of the referenced prompt names only the 23 designed screens. The existing audit
supplies 15 additional named screens. All 38 named screens are retained below;
the remaining 18 unnamed screens cannot be mapped without a screen list. This is
an input gap, not evidence that those screens have working endpoints.

| Area | Screen | Endpoint(s) | Request DTO / query | Response DTO / shape | Status |
|---|---|---|---|---|---|
| Retailer | Home | `GET /api/v1/categories`; `GET /api/v1/products`; `GET /api/v1/banners` | Product query: `q`, `categoryId`, `minPrice`, `maxPrice`, `maxMoq`, `sort`, `page`, `pageSize` | Category/banner entities; paged anonymous product list using `ProductSummaryResponse` | partly |
| Retailer | Product | `GET /api/v1/products/{slug}`; `GET /api/v1/landed-cost` | `productId`, `qty`, `cityId`, optional `pincode` | `ProductDetailResponse`; `LandedCostResponse` | partly |
| Retailer | Cart | `GET /api/v1/cart`; `POST/PUT/DELETE /api/v1/cart/items...` | `CartRequest(VariantId, Quantity)` | `CartResponse` (tier price on product total qty, GST %, `nextTier` add-more savings, per-line `issues`, `canCheckout`); shipping calculated at checkout | exists |
| Retailer | Checkout | `POST /api/v1/checkout`; `POST /api/v1/payments` | `CheckoutRequest(DeliveryAddressId)` + required `Idempotency-Key` header (8-100 chars); `PaymentRequest(OrderId)` | `OrderResponse` (201 created, 200 replay of the same key; totals, delivery snapshot, per-seller orders with item snapshots and platform fee); `PaymentSessionResponse` (`POST /api/v1/payments` with `Idempotency-Key`, `GET /api/v1/payments/{gatewayOrderId}` status Pending/Paid/Failed; test gateway only: `POST /api/v1/payments/{gatewayOrderId}/dev-complete[?success=false]`) | exists |
| Retailer | Orders list | `GET /api/v1/orders?group=active\|delivered\|problems\|all&page&pageSize` | optional group/paging (pageSize 1-100, newest first) | `PagedResponse<RetailerOrderSummaryResponse>` (`status`, `group`, item count, first item title, seller names, `items`: the order lines as in the detail, so no second request per order) | exists |
| Retailer | Track order | `GET /api/v1/orders/{id}`; `POST /api/v1/orders/{id}/confirm-delivery` | Order id | `OrderResponse` (`status`, `group`, delivery address, per-seller `history` timeline, `shipment` LR/carrier, seller names); confirm returns the same detail | exists |
| Retailer | Search + filters | `GET /api/v1/products` | Supports query, category, price, MOQ, sort, paging; no material/size/colour/seller-city filters | Paged `ProductSummaryResponse` list | partly |
| Retailer | Account | `GET /api/v1/auth/me`; `GET/PUT /api/v1/businesses/me`; address routes | `BusinessUpdate`; address entity | Auth profile/business/address shapes; language preference missing | partly |
| Retailer | Notifications | `GET /api/v1/notifications`; `GET /unread-count`; `POST /{id}/read` | Page query; notification id | Anonymous paged list; `{ count }`; notification entity | partly |
| Retailer | Report a problem | `POST /api/v1/seller-orders/{sellerOrderId}/claims`; photo upload route | `ClaimRequest(Reason)`; individual photo upload | Return-claim entity / photo count | partly |
| Auth | Login (mobile) | `POST /api/v1/auth/otp/request` | `OtpRequest(PhoneNumber)` | `204`; challenge cookie | exists |
| Auth | OTP | `POST /api/v1/auth/otp/verify` | `OtpVerifyRequest(PhoneNumber, Code, ChallengeId?)` | `AuthProfileResponse` with user, roles, business and preferredLanguage | exists |
| Auth | Business details | `POST/GET/PUT /api/v1/businesses...`; document routes | Business entity; `BusinessUpdate`; multipart document; no pincode lookup DTO/route | Business/address/document entities | partly |
| Auth | Waiting for approval | `GET /api/v1/auth/me` | None | Auth profile anonymous shape including verification status/rejection note | exists |
| Seller | Dashboard | `GET /api/v1/seller/dashboard` | None | Anonymous dashboard: new orders, pending dispatch, monthly sales, payout due, top products, low stock | partly |
| Seller | Orders | `GET /api/v1/seller/orders?status=new\|to_pack\|shipped\|done\|all&page&pageSize`; `GET /{id}`; `POST /{id}/accept\|reject\|dispatch` | `SellerReject(Reason)`; `SellerDispatch(LrNumber, CarrierName?)` | `PagedResponse<SellerOrderSummaryResponse>` and `SellerOrderResponse` (`orderValue`, `platformFee`, `youGet`, `acceptDeadline`, `items` on each list row, history, shipment); only paid orders; unaccepted orders auto-cancel and refund after the accept window | exists |
| Seller | Products | `GET /api/v1/seller/products`; product detail/delete; inventory alerts | None for list; product id for mutations | `ProductSummaryResponse`; alert/duplicate anonymous shapes | partly |
| Seller | Add product | `POST /api/v1/seller/products`; bulk template/preview/import/image routes | `ProductInput`; multipart bulk/image upload | `ProductDetailResponse`; `BulkPreview`; anonymous import/image results | partly |
| Seller | Payouts | `GET /api/v1/seller/payouts?csv=true` | Optional `csv` query | Anonymous payout rows or CSV: seller order, amount, commission, status, settled date | partly |
| Admin | Home | `GET /api/v1/admin/dashboard` | Optional `days` (one window only) | Anonymous dashboard: window, GMV, orders, commission, new businesses, pending approvals | partly |
| Admin | Approvals list | `GET /api/v1/admin/businesses`; approve/reject routes | `Rejection(Note)` for rejection; no status/page query | Pending-only business list/entity | partly |
| Admin | Review | Business/document routes; approve/reject routes | `Rejection(Note)`; document id | Business/document entities; no GST-check result endpoint | partly |
| Admin | Problem reports | `POST /api/v1/admin/claims/{claimId}/decide` | `AdminDecision(Decision, RefundAmount, Note)` | Return-claim entity | partly |
| Retailer | Category list | `GET /api/v1/categories` | None | Category name list; no id/slug/tree contract | partly |
| Retailer | Seller profile | `GET /api/v1/storefronts/{businessId}` | Business id | Storefront anonymous shape with business, rating, review count, products | exists |
| Retailer | Address book | `GET/POST /api/v1/businesses/me/addresses`; default-address route | Address entity; address id | Address entity/list; edit/delete missing | partly |
| Retailer | Payment success/failed | `POST /api/v1/payments`; payment webhook | `PaymentRequest(OrderId)`; webhook payload | Anonymous payment result; webhook response | partly |
| Retailer | Order detail | `GET /api/v1/orders/{id}` | Order id | `OrderResponse` | partly |
| Retailer | Reorder | No endpoint found | None | No response contract | missing |
| Retailer | Saved products | `GET /api/v1/saved-products`; `PUT` or `DELETE /api/v1/saved-products/{productId}` | Product id | paged product cards; the old `POST /favourites/{productId}` toggle was removed (F6.3) | exists |
| Retailer | Rate order | `POST /api/v1/orders/{id}/rating`; `GET` the same route | `{ stars 1-5, comment? }` | the rating; the seller rating on the dashboard and storefront is built from these. The old `POST /reviews`, `/enquiries` and `/group-buys` stubs were removed (F6) | exists |
| Retailer | GST invoice list | Download only: `GET /api/v1/invoices/seller-orders/{sellerOrderId}/download` | Seller-order id and invoice type | File download; no invoice-list response | missing |
| Seller | Ship order | `POST /api/v1/seller/orders/{id}/dispatch`; shipment/status routes | `SellerDispatch`; `ShipmentStatus` | `SellerOrderResponse`; `ShipmentResponse` | exists |
| Seller | Edit product | No update endpoint; only create/get/delete product routes | None | No update response contract | missing |
| Seller | Bulk upload result | `POST /api/v1/seller/products/bulk/preview`; `POST /import` | Multipart CSV/Excel file | `BulkPreview`; anonymous import result | exists |
| Seller | Bank link status | No seller payout-account status endpoint found | None | No linked-bank-account response contract | missing |
| Seller | Ratings | Storefront includes aggregate rating; no ratings list endpoint | Business id | Storefront aggregate only | partly |
| Admin | Admin lists | Business approval list and report export exist; no general problem-report list | Optional report kind | Business list; CSV export | partly |

## Error contract (A2)

All `/api/v1` errors use JSON `{ "code": "stable_string", "message": "safe English fallback", "fields": { "field": ["invalid_value"] } }`.
`fields` is optional. Translate `code` and field codes in the client; never show raw server diagnostics.
HTTP status and headers such as `Retry-After` are preserved. HEAD responses have no body.

| HTTP | Code |
|---|---|
| 400, 422 | `validation_failed` |
| 401 | `authentication_required` |
| 403 | `forbidden` |
| 404 | `not_found` |
| 405 | `method_not_allowed` |
| 409 | `conflict` |
| 413 | `payload_too_large` |
| 415 | `unsupported_media_type` |
| 429 | `rate_limited` |
| 503 | `service_unavailable` |
| Other 5xx | `internal_error` |
| Other 4xx | `request_failed` |

Business-rule failures keep their HTTP status but carry a specific code (always safe to translate):

| HTTP | Code | Meaning |
|---|---|---|
| 409 | `cart_empty` | Checkout with no cart items |
| 409 | `cart_not_ready` | A cart line has an issue (`below_moq`, `unavailable`, `insufficient_stock`) |
| 409 | `delivery_unavailable` | Delivery address pincode/city is not serviceable |
| 409 | `shipping_unavailable` | No freight rate for the seller-to-address route; order cannot be priced |
| 409 | `insufficient_stock` | Stock changed between cart and checkout |
| 409 | `idempotency_key_reused` | Same `Idempotency-Key` sent with a different request |
| 409 | `order_not_paid` | Seller action on an order that is not paid |
| 409 | `invalid_order_transition` | Action not allowed in the order's current state (e.g. accept twice, reject after accepting) |
| 409 | `accept_window_expired` | The acceptance window passed (the order will be or was auto-cancelled and refunded) |
| 409 | `product_deactivated_by_admin` | Seller tried to switch on a product an admin deactivated |
| 409 | `order_not_shipped` | Delivery cannot be confirmed before the seller ships |
| 409 | `order_not_payable` | Order is already paid or no longer waiting for payment |
| 400 | `payment_amount_mismatch` | Gateway reported a different amount than the order total |

## Coverage counts (A1 snapshot)

- Exists: **6**
- Partly: **28**
- Missing: **4**

These counts and the initial table record the starting audit. The completed B-task
contracts below supersede those snapshot entries; D1 will export the final OpenAPI.

## Authentication (B1)

Use `GET /api/v1/auth/csrf` and keep its cookie; send the returned token in
`X-CSRF-TOKEN` on every JSON/form mutation, including OTP request/verify, refresh,
and logout. Get a fresh token after sign-in because it is bound to the user.
Gateway webhook routes use signature verification instead of CSRF.
`POST /api/v1/auth/refresh` renews the current cookie and returns `AuthProfileResponse`;
`POST /api/v1/auth/logout` clears it (204). Both require authentication.
Profile fields: `userId`, `phone` (own user only), `fullName`, `roles`, `business`,
`preferredLanguage`. Role names remain `Retailer`, `Manufacturer`, `Admin`.
Development OTP is `123456`; five wrong attempts, five-minute expiry, one-use,
resend/mobile/IP limits remain enforced. Fake delivery does not print OTPs.
Development HTTP cookies work for local preview; production cookies are always Secure.

## Business profile and pincode contract (B2)

`POST /api/v1/businesses` accepts `BusinessCreateRequest`: `businessType`
(`Retailer`/`Manufacturer`), `legalName`, `displayName`, optional `phone` (defaults to
the signed-in user's phone), `email`, `gstin`, `pincode`. It returns 201 with
`BusinessProfileResponse`; repeat registration returns 409. Ownership, verification,
commission, platform ownership and ids cannot be supplied by the caller.
`GET/PUT /api/v1/businesses/me` returns only the current user's profile, including
`verificationStatus` and `rejectionNote`. Pending signup does not authorize ordering/listing.
GST is optional, uppercased and format-checked; this is not a live GST registration check.
A GSTIN (`gstin` on sign-up) is checked for its shape, a known state code (01 to 38, 97, 99) and the check character; a wrong one returns 400 with field `gstin`. This catches typos and invented numbers; it does not prove the number is registered (that needs the GST portal, which is not connected). Product photos now come with small copies: `?size=main|card|thumb` on the photo address gives a square WebP (800, 400, 120 pixels), the upload response and the product, list, cart and seller responses carry `srcSet` and `lqip` (a tiny blurred preview as a data URL), and `imageUrl` points at the card copy in lists, the main copy on the product page and the thumb copy in the cart; see docs/MEDIA.md. An upload that cannot be read as an image returns 400 `photo_unreadable`. `GET /api/v1/products/filters` returns counts for the filter sheet (see docs/SEARCH.md). Saving an address (sign-up, `/account/addresses`, `/businesses/me/addresses`) with a pincode that is not six digits gives 400 `pincode_invalid`, and one that is not in the postal directory gives 400 `pincode_unknown` (the pincode is in the message); the city is always the one of the pincode, so `/businesses/me/addresses` no longer reads a `cityId` from the caller. See docs/MASTER_DATA.md for what the directory holds. Seller order moves are defined in one place (`SellerOrderTransitions`): Pending to Accepted, Rejected or Cancelled; Accepted to Dispatched; Dispatched to Delivered; Rejected, Cancelled and Delivered are final. Any other move returns 409 `invalid_order_transition`. `POST /seller/orders/{id}/shipment` takes `{ "status": "InTransit" | "ReachedCity" }` for a shipped order only (400 `invalid_shipment_status` for anything else, including Delivered: delivery is confirmed by the retailer). Every 429 (`{ "code": "rate_limited" }`) carries a `Retry-After` header in whole seconds: for the OTP request it is the exact time until the send is allowed again (the longest of the resend cooldown and the per-number and per-IP windows that are full), for the "auth" and "search" limiters it is the time until the window resets. Clients should wait that long before retrying. Cart groups (`GET /cart` and every cart change) carry `sellerCity` and `arrivesBy` (a date like `2026-11-23`: today + the longest dispatch days in the group + the longest transit days to the default delivery address; null when there is no delivery address or no freight rate). `GET /seller/dashboard` has `rating` (average stars, one decimal, null until the first rating) and `ratingCount`; a rated order counts for every seller in it. `GET /products` also filters by `season` (contains, up to 80 characters), next to `material`. Product photos: `POST /api/v1/seller/products/{id}/media` (multipart, one field `file`; JPEG, PNG or WebP read from the file bytes, max 5 MB, max 8 photos per product) returns 201 `{id, url, mediaType, sortOrder, isMain}`; errors `photo_invalid_type` (400), `photo_too_large` (413), `photo_limit_reached` (409), 404 for a product that is not yours; `DELETE /api/v1/seller/products/{id}/media/{mediaId}` returns 204. The seller product detail `GET /seller/products/{id}` now has `media` (same fields, ordered; the first is the main image) and the list rows already carry `imageUrl`. A photo `url` (`/api/v1/products/{slug}/media/{id}`) works for the owning seller even while the product is a draft; everybody else can open it once the product is live. The first photo is the main image; there is no reorder route yet (delete and re-upload). There is no tiny blurred preview (`lqip`). `POST /api/v1/businesses` accepts an optional `ownerName` (the signup "Your name", up to 120 characters, trimmed); `BusinessProfileResponse` (also inside the admin review) returns it as `ownerName`, or null. `POST /api/v1/businesses/me/documents?docType=Gst|Pan|ShopLicence|Udyam` takes the file as multipart field `file` and the type as the required query value `docType` (case does not matter; anything else is 400); the admin review `GET /api/v1/admin/businesses/{id}` lists the uploaded documents with a `url` that an admin can open (it streams the private file; there is no signed link). `GET /api/v1/states` returns `[StateResponse(id, name, code)]` sorted by name, and `GET /api/v1/cities[?stateId=]` returns `[CityResponse(id, stateId, name, isDeliveryZone, isSourcingHub)]` sorted by name (both public, no paging; use them for the signup fallback when a pincode is not found and for the admin freight screen); `GET /api/v1/pincodes/{pincode}` returns `PincodeResponse(pincode, cityId, city, stateId, state)`;
malformed codes return 400 and unknown codes return 404. A pincode-only registered
location does not invent a street address or create a usable delivery address.
The postal directory is extensible in `PostalCodes`, initially seeded with only
452001 (Indore) and 141001 (Ludhiana). Import verified nationwide data before launch.
Sources: [Indore district](https://indore.nic.in/en/std-pin-codes/) and
[India Post Punjab directory](https://www.indiapost.gov.in/sites/PostalCircles/Punjab/Lists/PostOffices/PB_Pincode_12Nov2020.pdf).

## Admin approvals (B3)

`GET /api/v1/admin/businesses?status=Pending&page=1&pageSize=20` returns
`PagedResponse<AdminBusinessSummaryResponse>` (`items`, `total`, `page`, `pageSize`).
Statuses include Pending, Approved, Rejected and Suspended; sort is created time/id
descending, page size 1–100. Invalid filters/pages return 400.
`GET /api/v1/admin/businesses/{id}` returns `BusinessReviewResponse` containing
`business`, `gstCheck` (`provided`, nullable `formatValid`, `status` = `not_checked`
or `not_provided`), and `documents`. This is a format result, not a GST registry check.
Document URLs require Admin access and match both business and document ids; storage
keys and full phone numbers are not returned in admin review/list responses.
Approve/reject routes are unchanged; rejection requires a nonblank `note` up to
1000 characters. Missing businesses/documents return 404; wrong roles get 403.
Each member's approval claims/security stamp are updated atomically with the
business decision on MySQL. Repeating the same decision avoids duplicate notifications.
Suspended businesses require the separate reactivation flow.

## Public catalogue (B4.1)

`GET /api/v1/home` returns `HomeResponse`: categories and a paged `bestSellers`
collection ranked by paid, non-cancelled/non-rejected units sold, with stable id ties.
`GET /api/v1/categories?page=1&pageSize=100` returns active category ids, parent ids,
names, slugs and sort order. `GET /api/v1/products` returns a paged collection with
real seller/city, image URL, factory price range, material, available stock and units sold.
Filters: `q`, `categoryId`, `minPrice`, `maxPrice`, `maxMoq`, `stateId` or `state`
(code/name), `sellerCityId`, `material`, `size`, `colour`. Price filters use the starting
factory tier price. Sort: `popular`, `price-asc`, `price-desc`, `moq-asc`, `new`/`newest`.
Page size is 1–100; default 20. Invalid filters return 400. Public browsing is anonymous.
Only active, non-deleted products of approved, non-deleted manufacturers are visible.
`GET /api/v1/products/{slug}` includes stored fabric/weight/specs, active variants and
stock, ordered quantity tiers and media URLs. Missing/hidden products return 404.
Private storage keys are not returned. Media routes verify public product ownership;
only supported image/video formats are served inline, with `nosniff`.
No existing public DTO names changed. Destination-specific landed quotes follow in B4.2.

## Original audit follow-up list

- Home: no explicit best-sellers contract; current product response is a generic list.
- Product: landed price is a separate call and product details do not include all required presentation fields.
- Orders and seller orders: lists are not paged and response mappings omit populated snapshot/address/history fields.
- Search: material, size, colour, and seller-city filters are absent from the query contract.
- Notifications: no mark-all-read route; list/read responses are anonymous/entity shapes.
- Report a problem: missing pieces count, note, photo limit, and reason enum validation.
- Business details: pincode lookup is missing; business routes bind domain entities directly.
- Seller dashboard: no six-month chart and no explicit next-payout status contract.
- Seller orders/products/add-product: missing complete list filters, product on/off toggle, and explicit save-draft/publish/update contracts.
- Payouts: missing next-payout/on-hold summary and linked-bank-account display contract.
- Admin dashboard: no simultaneous Today/7/30-day result and no needs-a-decision breakdown.
- Admin review: no GST check result contract.
- Admin problem reports: no admin list endpoint; only decision by known claim id exists.

## Gaps

Unspecified screens | the referenced inputs name only 15 of the claimed 33 additional screens; 18 screen identities are unspecified | unknown
Account | en/hi language preference is absent | small
Approvals list | only pending businesses; no status filter or paging | medium
Category list | names only; category ids, slugs and tree are absent | small
Address book | address edit and delete are absent | medium
Home | no explicit best-sellers contract; current product response is a generic list | medium
Product | landed price is a separate call and product details omit required presentation fields | medium
Checkout | no visible `Idempotency-Key` contract; payment and checkout responses are not documented DTOs | big
Orders list | list is not paged and response mappings omit populated snapshot/address/history fields | medium
Track order | response mapping does not reliably populate order timeline and shipment details | medium
Search + filters | material, size, colour, and seller-city filters are absent | medium
Notifications | no mark-all-read route; list/read responses are anonymous/entity shapes | small
Report a problem | missing pieces count, note, photo limit, and reason enum validation | medium
Business details | pincode lookup is missing; routes bind domain entities directly | medium
Seller dashboard | no six-month chart and no explicit next-payout status contract | medium
Seller products | missing search/filter query and on/off toggle contract | medium
Add product | missing explicit save-draft, publish, update, and product-photo contract | big
Payouts | missing next-payout/on-hold summary and linked-bank-account display contract | medium
Admin home | no simultaneous Today/7/30-day result and no needs-a-decision breakdown | medium
Admin review | no GST check result contract | small
Admin problem reports | no admin list endpoint; only decision by known claim id exists | medium
Payment success/failed | no payment-status lookup or stable success/failed response DTO | medium
Order detail | response mapping omits populated detail fields | medium
Saved products | only toggle exists; no saved-product list endpoint | small
Rate order | review targets a product, not an order, and has no order-rating response contract | medium
Reorder | no reorder endpoint or response contract | medium
GST invoice list | no invoice-list endpoint; only single-invoice download exists | medium
Edit product | no product update endpoint | medium
Bank link status | no linked-bank-account status endpoint | medium
Ratings | no ratings list/detail endpoint; only aggregate storefront rating and create review exist | medium
Admin lists | no unified/list endpoint for problem reports and related admin queues | medium
