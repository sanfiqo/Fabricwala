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
| 1 | Saved products | Shop | ready (`/saved-products`) | heart on cards, saved grid, remove, empty state |
| 2 | Seller storefront | Shop | ready (`/storefronts/{id}`) | cover, logo, rating + count, about, city, product grid |
| 3 | Invoice download | Shop, Seller | download route only | Download GST invoice button on order detail; invoice list is missing in the API |
| 4 | Bulk upload | Seller | ready (`/seller/products/bulk/*`) | template download, file pick, error preview table, import button, result |
| 5 | Edit product (finish) | Seller | update route missing in API | same form as Add product, photo delete/re-upload |
| 6 | Legal pages | All | none needed | Privacy, Terms, Seller agreement, Refund and returns (placeholders marked [LEGAL REVIEW NEEDED]) |
| 7 | Address book | Shop | edit/delete missing | list, add, edit, delete, set default |
| 8 | RFQ: new request | Shop | ready, switch `Rfq` is off | what you need, quantity, city, end date (1 to 30 days) |
| 9 | RFQ: my requests + quotations | Shop | ready | status tabs, quotations cheapest first, Accept, Cancel |
| 10 | RFQ: open requests + send quotation | Seller | ready | request list, price, quantity, days to deliver, note, Withdraw |
| 11 | Bank / payout account link | Seller | status endpoint missing | link status, onboarding steps |
| 12 | Problem reports list | Admin | partly | list of claims, photos, seller reply, decision buttons |
| 13 | Staff users | Shop, Seller | adds by user id, not mobile | list, add by mobile, remove |

Not screens: real SMS/WhatsApp sending and live Razorpay need accounts (P11).
