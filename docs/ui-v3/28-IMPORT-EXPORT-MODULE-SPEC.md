# 28 — Import / Export Module Spec (V3.3)
Reference: `css/import-export.html` (working demo: CSV template download, upload, row validation, publish, export with CSV-injection protection) and `css/import-export.png`. The Export section of the mill profile is in `css/supplier-profile.html`. Placeholders and demo data only; nothing here exists in the repo yet.

## Part A — Bulk data import/export (supplier dashboard → Import / Export)
### Import products (CSV)
1. **Template:** download a CSV with columns `sku, name, category, unit, price_min, price_max, moq, width_in, gsm, composition, tier_prices` (tier format `20:620|100:590|500:560`).
2. **Upload:** `.csv`, UTF-8, max 2,000 rows / 2 MB in v1. (Excel users save as CSV; native `.xlsx` is a later option.)
3. **Review:** show Rows / Ready / Need fixing and an error list with row numbers. Rows with problems are skipped; they do not block the rest.
4. **Publish:** only valid rows are created. Show an import log (file, date, added, skipped, status).

### Validation rules (blocking per row)
| Field | Rule |
|---|---|
| required columns | `sku, name, category, unit, price_min, price_max, moq` must exist, otherwise the whole file is rejected with a clear message |
| name, sku | required; sku unique inside the file |
| unit | must be `metre`, `kg` or `piece`. Never assume metres |
| price_min / price_max | number > 0; max ≥ min |
| moq | whole number > 0 |
| gsm | number if present |
| tier_prices | ascending quantities, falling prices (checked server-side) |
Server must re-validate everything; the browser check is only for fast feedback.

### Server behaviour
- Upload goes to a background job; the supplier sees progress and a result. Large files never block the request.
- Existing SKU of the same supplier → **update**, not duplicate (ask in the review step: "Update existing products?").
- Photos are attached after import (by zip or one by one); imported products without photos stay **draft** until a photo is added.
- Every import writes an audit log: supplier, user, file name, counts, time. Keep the error report downloadable.
- Rate-limit imports; reject files that are not text, have a formula-looking SKU, or exceed limits.

### Export data (CSV; PDF later)
Products, Orders, Quotes (RFQ responses), Payouts, for a chosen period. Exports only the signed-in user's own data (supplier sees own; buyer sees own orders/RFQs; admin follows admin permissions). Large exports run in the background and appear under Recent exports.
- **CSV injection protection (required):** any cell starting with `=`, `+`, `-`, `@`, tab or CR gets a leading `'`. Reference implements this.
- File begins with a UTF-8 BOM so Excel shows Hindi and ₹ correctly.
- No hidden columns, no other users' personal data. Phone/GSTIN masking rules from `27` still apply to exports unless the owner exports their own data.

## Part B — International export / import (trade enquiries)
**Scope for v1: lead generation only.** FabricWala connects exporters and overseas buyers, like RFQ award (contacts shared, no commission). FabricWala does **not** handle customs, international payment, FEMA/RBI compliance or freight in v1.

### Mill profile (see `css/supplier-profile.html`)
- "Export ready" badge, shown only when the IEC is verified.
- Export block: countries exported to, loading ports, Incoterms (EXW / FOB / CIF), currencies (USD, INR), export min. order, export packing, IEC code (masked).
- Verification row: **IEC (Import Export Code)**, 10 characters, checked by admin with date. Mill-claimed IEC without a check shows "Self-declared".
- Products: optional **HS code** and export price in USD per unit.

### Buyer side
- Search filter "Exporters / Export ready" and a country selector.
- RFQ form gets optional fields when the buyer chooses "Outside India": destination country, Incoterm, currency, target port. Only export-ready mills are matched to such RFQs.
- Overseas buyer onboarding (later): company name, country, business email, tax ID. Verification step before contacts are shared.

### Import (buying from abroad) — later phase
Product tag "Imported · origin country", optional HS code and landed-cost note. No customs workflow in v1.

## Business decisions needed
Who verifies IEC and how; ~~whether overseas buyers can sign up in v1~~ (decided: yes, see `29`); currency display (USD only as supplier-entered text, no conversion in v1); max import size; whether imports can overwrite prices; retention period for import/export files and logs (suggest 90 days for files, 1 year for logs).

## Build order for Codex
1. Export CSV (read-only, lowest risk) with injection protection.
2. Import: template + validate (no write) → background job + publish → update-by-SKU → audit log.
3. Mill profile Export fields + IEC verification record.
4. RFQ export fields and export-ready matching.
5. Overseas buyer onboarding and import tags.
Do not touch cart, checkout, OTP or RFQ award logic. Add automated tests for every validation rule and the injection guard.


---
**V3.4:** overseas buyer signup is approved for v1; see `29-OVERSEAS-BUYER-SIGNUP-SPEC.md`.


---
**V3.9:** the Export block on the mill profile now shows an export globe; see `34-EXPORT-GLOBE-COMPONENT.md`.
