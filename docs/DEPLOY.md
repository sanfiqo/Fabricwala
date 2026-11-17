# DEPLOY.md — running TextileConnect for real

**Status:** written from the code and configuration. The Docker image, compose file, backup and restore scripts and the k6 script were **never run** (no Docker, MySQL or k6 on the machine where they were written). Do a rehearsal on a staging server and fix this file with what you learn.

## 1. What you need
- A MySQL 8 server (managed is better than self-run), with one database for the marketplace and the identity tables (the connection string names one database; both contexts use it unless you split them).
- A host that runs the Docker image of the API behind HTTPS (a reverse proxy or load balancer).
- A static host for the React client, on the **same site** as the API (for example `app.example.in` and `api.example.in`), because the sign-in cookie is SameSite=Lax.

## 2. Settings
Set as environment variables (`__` for `:`) or the host's secret store. Never put them in a file in git. Names of the secrets are also in [SECRETS.md](SECRETS.md).

| Setting | Required | Meaning |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | yes | `Production` |
| `ConnectionStrings__TextileConnect` | yes | MySQL connection, with TLS if the database is not on a private network |
| `UserContacts__LookupHmacKey` | yes | random, 32+ bytes; losing or changing it makes every stored phone lookup unmatched |
| `Otp__StorageHmacKey` | yes | random, 32+ bytes, different from the one above |
| `DataProtection__KeyRingConnectionString` | yes | durable key ring (database); never local disk |
| `Cors__FrontendOrigin` or `Cors__FrontendOrigins` | yes | exact origin(s) of the client, e.g. `https://app.example.in` |
| `Proxy__KnownProxies__0` / `Proxy__KnownNetworks__0` | yes behind a proxy | address or network of your proxy; without it every visitor looks like the proxy and the rate limits and audit log are useless (the app logs a warning) |
| `Auth__AdminMfa` | before launch | `Off` (default) or `Required`. With `Required` an admin needs a code from an authenticator app after the mobile code; without it an admin session has no Admin role. Enrol first (below), then switch it on. The app logs a warning at start while it is not `Required` outside Development |
| `Bootstrap__AdminPhones__0` | first start | phone number that becomes Admin when it signs in; remove afterwards |
| `Payments__Razorpay__KeyId`, `KeySecret`, `WebhookSecret` | yes | use `rzp_test_` keys first |
| `Otp__Msg91__AuthKey`, `TemplateId` | when built | real SMS sender (not built yet) |
| `WhatsApp__AccessToken`, `PhoneNumberId` | when built | not built yet |
| `Storage__R2__AccountId`, `AccessKeyId`, `SecretAccessKey`, `BucketName` | when built | real file storage (not built yet) |
| `Serilog__WriteTo__...` | optional | log destination; the default is one JSON line per event on the console |

**The app refuses to start in Production while a development adapter is in place** (`ProductionAdapterGuard`): file storage, payment gateway and OTP sender. Today the real OTP sender (MSG91) and real storage (R2) do not exist, so **a Production start fails by design** until F3 is done. For a staging trial use Development inside a private network only, never with real customers.

## 3. First deployment
1. Build: `docker build -t textileconnect-api .` (multi-stage; the final image runs as a non-root user).
2. Check the pending migrations from a machine that can reach the database: `dotnet ef migrations list --project src/TextileConnect.Infrastructure --startup-project src/TextileConnect.Api --context MarketplaceDbContext`.
3. **If the database already holds data**, run `mysql ... < docs/FK_ORPHAN_CHECK.sql` first; it must return no rows.
4. Apply migrations (a person does this, the app never does): `dotnet ef database update` for `MarketplaceDbContext` and for `ApplicationIdentityDbContext` (the compose `migrate` service does both).
5. Load the pincode directory: `dotnet run --project src/TextileConnect.Api -- import-pincodes <file.csv>` (see [MASTER_DATA.md](MASTER_DATA.md); only 2 sample pincodes exist until the real file is loaded).
6. Start the API container with the settings above.
7. Build the client (`npm ci && npm run build` in `client/`) and publish `dist/` on the static host.

### Admin second step (authenticator app)
1. Leave `Auth__AdminMfa` as `Off`, sign in as the admin, and call `POST /api/v1/auth/mfa/setup`: it returns a secret and an `otpauth://` address (make a QR code of it, or type the secret into Google Authenticator, Microsoft Authenticator or similar).
2. Call `POST /api/v1/auth/mfa/enable` with the six digit code from the app. The answer holds **10 recovery codes, shown once**: save them somewhere safe. Each works once instead of an app code.
3. Set `Auth__AdminMfa=Required` and restart. Admin sessions that were open are ended; the next sign-in asks for the code (`POST /api/v1/auth/mfa/verify`).
4. If the phone with the app is lost: use a recovery code; with none left, set `Auth__AdminMfa` back to `Off`, sign in, enrol again, switch it on. Five wrong codes lock that user for a few minutes.
5. **Enrol before you switch it on**: while it is `Required`, an admin who has not enrolled can only enrol (and whoever holds that phone's mobile code can do it). There is no screen for this in the client yet; the routes are in [API_REFERENCE.md](API_REFERENCE.md).

## 4. Health probes
- Liveness: `GET /health/live` (the process answers). Restart the container if it fails.
- Readiness: `GET /health/ready` (database and storage work; 503 with a JSON list when not). Send traffic only when it is 200.
- `GET /api/v1/health` is the simple check for monitors.
Probes are exempt from rate limits and are logged at the lowest level.

## 5. Proxy and cookies
- Terminate HTTPS at the proxy and forward `X-Forwarded-For` and `X-Forwarded-Proto`; list the proxy in `Proxy__*`.
- Do not strip the `Cookie`, `X-CSRF-TOKEN` or `Idempotency-Key` headers; allow `X-Correlation-Id`.
- The client host must send its own security headers: a CSP allowing only its own scripts and the API origin in `connect-src`, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`.

## 6. Backups and restore
- Backup: `ops/backup-mysql.sh <folder>` (Linux, run daily from cron) or `ops/backup-mysql.ps1` (Windows). It takes a consistent dump, checks the gzip, keeps 14 days (`BACKUP_KEEP_DAYS`), exits non-zero on failure so the scheduler can alert.
- Restore: `ops/restore-mysql.sh <dump.sql.gz> --into <database>`; `--verify` restores into a scratch database and counts tables without touching the real one.
- Copy the dump folder **off the server** (another account or region); a backup on the same disk is not a backup.
- **RPO (data you can lose): up to 24 hours** with a daily dump. Turn on the managed database's point-in-time recovery to bring this to minutes.
- **RTO (time to be back): estimate 1 to 2 hours** (new database, restore, migrate check, start). This is an estimate, not a measurement: run a restore drill and replace the number.
- Uploaded files are not in the database dump; back them up with the storage provider's own versioning once R2 exists.

## 7. Updating
Build the new image, apply new migrations (steps 2 to 4) **before** starting it, then roll the containers one at a time using the readiness probe. Take a backup first. The development database `textileconnect_dev` has every migration up to `AddCreditNotes`. Three newer ones are **not applied anywhere**: `AddRfq` (two new tables), `AddInvoiceRegister` (new columns on `Invoices`, empty for old rows) and `AddProductPriceColumns` (two new columns on `Products`, **filled by the migration itself** from the price tiers, plus two indexes; on 20,000 products the fill took 0.4 s and the column add 11 s). All three only add; none drops or rewrites data.

## 8. Load test
`k6 run ops/load/catalogue-and-checkout.js` with `BASE_URL` set to a staging address (never production). It browses the catalogue and runs checkouts. Not run yet; the 20,000 product benchmark of [PERFORMANCE.md](PERFORMANCE.md) is also not run.

## 9. Before the first real customer
Finish F3 (real SMS, storage, Razorpay Route), the GST invoice (F4.1), the admin MFA, an outside security test ([SECURITY_REVIEW.md](SECURITY_REVIEW.md)), choose the log sink and error tracker, rehearse a restore, and check the legal text and brand/domain.
