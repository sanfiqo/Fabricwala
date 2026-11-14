# Run TextileConnect locally

How to start the API on your machine with demo data, and sign in as the demo users. No secret value is written in this file or in git.

## 1. What you need

- .NET 10 SDK
- A MySQL database you can reach (local or remote), and an empty schema for it
- Two secrets of your own (see section 2). You choose their values.

## 2. Put your secrets in user-secrets (names only, you supply the values)

Secrets live outside git, in `dotnet user-secrets` for the API project (id `TextileConnect.Api`). Run each command yourself and type your own value where it says `<your value>`:

```powershell
dotnet user-secrets set "ConnectionStrings:TextileConnect" "<your MySQL connection string>" --project src/TextileConnect.Api
dotnet user-secrets set "UserContacts:LookupHmacKey" "<your random text, at least 32 bytes>" --project src/TextileConnect.Api
dotnet user-secrets set "Otp:StorageHmacKey" "<a different random text, at least 32 bytes>" --project src/TextileConnect.Api
```

| Secret name | What it is for | Needed to start locally |
|---|---|---|
| `ConnectionStrings:TextileConnect` | MySQL connection | yes |
| `UserContacts:LookupHmacKey` | Encrypts the lookup of mobile numbers. Without it nobody can sign in and the demo users are not created | yes |
| `Otp:StorageHmacKey` | Protects stored OTP challenges | yes |
| `DataProtection:KeyRingConnectionString` | Durable key ring (production) | no, not locally |
| `Payments:Razorpay:*`, `Otp:Msg91:*`, `WhatsApp:*`, `Storage:R2:*` | Real payment, SMS, WhatsApp and file storage | no. Development uses fakes |

The full list of reserved names is in [SECRETS.md](SECRETS.md). A random value can be made with `[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])`. Use a different value in production. Never commit a value. The git-ignored file `src/TextileConnect.Api/appsettings.Local.json` also works for local overrides, but user-secrets is preferred.

## 3. Create the database tables

The API does not migrate the database by itself. Apply the migrations once, and again after pulling new ones:

```powershell
dotnet ef database update --project src/TextileConnect.Infrastructure --startup-project src/TextileConnect.Api --context MarketplaceDbContext
dotnet ef database update --project src/Sanfiqo.Identity --startup-project src/TextileConnect.Api --context ApplicationIdentityDbContext
```

If `dotnet ef` is missing: `dotnet tool install --global dotnet-ef`.

Not verified in the night run: there was no MySQL server there, so these two commands (and a live start against MySQL) were never executed. The migration SQL was only generated and read offline. The identity context has no design-time factory, so its command starts the API host to build the context and needs the secrets from section 2. If either command fails, send the error back.

## 4. Start the API

```powershell
dotnet run --project src/TextileConnect.Api --launch-profile http
```

- Base URL: **http://localhost:5075** (the `https` profile adds https://localhost:7153)
- Swagger UI (Development only): http://localhost:5075/swagger
- Health check: http://localhost:5075/api/v1/health
- Machine readable contract for the frontend: [openapi.json](openapi.json). Screen by screen coverage: [API_SCREENS.md](API_SCREENS.md)

On every start in the `Development` environment the API adds the demo data below if it is not there yet (it never runs in Production). Run it again any time; nothing is duplicated.

## 5. Demo users and the development OTP

In Development there is no SMS. The one-time code is always **123456**. All numbers are fake.

| Who | Mobile number | Role | Business |
|---|---|---|---|
| Dev Admin | +919000000003 | Admin | none |
| Ramesh Sharma | +919000000001 | Retailer | Sharma Textiles, Indore (approved) |
| Mehta Stores | +919000000013 | Retailer | Mehta Stores, Jammu (approved) |
| Anil Kapoor | +919000000002 | Seller (Manufacturer) | Kapoor Knits, Ludhiana (approved) |
| Shree Kurti House | +919000000011 | Seller | Shree Kurti House, Jaipur (approved) |
| Gupta Garments | +919000000012 | Seller | Gupta Garments, Delhi (approved) |
| Raj Woollens | +919000000014 | Retailer until approved | Raj Woollens, Ludhiana (pending approval, approve it as the admin to try the queue) |

Demo products (all sellers): Kapoor Knits has Quilted puffer jacket (MOQ 50, 50+ 480, 200+ 459, 500+ 440), Fleece hoodie (one size nearly out of stock), Sherpa jacket and Wool cardigan (draft). Shree Kurti House has Cotton kurti set, Gupta Garments has Thermal innerwear set. Freight between these cities is seeded so prices and checkout work. Payments use the fake gateway: after `POST /api/v1/payments` call `POST /api/v1/payments/{gatewayOrderId}/dev-complete` to pay, or `?success=false` to fail.

If the demo users are missing, `UserContacts:LookupHmacKey` was not set at the first start. Set it and restart: businesses and products are already there and the owners are linked on the next start.

## 6. Signing in from a client (cookies and CSRF)

Sign-in uses a secure HttpOnly cookie, not a token. Keep cookies between calls (`credentials: "include"` in a browser, a cookie jar in curl).

1. `GET /api/v1/auth/csrf` returns a token. Send it as header `X-CSRF-TOKEN` on every POST/PUT/DELETE.
2. `POST /api/v1/auth/otp/request` with `{ "phoneNumber": "+919000000001" }` returns a `challengeId`.
3. `POST /api/v1/auth/otp/verify` with `{ "phoneNumber": "+919000000001", "challengeId": "<id>", "code": "123456" }` sets the cookie.
4. `GET /api/v1/auth/me` returns the profile (roles, business, language). `POST /api/v1/auth/refresh` renews the cookie after an admin approval, `POST /api/v1/auth/logout` signs out.

Errors always look like `{ "code": "...", "message": "...", "fields": { ... } }` (codes are listed in API_SCREENS.md). Repeating a checkout or payment needs the same `Idempotency-Key` header.

## 7. Frontend dev servers (CORS)

In Development the API accepts browser calls with credentials from `http://localhost:5173` and `http://127.0.0.1:5173`, and from the same port on your local network (private addresses such as `http://192.168.x.x:5173`), so a phone on the same Wi-Fi can open the Vite dev server. Add another origin with `Cors:FrontendOrigins` (a list) in user-secrets or an environment variable. Outside Development no origin is allowed unless `Cors:FrontendOrigin` or `Cors:FrontendOrigins` is configured.

## 8. Run the tests

```powershell
dotnet test TextileConnect.slnx
```

The tests use in-memory data and fakes only. They need no secrets and no MySQL. If an endpoint or DTO changes, the OpenAPI test fails until you refresh the file: `$env:OPENAPI_EXPORT="1"; dotnet test TextileConnect.slnx --filter OpenApiExport` and commit `docs/openapi.json`.
