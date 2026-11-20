# 26 — Home Screen Feature Spec (V3.1)
Reference: `css/home-screen.html` (open with `#buyer` for the logged-in view) and `css/home-screen.png`. Placeholders only (grey boxes); production uses real fabric photos. Styling follows `25` (strict B&W; colour only from fabric imagery and the status colours).

## Layout order (top to bottom)
1. Sticky header: logo, buyer greeting (buyer only), language toggle (हि/EN), swatch-board button with count, notifications.
2. Search: text, camera (search by photo), go. Suggestions show only while the search is focused.
3. Tabs: Fabrics / Mills / Ready stock / My RFQs.
4. Quick-filter row: Filters (opens sheet), Verified, Ready stock, MOQ < 50 m, Under ₹200.
5. Auto-slide banner carousel (all users): slide 1 is the RFQ call-to-action, then Ready stock, New verified mills, Order a sample. Buyer additionally sees the active RFQ card and order-tracking strip below it.
6. Category rail, "Shop by use" grid, Trending, Ready stock, Top verified mills.
7. Fixed footer: guest sign-in bar (guest only) + 5-item bottom nav.

## Features, behaviour and backend need
| Feature | Behaviour | Backend |
|---|---|---|
| Quick filters / Filters sheet | Width, GSM, composition, colour dots, price unit (per metre / per kg). Button shows live result count. | Existing search API + new facet counts |
| Shop by use | Saree, Kurti, Shirting, Suiting, Upholstery, Lehenga → results filtered by end use | New `use` attribute on products |
| Bulk estimate | Opens sheet: tier prices, quantity input, rate, total, "add N m for next tier", pincode delivery estimate. Excludes GST and freight (stated on sheet). | Tier pricing exists in spec; delivery estimate needs new API |
| Order sample | Card button starts sample request (₹160 is a demo price) | New sample-order flow; business decision on price/policy |
| Swatch board + price alerts | Heart saves fabric, shows count, toast; board sheet lists saved and shares to WhatsApp | New saved-items + alerts tables, notifications job |
| WhatsApp share / contact | Share link on cards and board; "WhatsApp" on mill cards | Share is front-end only; mill number from supplier profile |
| GST ✓ / Verified badges | Shown on mill cards and verified products | Verification status field; GSTIN check is a business process |
| Order tracking strip | Placed, Packed, Shipped, Delivered (buyer only) | Existing order/shipment status |
| Active RFQ card | Quotes received, lowest price, Compare (buyer only) | Existing RFQ endpoints (`21`) |
| Hindi/English toggle | Switches key strings (demo covers key strings only) | i18n resource files for all screens |

## Rules
- Product price shows its real unit (m, kg, piece); never assume metres.
- Every status uses text plus icon, never colour alone. Sheets: Esc and backdrop close, focus returns to trigger, `role=dialog aria-modal`.
- Counts, delivery days and ₹160 sample price in the reference are demo values.
- Known cosmetic issues in the reference: toast can overlap an open sheet (show it above the sheet or inside it), card controls are tight near the fixed footer (add bottom padding), language toggle does not translate sheet content.
- Build order for Codex: tokens/components (`25`) → Home static with real API data → filters + estimate → swatch board/alerts (needs backend) → sample orders. Do not touch cart, checkout, OTP or RFQ award logic.

## Auto-slide banner rules
- Advances every 4 s; swipe/scroll-snap works manually; next card peeks (88% width) to show it slides.
- Pauses on touch, hover, keyboard focus, when a sheet is open, when the tab is hidden, and via a visible Pause/Play button (WCAG 2.2.2).
- Off by default when the device has `prefers-reduced-motion`.
- Dots show position; each slide is one link with real text (not text baked into an image). Max 5 slides; slide 1 stays the RFQ CTA. Slides come from an admin-managed list (new backend) in production.
