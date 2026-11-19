# Missing screens (checked 19 Nov 2026)

Method: routes in `src/App.tsx` and `src/pages/` compared with `docs/API_SCREENS.md`, `docs/PHASES.md` (P10 [WEB]) and `docs/RFQ.md`.
"No page" means no route and no page file exists. The backend column says whether the API is ready.

## Built (25 pages)
Common: Login, Register, Pending, Session expired.
Shop: Home, Products list, Product, Cart, Checkout, Orders, Order detail, Report a problem, Account, Notifications.
Seller: Home, Products, Product editor, Orders, Order detail, Payouts.
Admin: Home, Businesses, Review, Settings, Shipping.

## Missing, in build order

| # | Screen | Who | Backend | Elements |
|---|---|---|---|---|
| 1 | Saved products ✅ built | Shop | ready (`/saved-products`) | `/shop/saved`; heart on the product page; link in Account; remove, empty state |
| 2 | Seller storefront ✅ built | Shop | ready (`/storefronts/{id}`) | `/shop/store/:id`; seller name on the product page links to it; rating, city, product grid |
| 3 | Invoice download ✅ built (button) | Shop, Seller | download route only | "Download GST invoice" row on order detail for paid orders; the seller also gets the platform fee invoice. A list of all invoices needs an API route that does not exist |
| 4 | Bulk upload ✅ built | Seller | ready (`/seller/products/bulk/*`) | `/seller/products/bulk`; link in Account; template, file pick (CSV), error list, preview, import, result |
| 5 | Edit product (finish) | Seller | update route missing in API | same form as Add product, photo delete/re-upload |
| 6 | Legal pages ✅ built (outline only) | All | none needed | `/legal/privacy`, `/terms`, `/seller-agreement`, `/refund`; real text still needs a lawyer ([LEGAL REVIEW NEEDED]) |
| 7 | Address book ✅ built (add + default) | Shop, Seller | edit/delete missing in API | `/shop/addresses`, `/seller/addresses`; list, add (city from pincode), make default. Edit and delete wait for a backend route; the screen links to WhatsApp support meanwhile |
| 8 | RFQ: new request ✅ built | Shop | ready, switch `Rfq` | `/shop/rfq/new`; what you need, quantity, unit, target price, pincode, needed by, days open |
| 9 | RFQ: my requests + quotations ✅ built | Shop | ready | `/shop/rfq`, `/shop/rfq/:id`; tabs, quotations cheapest first, Accept (two taps), Cancel, contact of winner |
| 10 | RFQ: open requests + send quotation ✅ built | Seller | ready | `/seller/rfq`, `/seller/rfq/:id`; feed + my quotations, price, quantity, days, note, Withdraw |

The RFQ links in Account only show when the admin has switched the `Rfq` feature on (`GET /features`).
| 11 | Bank / payout account link | Seller | status endpoint missing | link status, onboarding steps |
| 12 | Problem reports list | Admin | partly | list of claims, photos, seller reply, decision buttons |
| 13 | Staff users | Shop, Seller | adds by user id, not mobile | list, add by mobile, remove |

Not screens: real SMS/WhatsApp sending and live Razorpay need accounts (P11).

## Still open (each needs a backend change first)
The backend could not be built or tested in the session that wrote the screens above (no .NET SDK, no network), so these four were not started. Per `docs/AGENTS.md` a backend change needs a build and tests.

| Screen | Backend work needed |
|---|---|
| Edit product (finish) | `PUT /seller/products/{id}` (update) with the same rules as create |
| Bank / payout account link | a seller payout-account status route and an onboarding step (also needs a Razorpay account, P11.2) |
| Admin problem reports list | a list route over claims (`GET /admin/claims`) with filters |
| Staff users | add a staff member by mobile number (today it takes a user id) |
