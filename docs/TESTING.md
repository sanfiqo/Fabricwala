# Testing

There are two kinds of backend test and one set of client checks.

## 1. The fast suite (no setup)

```powershell
dotnet test TextileConnect.slnx
```

It needs no database and no secrets. It uses an in-memory store, test sign-in headers and fakes for the payment gateway, SMS and files, and it runs in under a minute.
The tests that need a real MySQL server are listed as **Skipped** (not passed), with the reason.

Useful filters: `--filter FullJourney`, `--filter "FullyQualifiedName~Payout"`.

Two things this suite cannot show, because the in-memory store has no transactions, no foreign keys and no row locks: that a failed save leaves nothing behind, and what happens when two requests meet at the same time. The MySQL suite covers those.

## 2. The MySQL suite (a real server)

It runs the money and order rules again on MySQL: checkout split, payment confirmation and webhook replay, refund on rejection and expiry, payout hold and release, claim hold, the whole sign-up to payout journey, and two checkouts for the last stock.

What you need: a MySQL 8 server where a user can create and drop databases. **Do not point it at your working database.** Every test creates its own database named `tc_test_<random>`, builds it from the real migrations, fills the master data, runs, and drops it. Nothing else on the server is touched.

```powershell
$env:TEXTILECONNECT_TEST_MYSQL = "Server=localhost;Port=3306;Uid=<user>;Pwd=<password>;SslMode=Disabled"   # the database name in it is ignored
dotnet test TextileConnect.slnx --filter "FullyQualifiedName~MySqlSuiteTests|FullyQualifiedName~MySqlFlowIntegrationTests|FullyQualifiedName~ProductSearchMySqlTests"
```

- A test takes a few seconds longer than its in-memory twin because it applies all migrations. That also checks the migrations themselves (foreign keys, indexes).
- To keep the database of a failing test for a look: `$env:TEXTILECONNECT_TEST_MYSQL_KEEP = "1"` (drop the `tc_test_*` databases by hand afterwards).
- `docs/FK_ORPHAN_CHECK.sql` is separate: run it on a database that already has data before applying a migration that adds foreign keys.

How it works: `MySqlStore.RunAsync` creates the database and, for the time of the test, makes `MarketplaceHttpFixture` use it instead of the in-memory store. `MySqlSuiteTests` simply calls existing tests inside it, so a rule is written once and checked on both stores. To add one, add a line to `MySqlSuiteTests`.

## 3. Contract tests that rewrite a file

| Test | Rewrites | When it fails |
| --- | --- | --- |
| `OpenApiExportTests` | `docs/openapi.json` | an endpoint or DTO changed. Run with `OPENAPI_EXPORT=1`, then commit the file |
| `ForeignKeyModelTests` | `docs/FK_ORPHAN_CHECK.sql` | a relation changed. Run with `FK_EXPORT=1`, then commit the file |

```powershell
$env:OPENAPI_EXPORT = "1"; dotnet test TextileConnect.slnx --filter OpenApiExport
$env:FK_EXPORT = "1"; dotnet test TextileConnect.slnx --filter ForeignKeyModelTests
```

## 4. Other checks

```powershell
dotnet list TextileConnect.slnx package --vulnerable --include-transitive    # must say "no vulnerable packages" for every project
cd client; npm ci; npm run lint; npm run build
cd client; npm run e2e:real     # browser run against the running API (script in client/package.json)
```

## 5. CI

`.github/workflows/ci.yml` runs on every push to `main`, `backend/**` and `frontend/**` and on every pull request:
build, the fast suite, the MySQL suite (a `mysql:8.0` service container), the vulnerable-package check (fails when one is found), and for `client/` `npm ci`, `npm run lint`, `npm run build`.
The workflow has not run on GitHub yet; the first push shows whether it needs a fix.
