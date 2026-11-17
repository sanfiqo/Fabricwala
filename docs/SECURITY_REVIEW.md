# Security review — TextileConnect

Review of 17 Nov 2026 by the development team's own checks (no outside tester). It lists what is in place, where the proof is, and what is **not** done. "Proof" is a test that fails if the protection is removed; a statement without a test is marked as such.
This is not a penetration test and does not replace one before the first real customer.

## 1. OWASP Top 10 (2021)

| # | Risk | What is in place | Proof | Gap |
| --- | --- | --- | --- | --- |
| A01 | Broken access control | Roles by policy; every query filtered by the signed-in business; an order, product or address of another business answers 404 | `IdorWalkTests` calls all 49 routes that take an id as two businesses that own nothing, then checks no data changed; `FullJourneyHttpTests` | Staff roles inside a business are not separated (all members act as the business) |
| A02 | Cryptographic failures | One-time codes and phone lookups stored as HMAC; cookie is HttpOnly + Secure; HSTS outside Development; app refuses to start in Production with the local HMAC keys | `HardeningTests`, Identity tests | Data Protection keys sit in the database unencrypted (a key-encryption certificate is not set up); database connection encryption is a deployment setting |
| A03 | Injection | EF Core parameters only; no raw SQL with user text; CSV exports neutralise formulas (`CsvCell`); responses are JSON, never HTML | `CsvInjectionTests`, `CataloguePerformanceTests` (search with hostile text) | Full-text search text is passed as a parameter; not fuzzed |
| A04 | Insecure design | State machine for orders, server-side prices, idempotent checkout and webhooks, payout held while a problem is open | `SellerOrderTransitions` tests, `FullJourneyHttpTests`, settlement tests | Commission bearer and payout delay still to be decided by the owner |
| A05 | Security misconfiguration | Security headers on every answer, CSP `default-src 'none'` for the API, CORS limited to configured origins, swagger and demo data only in Development, fake adapters refused outside Development | `HardeningTests`, `ProductionAdapterGuard` tests | Real values of the proxy list, CORS origin and secrets are checked only by the warning at start-up, not by a deploy test |
| A06 | Vulnerable components | `npm audit --omit=dev`: **0** vulnerabilities. `npm audit` with dev tools: 12 moderate, all in build tooling, not shipped. NuGet packages are current at the time of writing | run by hand on 17 Nov 2026 | No automated scan in CI (CI is also not running, see PROGRESS.md); run `dotnet list package --vulnerable` before each release |
| A07 | Identification and authentication failures | OTP 5 minutes, single use, 5 wrong tries, limits per number and per address; antiforgery header on changing calls; cookie refreshed after approval | Identity tests, `HardeningTests` | Admin second step (authenticator app) is built and **off by default** (`Auth:AdminMfa`, see DEPLOY.md); real SMS not built: the fixed development code works in Development only |
| A08 | Software and data integrity | Payment webhooks verified by signature and applied once; migrations applied by a person, not by the app; uploads judged by bytes | `PaymentWebhookTests`, `UploadHardeningHttpTests` | No signed builds or image signing |
| A09 | Logging and monitoring failures | One log line per request with correlation id; no query string, body, code or token; admin actions written to the audit log | `RequestLoggingTests`, `docs/AUDIT.md` tests | Log sink and error tracker not chosen, so nobody is alerted yet (F9.6) |
| A10 | Server-side request forgery | The server fetches no address given by a user. Outgoing calls go only to the payment gateway | review of `HttpClient` use | Add an allow-list if image-by-URL import is ever built |

## 2. Headers (`SecurityHeadersMiddleware`)
`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Permissions-Policy` (camera, microphone, geolocation off), for `/api` a CSP of `default-src 'none'; frame-ancestors 'none'`, `Cache-Control: no-store` for changing or signed-in requests, HSTS outside Development/Testing.
The **frontend host** must send its own headers (CSP allowing only its own scripts and the API origin, the same nosniff, framing and referrer rules). That file is part of the client deployment, see DEPLOY.md. Proof for the API: `HardeningTests`.

## 3. Rate limits (`ApiRateLimiting`)
Per client address, in fixed one-minute windows: sign-in routes 30, search 120, everything 600 (health probes exempt). Rejections answer 429 with `Retry-After`. IPv6 clients are grouped by /64.
A bug was found and fixed during this review: the earlier limiter was one counter for the whole site, so a single abuser would have locked everyone out. The address is read from `X-Forwarded-For` **only** from proxies listed in `Proxy:KnownProxies` / `Proxy:KnownNetworks`; otherwise the header is ignored (so it cannot be forged). Proof: `HardeningTests`.
Gap: the counters live in memory, so with several API instances each keeps its own count.

## 4. Access control walk
`IdorWalkTests` enumerates every route with an id, calls it with ids of rows that exist but belong to someone else, and requires 403/404 and no change. A coverage assertion fails when a new id route is added without being walked. Two routes that deliberately act on the caller's own row are on an explicit allow-list. Result on 17 Nov 2026: no leak found.

## 5. Uploads (`UploadRules`)
- Type is decided by the first bytes (JPEG, PNG, WebP for photos; PDF, JPEG, PNG for documents), never by file name or the type the client claims.
- Size: photos 5 MB, documents 10 MB, bulk upload 100 files.
- Photos are decoded and re-encoded into new copies, so embedded scripts or metadata do not survive.
- Business documents and claim photos are not served from a public path.
Proof: `UploadHardeningHttpTests`. Gaps: no virus scan; private files are served through the API, not short-lived signed URLs (needs R2); the Development storage writes to local disk.

## 6. Secrets
- A search of tracked files and of the git history for key shapes, passwords and tokens found no real secret. **gitleaks was not run** (not installed); run it in CI once CI works.
- Local secrets are in user-secrets; the repository holds only names (`docs/SECRETS.md`) and `.env.example` with placeholders.
- Razorpay keys are to be set as `rzp_test_` first. Nothing in the repository or its history contains a key.

## 7. CSV export injection
The admin business report and commission report write names that a business typed. A name starting with `=`, `+`, `-` or `@` would run as a formula when opened in Excel, and a comma or line break would break the columns. `CsvCell.Text` prefixes such cells with `'` and quotes them. Found and fixed in this review; proof `CsvInjectionTests`.

## 7b. Requests for quotation and registers (17-18 Nov 2026)
- A buyer's name and phone are shown **only** to the manufacturer whose quotation the buyer accepted, and the manufacturer's to that buyer (`RfqHttpTests`). Before that manufacturers see the city and state of delivery only, and never the quotations of their competitors. The two businesses can then contact each other outside the platform: this is by design, and it also means the platform cannot stop spam or a side deal after the contact is shown.
- Abuse limits: 10 open requests per retailer, one quotation per manufacturer per request, text lengths, and everything behind the feature switch. There is no daily limit and no report button yet.
- The invoice register reads stored figures of the caller's own documents only (a seller sees their own rows, the admin route is admin only); CSV cells that a user typed are made safe against formulas (`CsvCell`).
- A switched-off feature answers 404 after the sign-in and role checks, so it does not reveal itself to anonymous callers.

## 8. Not done (in order of importance)
1. Switch the admin second step on (`Auth:AdminMfa=Required`) after enrolling every admin; a client screen for it.
2. Outside penetration test.
3. Log sink and alerting.
4. Signed, short-lived URLs for private files (needs R2 storage).
5. Data Protection key encryption at rest; key rotation plan.
6. gitleaks and package scans in CI.
7. Rate-limit counters shared between several API instances.
8. Anti-virus scan of uploads.
