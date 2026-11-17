# Performance

Target: **p95 below 300 ms** for the product list, search, filtered search and filter counts with **20,000 products**.

## Status

**Not measured yet.** The benchmark is written and checked (the seeder runs in the normal test suite) but it needs a real MySQL server, and none was available when it was written. Timings from the in-memory store would say nothing, so none are given here. Run it once and paste the table below.

## How to measure

```powershell
$env:TEXTILECONNECT_TEST_MYSQL = "Server=localhost;Port=3306;Uid=<user>;Pwd=<password>;SslMode=Disabled"   # a server where the user can create databases, see docs/TESTING.md
$env:TEXTILECONNECT_PERF = "1"
$env:TEXTILECONNECT_PERF_REPORT = "$PWD\perf-report.md"
dotnet test TextileConnect.slnx --filter CataloguePerformanceTests
```

It creates its own database from the migrations (including the full-text index), seeds 20,000 products (`CatalogueSeeder`: 40 sellers in 12 cities, 3 sizes, 2 price tiers and a photo each, always the same data), then calls the real endpoints in-process: 100 requests each after 5 warm-up calls, plus two cases with 8 requests at a time. The test **fails when any p95 is 300 ms or more** and writes the table to the report file. `TEXTILECONNECT_PERF_PRODUCTS` changes the size (for example 100000 to see how it grows). It is not part of CI: it takes a minute or more.

The numbers include the HTTP stack and JSON of the API but not a network, so a phone on a mobile network adds its own delay on top. Run it on a machine like the server that will be used; a laptop and the database on the same machine is the best case.

## Results

(paste the table from `perf-report.md` here, with the date and the machine)

## What to look at if a number is too high

These are reasoned from the queries, not measured:

- **List and count.** Every list runs a `COUNT` and then the page. Both join products, sellers and price tiers and work out each product's lowest price and seller city per row. If this is slow, store the lowest and highest price on the product (like `UnitsSold`) and the seller's city on the product, and index them.
- **"Contains" search** (words under 3 letters, stop words, non-English). It cannot use an index and scans title, description and material; it grows with the catalogue. The full-text path does not have this problem.
- **Filter counts** run about nine small queries (one per filter group). They can be run in parallel or cached for a minute if needed.
- **Pages deep in the list** (`page=50`) use `OFFSET`, which gets slower the deeper it goes; keyset paging would fix it if shoppers really go that deep.
- The indexes are in `docs/INDEXES.md`. Check a slow request with `EXPLAIN` on the SQL from the MySQL slow log.
