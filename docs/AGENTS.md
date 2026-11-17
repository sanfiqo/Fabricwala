# AGENTS.md — TextileConnect

Read this file first in every session. It is the single source of rules for this repo.

## What this project is
TextileConnect (working name "Kashmir Textile Connect") is a multi-vendor B2B marketplace.
Verified manufacturers list wholesale textiles; verified retailers in Jammu & Kashmir order in bulk.
The platform earns a commission per order. The platform company can also sell through its own
seller account (Businesses.IsPlatformOwned = true).

This is a SEPARATE project. Do NOT use the GoalOrbit development skill or any GoalOrbit rules here.

## Paths
- This repo: `D:\2026 projects\TextileConnect`
- Old e-commerce project (READ-ONLY reference, never modify): `D:\2026 projects\Ecommerce-V1`
- Do not touch any other folder under `D:\2026 projects`.

## Stack (decided, do not change without asking)
- .NET 10, ASP.NET Core minimal APIs (endpoint files, no controllers), solution file `TextileConnect.slnx`
- EF Core with MySQL using `MySql.EntityFrameworkCore` (same provider as the old project)
- Auth: ASP.NET Core Identity + mobile OTP + secure HttpOnly cookie + antiforgery (`X-CSRF-TOKEN`). NO JWT for now.
- Frontend (later): React + Vite + TypeScript PWA in `client/`, Tailwind + shadcn/ui, TanStack Query
- Background jobs: `BackgroundService` (Hangfire is not used). Logging: Serilog.
- SMS OTP: MSG91 (behind `IOtpDelivery`; fake sender in Development; the real MSG91 sender is not built yet)
- WhatsApp: Meta WhatsApp Cloud API (behind `INotificationSender`)
- Payments: Razorpay Route (split settlement). Platform never holds a wallet balance.
- Files: Cloudflare R2 (behind `IFileStorage`)

## Solution structure
```
src/Sanfiqo.Identity/              reusable identity library (users, roles, OTP, cookie security)
    Users/ Otp/ Contacts/ Security/ Persistence/
src/TextileConnect.Domain/         entities, enums, business rules (no EF, no ASP.NET)
src/TextileConnect.Application/    services, DTOs, validators (FluentValidation), interfaces
src/TextileConnect.Infrastructure/ DbContext, EF configs, migrations, gateway/SMS/WhatsApp/storage clients
src/TextileConnect.Api/            endpoint files, middleware, DI composition, Swagger
tests/TextileConnect.Tests/        xUnit tests
docs/                              PHASES.md (task list), ARCHITECTURE.md (data model, flows, API)
```
Dependency direction: Api -> Application/Infrastructure/Sanfiqo.Identity; Infrastructure -> Application -> Domain.
Domain references nothing.

## Roles
`Admin`, `Retailer`, `Manufacturer` (later `Logistics`). Seeded at startup.
No MFA requirement on Retailer/Manufacturer policies. Admin MFA is a later TODO.

## Data rules
- Businesses own everything commercial. Users act for a business through `BusinessUsers` (BusinessId + UserId PK, BusinessRole Owner/Staff).
- Never put business fields on ApplicationUser.
- Keys: BIGINT auto-increment for business tables; Identity users keep string/GUID Id.
- Money: `decimal(18,2)`. All times UTC. `CreatedAt`, `UpdatedAt` on every table.
- Enums stored as int with C# enums.
- OrderItems store snapshots (title, size, colour, unit price, GST rate) at checkout.
- One `Orders` row per checkout; one `SellerOrders` row per manufacturer in that checkout.

## Business rules (enforce in Application layer AND endpoint policies)
1. A business with VerificationStatus != Approved cannot list products or place orders.
2. Order qty per product must be >= Moq; the matching PriceSlab sets UnitPrice.
3. Never trust prices from the client. Recalculate slab price, shipping, GST and commission on the server.
4. Commission = SellerOrder.SubTotal x (Business.CommissionRateOverride ?? PlatformSettings default).
5. Every query for business data is filtered by the current user's BusinessId (resource isolation), not only by role.
6. Every status change adds an OrderStatusHistory row and queues a notification.
7. Payment webhooks: verify signature, handler must be idempotent.

## Security rules
- Secrets (connection strings, HMAC keys, gateway keys) only in user-secrets / environment variables. Never in appsettings.json in git.
- Data Protection keys must be persisted (database) before production, not local disk.
- Business documents (GST, PAN) are private; serve via short-lived signed URLs.
- Rate limit auth and search endpoints.

## How to work (IMPORTANT)
0. For tables, columns, relationships, flows and endpoints, follow `docs/ARCHITECTURE.md`. Read only the section the current task needs.
1. Open `docs/PHASES.md`, take the FIRST unchecked task (or the task I name).
2. Plan briefly (max 5 lines), then implement.
3. Build and run tests. Add tests for new business rules and for resource isolation.
4. If build and tests pass: commit with message `<task id>: <short title>`, then tick the task `[x]` in PHASES.md in the same commit.
5. Continue to the next task in the SAME phase without waiting for me.
6. STOP and ask me only when:
   - build or tests fail and you cannot fix them in 2 attempts
   - a decision is not covered by this file
   - a task needs a secret, an account, or an external service setup
   - the phase is finished
7. At a stop, report in max 8 lines: tasks done, tests result, anything I must do.

## Token rules (strict)
- Do not scan the whole repo. Use search to find only the files you need.
- Skip bin/, obj/, node_modules/, client/dist/, Migrations/ (except the latest).
- Read only the relevant parts of large files. Do not re-read a file you already read this session.
- No long explanations. Short summaries only. Code snippets in replies max 10 lines.
- Do not print whole files back to me.

## Naming
- No "Customer" or "EcommerceOne" names in new code. Use Retailer / Manufacturer / Business.
- Async methods end with `Async`. One public class per file.
- API routes under `/api/v1/...`, kebab-case.
