# Stage A polish list

Method: Playwright (Edge, 390px) screenshots of the running app next to InkHomeV2, InkProductV2, InkCartV2 rendered at 390px from `client/design-ref/` (design fonts replaced by the local Inter so both use the same font). Content differs (our mock data is shawls, the design shows jackets), so the list is about layout, type, colour, radius, shadow and structure.

Status column: **fix** = changed in this pass, **keep** = intentional difference (reason given), **n/a** = needs something that does not exist yet.

## Global

| # | Difference | Status |
| --- | --- | --- |
| G1 | Amber "Demo mode" strip pushes the whole page down by 30px on retailer screens (design has nothing there) | fix: strip only on seller/admin screens; retailers see the switch in Account |
| G2 | Tap feedback was 100ms; asked for 120ms and for every button and card | fix: 120ms, applied to all buttons and `.press` cards |
| G3 | `html` had no background colour, so a white flash is possible between page changes | fix |
| G4 | Tab bar "Orders" icon was a clipboard; design uses a box | fix |
| G5 | Units read "piece", design reads "pcs" | fix: short unit labels (`pcs`, `sets`, `m`, `kg`) in cards and slab table |
| G6 | Safe areas: `viewport-fit=cover` was already set; the browser theme colour was still the old navy | fix: theme and splash colour now #FAF9F6 |

## Home (InkHomeV2)

| # | Difference | Status |
| --- | --- | --- |
| H1 | Header top padding 20px, design 22px | fix |
| H2 | Notification bell (46px round, amber dot) missing | n/a: no Notifications screen yet |
| H3 | Search placeholder too long, cut off ("Search cloth, e.g. jackets, kurtis…"); design is short | fix |
| H4 | Mic button inside the search pill | n/a: voice search not built (Stage B) |
| H5 | "Last order" date shows the year ("13 Nov 2026"), design "2 Nov" | fix |
| H6 | Order-again thumbnail is a generic box icon, design shows the garment | keep: order items carry no image yet (TODO(BE) `imageUrl` on order items) |
| H7 | Order-again button text looked larger than 13px/500 | fix: checked, set explicit size and weight |
| H8 | Category tabs have no item counts (design "Winter 420") | n/a: API returns no counts |
| H9 | Product photo fills the whole 138px square edge to edge; design shows the item on a soft surface with a shadow ellipse under it | fix: rounded photo, smaller than the slot, ellipse under it |
| H10 | Card unit "Min 50 piece" vs "Min 50 pcs" | fix (G5) |
| H11 | Photo shadow was drawn inside the photo | fix: sits below the photo |
| H12 | Tab bar content sat under the bar without bottom padding on the last row | fix: checked, `pb-32` and safe area |

## Product (InkProductV2)

| # | Difference | Status |
| --- | --- | --- |
| P1 | Title line-height 1.15, design 1.05 | fix |
| P2 | Right side of header is a cart button, design has "Save" | keep: Save does not exist; the cart button is the only way to see the cart bump |
| P3 | Seller line has no rating and chevron | n/a: no rating data |
| P4 | Photo panel: design shows the item at 230px centred with a shadow ellipse; ours stretched the photo to fill | fix |
| P5 | Photo counter reads "Photo 1 of 3"; design "1 / 4" | fix |
| P6 | Colour swatch gap 12px, design 10px | fix |
| P7 | Slab labels "50–199 piece", design "50+ pcs" style (short) | fix (G5) |
| P8 | Add-to-cart button had a bag icon; design is text only | fix |
| P9 | Bottom bar had a shadow and 16px bottom padding; design is a hairline and 22px | fix |
| P10 | Quick chips are 44px high, design 36px | keep: 44px is the minimum touch size in the rules |
| P11 | Stepper buttons 44px, design 40px | keep: same reason |
| P12 | Size chips: design shows sizes of one set; ours shows each size with its own quantity | keep: our stock is per size (see DECISIONS) |

## Cart (InkCartV2)

| # | Difference | Status |
| --- | --- | --- |
| C1 | Item thumbnail fills 76px edge to edge; design shows the item at 58px with a shadow ellipse | fix |
| C2 | Seller header shows subtotal on the right, design shows "Arrives by …" | keep: cart API has no delivery date; subtotal is useful |
| C3 | Seller header has no city ("· Ludhiana") | n/a: cart API has no seller city |
| C4 | Total row 16px, design 17px | fix |
| C5 | Extra "Remove" text link under each item | keep: needed for the remove and undo feature |
| C6 | Extra slab note, estimate note and WhatsApp help row | keep: existing features |
| C7 | Button says "Checkout · total", design "Pay total" | keep: payment is not wired yet |
| C8 | "Add 50 more, save ₹10" chip under each item | n/a: cart items carry no slab data; shown on the product page instead |
