# Decisions

- Brand remains `TextileConnect` as the placeholder.
- Manufacturer pays commission: `Commission.DefaultRate = 2%`; retailer platform fee is `0`.
- Settlement auto-release default is `7` days.
- Development uses fake OTP, payment, and local private-file implementations; production adapters require secrets/accounts.
- Collective buying and logistics remain intentionally minimal until the post-MVP phase.
- The frontend contract's explicit `{ token }` CSRF response and expanded `auth/me` profile are authoritative; existing backend domain models remain the source for product and order persistence until their full detail DTOs are implemented.
