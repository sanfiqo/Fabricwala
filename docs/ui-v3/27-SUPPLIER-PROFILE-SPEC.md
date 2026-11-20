# 27 — Mill / Manufacturer Profile Spec (V3.2)
Reference: `css/supplier-profile.html` (add `#buyer` to the URL for the signed-in view) and `css/supplier-profile.png`. Placeholders only. Style follows `25` (strict B&W). Applies to Manufacturer, Mill and Trader profiles. Needs the new directory/showroom API (phase P6); nothing here exists in the repo yet.

## Page order
1. **Header (sticky):** back, name (appears on scroll), share to WhatsApp, follow.
2. **Identity:** cover, logo, name, badges (Verified, GST ✓, Manufacturer/Mill/Trader, Top rated), city, years in business, rating and review count, reply time, last active.
3. **Key numbers (6):** monthly capacity, min. order (real unit), lead time, looms/machines, repeat-buyer %, response rate.
4. **Sticky tabs with scroll-spy:** Overview, Products, Verification, Factory, Reviews, Contact.
5. **Fixed action bar:** Request quote (primary), WhatsApp, Call.

## Sections
| Section | Content |
|---|---|
| Overview | About, speciality chips, trade terms table: payment, sample, customisation, shipping, returns, export |
| Products | Category chips, 1:1 product cards with price + unit + min. order, "See all (n)" |
| Verification | Checklist, each row with document, date checked and a text status: Verified / Self-declared / Not provided (GST, address, factory video, OEKO-TEX, ISO, GOTS, other) |
| Factory | Photos, video, machines, staff, quality check, area |
| Reviews | Overall rating, sub-ratings (quality, on-time, communication, price accuracy), review cards with "✓ Verified order" tag, fabric, quantity, month, optional mill reply |
| Contact | Map, city, full address, phone, hours, languages |
| Similar mills | Rail of 3+ mills, "Report this supplier" link |

## Request quote (sheet)
Fabric (prefilled), quantity + unit (metre / kg / piece; never assume metres), pincode, note. Sends to this mill only. A general multi-mill RFQ stays a separate flow (`21`). Award behaviour is unchanged: contacts shared, no commission.

## Rules
- **Verified means FabricWala admin checked the document.** Show the date. Mill-claimed items say "Self-declared". Never show a verified badge without a stored check record.
- **Contact gating:** guests see city only; full address and phone appear for signed-in buyers. Mask the phone and GSTIN until revealed (reference: GSTIN `24AB•••••Z5`).
- **Reviews only from completed orders.** One review per order; the mill may reply once. Admin can remove abusive reviews.
- Status is always text plus icon, never colour alone. Sheets: Esc and backdrop close, focus returns.
- Terms (payment, sample, returns) are supplier-set; show "Prices exclude GST and freight".
- Photos: real, unfiltered; cover and gallery uploaded by the mill and moderated.

## Supplier-side: profile strength (so profiles stay rich)
Show the mill a completeness meter in its dashboard, with a checklist and the benefit of each item (more verified items → higher ranking in search):
logo + cover, about (min 150 chars), 5+ products, capacity and lead time, trade terms, GST verified, address verified, factory video, 1+ certificate, 3+ factory photos. Profile under 60% complete is not listed in the directory's "Top" rail.

## Data needed (new)
supplier profile fields above; verification records (type, document ref, checked_by, checked_at, status, expiry); gallery/video; reviews (order_id, ratings, text, reply); follows; response-time/response-rate metrics (computed); reports. Business decisions needed: who verifies and how fast, review moderation policy, sample price/policy, whether phone is shown or calls go through the platform.

## Build order for Codex
Static profile with API data → verification block → products tab (reuse card component) → reviews (needs completed-order link) → follow + alerts → supplier-side editor and completeness meter. Do not touch cart, checkout, OTP or RFQ award logic.


---
**V3.3:** the profile now also has an Export block and an IEC verification row. Details in `28-IMPORT-EXPORT-MODULE-SPEC.md` (Part B).
