# UX_RULES.md — TextileConnect

Rules for every screen, web (PWA) and later the mobile app. If a rule here clashes with a design file, tell the owner, do not guess.

## 1. Who uses this
Shop owners and factory owners across India. Many use a mid or low-end Android phone on a slow network, many are more comfortable in Hindi than English. Build for one hand, bright sunlight, and a person in a hurry.

## 2. Language
- English and Hindi (हिंदी) from day one. Every visible text comes from the i18n files. No text typed inside components.
- Plain, short words. Say "Accept order", not "Confirm fulfilment".
- Hindi text is longer. Never fix a width to a text. Test every screen in Hindi at 360px and 390px.
- Numbers use the Indian format (₹1,24,500). Dates read like "14 Nov".
- No flag emoji or flag icons anywhere. Language is shown by name: English | हिंदी.
- Fonts: Inter and Noto Sans Devanagari, self-hosted.

## 3. Money and trust
- The server calculates all money. The client only shows it.
- Retailer sees the landed price: factory price + delivery + GST, for their pincode.
- Seller sees order value, platform fee (from config, default 2%) and "You get".
- Seller is paid after delivery is confirmed, or automatically after 7 days. Say this in plain words where it matters.
- Never show a bank account number. Say "your linked bank account".
- The platform never holds wallet funds.
- Checkout and payment calls are idempotent. A double tap or a retry never charges twice. Disable the button while sending and show progress.

## 4. Layout and touch
- Design width 390px. Must work from 360px to tablet.
- Touch targets at least 44px (prefer 48px). One main action per screen, big, at the bottom.
- Body text 15 to 16px. Nothing below 12px. Contrast at least AA.
- Main navigation: floating pill tab bar, max 4 items, active tab is a charcoal pill. Content gets bottom padding so nothing hides behind it.
- Respect safe areas (notch, home bar).
- Colours come from tokens. Never type a hex value inside a component.
- Cards radius 20 to 24, buttons are pills. No navy blue buttons.

## 5. Every screen has four states
- Loading: skeleton in the shape of the final content. No spinner on lists.
- Empty: a short sentence, an icon, one button that helps.
- Error: what happened in plain words, one Retry button. Never show raw server text or error codes to the user. Map the backend `code` to a message.
- Offline: a clear banner, show saved data, say what needs internet.

## 6. Forms
- Ask for the least. Optional fields say "(optional)".
- Error appears under the field, in plain words, in red with an icon, not colour alone.
- Never lose what the person typed. Not on error, not on language switch.
- Use the right keyboard (number pad for mobile, pincode, quantity).
- Pincode fills city and state by itself. GST number is 15 characters, optional at signup.

## 7. Feedback and motion
- Every tap gives feedback within 100 ms (press scale 120 ms).
- Undo toast for removals. Confirm dialog only for things that cannot be undone.
- Animate only transform and opacity. Respect reduced motion. Keep a light profile for weak phones.
- No layout jump when images or data arrive. Reserve space.

## 8. Orders and time limits
- Seller must accept within the time limit. Show a countdown chip. If it ends, order is cancelled and the shop gets a full refund. Say this once, calmly.
- Show order status as a clear timeline: Placed, Accepted, Packed, Shipped, Delivered.
- "Report a problem" is always one tap away from a delivered order.

## 9. Accessibility
- Every control has a visible focus state and an accessible name.
- Icons that carry meaning have a text label.
- Colour is never the only signal.
- Screen reader order matches visual order.

## 10. Performance
- First JS load under 200 KB gzip. Each route is its own chunk.
- Lists are virtualised when long. Images are lazy, sized, with a placeholder.
- Works as a PWA: installable, caches Home and Orders for offline reading.

## 11. Privacy and safety
- No secrets in code, logs or docs.
- Ask for the least personal data. Do not show a person's full mobile number to others.
- Admin actions that change money or block a business need a written note.
