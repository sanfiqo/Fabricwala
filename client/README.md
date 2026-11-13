# TextileConnect client (Phase 6 PWA)

React + Vite + TypeScript, Tailwind v4, shadcn-style components, TanStack Query, React Router, vite-plugin-pwa.
Mobile first. All visible text is in `src/i18n/en.ts`.

## Run with mocks (no backend needed)

```bash
cd client
npm install
cp .env.example .env.local      # VITE_USE_MOCKS=true is the default in the example
npm run dev                     # http://localhost:5173
```

Log in with a demo number (shown on the login screen). The code is always `123456`.

| Number | Who |
| --- | --- |
| 9000000001 | Shopkeeper (approved, Srinagar) |
| 9000000002 | Manufacturer (approved, Ludhiana) |
| 9000000003 | Admin |
| 9000000004 | New user, goes to business registration |
| 9000000005 | Business waiting for approval |

Mock data is kept in `localStorage` (`tc_mock_db_v1`). Clear site data to reset it.

Switching without editing files: open **Account** and use the Demo mode switch, or add `?mock=1` (sample data) or `?mock=0` (real backend) to any URL. The page reloads.
Language: Account has English and हिंदी. Text lives in `src/i18n/en.ts` and `src/i18n/hi.ts` (same keys).

### Open on your phone (same Wi-Fi)

```bash
npm run dev -- --host
```

Open the `Network:` address it prints (for example `http://192.168.1.20:5173`). If the phone cannot connect, allow Node.js through Windows Firewall for private networks.
Add `?perf=lite` to the URL to test the light motion profile that weak phones get automatically.

## Run against the real backend

```bash
# .env.local
VITE_USE_MOCKS=false
VITE_API_PROXY_TARGET=http://localhost:5000   # where TextileConnect.Api listens
npm run dev
```

Vite proxies `/api` to the backend, so the auth cookie stays same-origin and no CORS setup is needed in development.
In production, serve the client and the API from the same site (see ARCHITECTURE.md, cookie note).

Other scripts: `npm run build` (typecheck + production build + service worker), `npm run lint`, `npm run preview`.

## How the client talks to the API

- One client: `src/api/client.ts`. Base path `/api/v1`, `credentials: 'include'`.
- Before every POST/PUT/DELETE it sends `X-CSRF-TOKEN` from `GET /api/v1/auth/csrf` (cached; refreshed once on an antiforgery 400; dropped after login/logout).
- Any 401 clears the session and sends the user to the login screen.
- Typed functions per module: `src/api/endpoints.ts`. Types: `src/api/types.ts`.
- Errors: reads `detail`, `message` or `title` from the response body (ProblemDetails works).

## Endpoints the frontend expects

Enums are strings (`"Approved"`, `"PendingAcceptance"`), ids are numbers, money is a number, dates are ISO strings.
Full shapes are in `src/api/types.ts`. Items marked **(shape)** are not in ARCHITECTURE.md section 7 in this exact form, so please match them.

### Auth
| Call | Request | Response |
| --- | --- | --- |
| `GET auth/csrf` | | `{ token }` **(shape)** |
| `POST auth/otp/request` | `{ phoneNumber }` (10 digits) | 204/200 |
| `POST auth/otp/verify` | `{ phoneNumber, code }` | `Me`, sets the cookie |
| `POST auth/logout` | | 204 |
| `GET auth/me` | | `Me`: `{ userId, phone, fullName, roles[], business: { id, type, displayName, verificationStatus, rejectionNote } \| null }`. 401 when logged out. **(shape)** |

### Business
| Call | Request | Response |
| --- | --- | --- |
| `POST businesses` | `{ type, legalName, displayName, gstin, pan?, phone, email? }` | `BusinessDetail` |
| `GET businesses/me` | | `BusinessDetail` incl. `addresses[]` (with `cityName`, `stateName`) and `documents[]` **(shape)** |
| `POST businesses/me/addresses` | `{ addressType, line1, line2?, cityId, pincode, isDefault }` | `BusinessAddress` |
| `POST businesses/me/documents` | multipart: `docType`, `file` | `BusinessDocument` |

Retailers register one `Delivery` address, manufacturers one `Pickup` address.

### Master data (public)
`GET categories`, `GET states`, `GET cities?zone=delivery|sourcing` (needs `isDeliveryZone`, `isSourcingHub`).

### Products
| Call | Notes |
| --- | --- |
| `GET products?q&categoryId&material&season&minPrice&maxPrice&maxMoq&sort&page&pageSize` | `Page<ProductSummary>` = `{ items, total, page, pageSize }`. `sort` = `new`, `price-asc`, `price-desc`, `moq-asc`. Only Active products. **(shape)** |
| `GET products/{slug}` | `ProductDetail` with `media[]`, `variants[]`, `slabs[]` |
| `GET landed-cost?productId&qty&cityId` | `{ slabPrice, shippingPerUnit (null = on request), gstPerUnit, platformFeePerUnit, landedPerUnit, total, transitDays }` |
| `GET seller/products` | Own products, all statuses (`ProductSummary[]`). **(shape)** |
| `GET seller/products/{id}` | `ProductDetail` |
| `POST seller/products`, `PUT seller/products/{id}` | `ProductInput` incl. `variants[]`. Returns `ProductDetail` |
| `DELETE seller/products/{id}` | |
| `PUT seller/products/{id}/slabs` | `{ slabs: [{ minQty, maxQty \| null, pricePerUnit }] }`. Server must also reject overlaps, gaps, first slab != MOQ (client checks the same) |
| `POST seller/products/{id}/media` | multipart `file`. Returns `ProductMedia` |

### Cart and orders
| Call | Request | Response |
| --- | --- | --- |
| `GET cart` | | `Cart`: groups per manufacturer with **server-calculated** slab price, shipping (null = on request), GST, totals, plus `addresses[]` (retailer Delivery addresses) **(shape)** |
| `POST cart/items` | `{ variantId, quantity }` | `Cart` |
| `PUT cart/items/{id}` | `{ quantity }` | `Cart` |
| `DELETE cart/items/{id}` | | `Cart` |
| `POST checkout` | `{ deliveryAddressId }` | `Order` with `sellerOrders[]` |
| `GET orders`, `GET orders/{id}` | | `Order` (each seller order has `items[]`, `history[]`, `shipment`) |
| `POST orders/{id}/confirm-delivery` | | `Order` |
| `POST payments` | `{ orderId }` | `{ orderId, gatewayOrderId, amount, currency, keyId }` |

Slab price is chosen by the total quantity of the same product across all its variants.

### Seller orders
| Call | Request | Response |
| --- | --- | --- |
| `GET seller/orders` | | `SellerOrder[]` incl. `retailerName`, `deliveryCity`, `commissionRate`, `commissionAmount` **(shape)** |
| `GET seller/orders/{id}` | | `SellerOrder` **(not listed in the doc, needed for the detail screen)** |
| `POST seller/orders/{id}/accept` | | `SellerOrder` (only when paid) |
| `POST seller/orders/{id}/reject` | `{ reason }` | `SellerOrder` |
| `POST seller/orders/{id}/dispatch` | `{ lrNumber, carrierName }` | `SellerOrder` |
| `POST seller/orders/{id}/status` | `{ status: "ReachedKashmir" \| "Delivered" }` | `SellerOrder` |

Status values: `PendingAcceptance, Accepted, Rejected, Dispatched, ReachedKashmir, Delivered, Cancelled`.
Payment status values: `Pending, Paid, Failed, Refunded, PartiallyRefunded`.

### Admin
| Call | Request | Response |
| --- | --- | --- |
| `GET admin/businesses?status=` | | `AdminBusiness[]` with `documents[]` (`url` = short-lived signed URL) **(shape)** |
| `POST admin/businesses/{id}/approve` | | `AdminBusiness` |
| `POST admin/businesses/{id}/reject` | `{ note }` | `AdminBusiness` |
| `GET admin/settings`, `PUT admin/settings` | `{ commissionDefaultRate, settlementAutoReleaseDays }` | same |
| `GET admin/shipping-rates` | | `ShippingRate[]` |
| `POST admin/shipping-rates`, `PUT admin/shipping-rates/{id}`, `DELETE admin/shipping-rates/{id}` | `{ fromCityId, toCityId, rateType: "PerKg" \| "PerPiece", rate, minCharge, transitDays, isActive }` | `ShippingRate` |

### Not used yet
`GET notifications`, `POST notifications/{id}/read`, `POST businesses/me/users`, `GET/PUT businesses/me` (update), webhooks.

## What is not done

- Razorpay checkout (blocked on the external account, see P5.9). After checkout the app calls `POST payments` and tells the user payment is being set up.
- Hindi and Urdu text (P6.6). Structure is ready: see DECISIONS.md.
- Testing on a real phone (P6.6), and PNG icons for install on all devices.

See [DECISIONS.md](DECISIONS.md) for choices made without asking.
