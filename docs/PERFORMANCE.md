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

## Measured on 17-18 Nov 2026 (20,000 products, 40 sellers, MySQL 8 on the same laptop, one client)
The benchmark (`CataloguePerformanceTests`, run with `TEXTILECONNECT_PERF=1` and `TEXTILECONNECT_TEST_MYSQL`) was run for the first time. Numbers are milliseconds; the laptop was doing other work at the same time, so treat them as an order of magnitude, not as a promise. Each request was sent 100 times (160 for the two "at a time" rows), by one client, after a warm-up.

**Where it started** (first single-request profile): a list page took 1,250 ms (newest first), because MySQL looked at every product and asked about the seller and the price tiers of each one before it cut the first 20; the filter counts took 4.7 s (nine SQL queries, each reading the whole catalogue).

**What was changed** (`CatalogueReader`, `CatalogueFacetSnapshot`, `MarketplaceDbContext`):
- the page asks the seller one product at a time, so MySQL walks the products in order and stops after the first page;
- the cheap tests on the product row are written first, the questions about other tables last;
- the city of a seller is worked out once per request instead of once per product;
- the count is its own query, in the shape that is fastest for counting;
- the sort and the page are cut on narrow rows, then the products of the page are read;
- **`Products.PriceFrom` and `PriceTo`** hold the lowest and highest price of the price tiers and are set by `MarketplaceDbContext` on every save, with an index on (Status, IsDeleted, PriceFrom, Id): a sort or a filter by price is an index range, no tier is read (`ProductPriceColumnsTests` checks every way of writing tiers, on MySQL too);
- the filter counts are worked out in memory from one copy of the listed products, kept for 30 seconds when the host has a memory cache (`CatalogueReader.SnapshotLifetime`; a cold copy takes about 0.4 to 1.4 s); they were checked against the old SQL counts on MySQL with 27 filter combinations (`FacetSnapshotTests`).

**After the first group of changes** (before the price columns; the price sort was the worst, 1.6 s):

| Request | p50 ms | p95 ms | max ms | requests | failed |
| --- | ---: | ---: | ---: | ---: | ---: |
| List, newest first | 157 | 311 | 438 | 100 | 0 |
| List, page 50 | 752 | 1076 | 1478 | 100 | 0 |
| List, most popular | 210 | 353 | 818 | 100 | 0 |
| List, price low to high | 1640 | 1977 | 2477 | 100 | 0 |
| Search, one word (full-text) | 100 | 207 | 251 | 100 | 0 |
| Search, two words (full-text) | 48 | 96 | 124 | 100 | 0 |
| Search, short word (contains) | 663 | 921 | 1132 | 100 | 0 |
| Filtered search | 964 | 1282 | 1382 | 100 | 0 |
| Filter counts, no filter | 121 | 221 | 258 | 100 | 0 |
| Filter counts, with search and filters | 93 | 157 | 191 | 100 | 0 |
| Search, 8 requests at a time | 224 | 456 | 2122 | 160 | 0 |
| List, 8 requests at a time | 506 | 745 | 972 | 160 | 0 |

**After the price columns** (the present state):

| Request | p50 ms | p95 ms | max ms | requests | failed |
| --- | ---: | ---: | ---: | ---: | ---: |
| List, newest first | 68 | 85 | 120 | 100 | 0 |
| List, page 50 | 183 | 308 | 349 | 100 | 0 |
| List, most popular | 56 | 59 | 68 | 100 | 0 |
| List, price low to high | 56 | 60 | 71 | 100 | 0 |
| Search, one word (full-text) | 42 | 46 | 55 | 100 | 0 |
| Search, two words (full-text) | 27 | 29 | 30 | 100 | 0 |
| Search, short word (contains) | 271 | 397 | 511 | 100 | 0 |
| Filtered search | 67 | 115 | 172 | 100 | 0 |
| Filter counts, no filter | 58 | 96 | 104 | 100 | 0 |
| Filter counts, with search and filters | 45 | 48 | 52 | 100 | 0 |
| Search, 8 requests at a time | 129 | 158 | 886 | 160 | 0 |
| List, 8 requests at a time | 223 | 237 | 243 | 160 | 0 |

Reading it:
- **Under 300 ms at p95 at 20,000 products:** every request, with two exceptions.
- **Not quite:** a search for a short word such as "xl" that has to scan the text (p50 271, p95 397) and page 50 (p95 308). Both are close; the first would need a full-text index with the ngram parser (`WITH PARSER ngram`) instead of `LIKE '%xl%'`, the second a cheaper total or a keyset page.
- The benchmark therefore still reports a failure at 20,000 products (it asserts that every p95 is under 300 ms). At 5,000 products run it with `TEXTILECONNECT_PERF_PRODUCTS=5000`.
- Filter counts are fast because of the kept copy; the first request after 30 seconds of quiet pays for making it (a cold copy is in the SQL profile: `TEXTILECONNECT_PERF_PROFILE=<file>` with the test `Profile_the_sql_of_the_benchmark_requests`).
