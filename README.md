# TextileConnect (Fabricwala)

A multi-vendor B2B textile marketplace for India: verified manufacturers list wholesale products, verified retailers order in bulk, the platform takes a commission.
Backend: .NET 10 minimal API + MySQL. Frontend: React + Vite PWA in `client/`.

## Try it in about 5 minutes (Development)
Needs: .NET 10 SDK, Node 20+, MySQL 8 (or Docker, see below).

```
# 1. secrets (once), from the repo root
dotnet user-secrets set "ConnectionStrings:TextileConnect" "Server=localhost;Database=textileconnect;Uid=...;Pwd=..." --project src/TextileConnect.Api
dotnet user-secrets set "UserContacts:LookupHmacKey" "<random 32+ chars>" --project src/TextileConnect.Api
dotnet user-secrets set "Otp:StorageHmacKey" "<another random 32+ chars>" --project src/TextileConnect.Api
# 2. tables (both contexts, see docs/RUN_LOCAL.md)
# 3. API  -> http://localhost:5075
dotnet run --project src/TextileConnect.Api
# 4. client -> http://localhost:5173
cd client && npm ci && npm run dev
```
In Development the sign-in code is always `123456` and payments use a fake gateway. With Docker instead: copy `.env.example` to `.env`, then `docker compose up -d mysql`, `docker compose run --rm migrate`, `docker compose up -d api` (never run so far).

## Check it
```
dotnet test TextileConnect.slnx        # 547 pass, 25 skip (need MySQL: set TEXTILECONNECT_TEST_MYSQL)
```

## Where things are
| Read | For |
| --- | --- |
| [docs/AGENTS.md](docs/AGENTS.md) | rules for working in this repo |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | how it is built, rules, flows |
| [docs/PHASES.md](docs/PHASES.md), [docs/PROGRESS.md](docs/PROGRESS.md) | task list and honest status |
| [docs/API_REFERENCE.md](docs/API_REFERENCE.md), [docs/openapi.json](docs/openapi.json) | every route (generated) |
| [docs/DATA_MODEL.md](docs/DATA_MODEL.md) | every table (generated) |
| [docs/DEPLOY.md](docs/DEPLOY.md) | going live, backups, probes |
| [docs/SECURITY_REVIEW.md](docs/SECURITY_REVIEW.md) | security status and gaps |
| [docs/TESTING.md](docs/TESTING.md), [docs/RUN_LOCAL.md](docs/RUN_LOCAL.md), [docs/SECRETS.md](docs/SECRETS.md) | tests, local setup, secrets |

## Status in one line
Order journey works end to end in Development. **Not ready for real customers:** no real SMS, file storage or seller payouts, GST on delivery and on the platform fee undecided, no admin MFA, and the MySQL tests, Docker, backups and load test have not been run. Details in `docs/PHASES.md`.
