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
The benchmark (`CataloguePerformanceTests`, run with `TEXTILECONNECT_PERF=1` and `TEXTILECONNECT_TEST_MYSQL`) was run for the first time. **It does not pass the 300 ms bar at 20,000 products.** Numbers are milliseconds; the laptop was doing other work at the same time, so treat them as an order of magnitude, not as a promise.

First run, before any change: a list page took **1,250 ms** (newest first), because MySQL looked at every product and asked about the seller and the price tiers of each one before it cut the first 20; the filter counts took **4.7 s** (nine SQL queries, each reading the whole catalogue).

What was changed (`CatalogueReader`, `CatalogueFacetSnapshot`):
- the page asks the seller and the price tiers one product at a time, so MySQL can walk the products in order and stop after the first page;
- the cheap tests on the product row are written first, and the questions about other tables last;
- the city of a seller is worked out once per request instead of once per product;
- the count of products is its own query, in the shape that is fastest for counting;
- the sort and the page are cut on narrow rows, and the products of the page are read afterwards;
- a sort or a filter by price joins the grouped price tiers;
- the filter counts are worked out in memory from one copy of the catalogue that is kept for 30 seconds (`CatalogueReader.SnapshotLifetime`; making a cold copy takes 0.7 to 1.4 s). They were checked against the old SQL counts on MySQL with 27 filter combinations (`FacetSnapshotTests`).

Benchmark table after the first group of changes (100 requests each, in-process API, memory cache on as in the real app):

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

Reading it:
- **Fine at 20,000:** a plain list (157 ms median), full-text search (48 to 100 ms), filter counts while the copy is warm (121 ms).
- **Not fine:** a sort by price (1.6 s), a search for a short word such as "xl" that has to scan (0.7 s), a search with a price sort (1 s), page 50 (0.75 s), eight list requests at the same moment (0.5 s median).
- A profile taken after the "narrow page" change shows the price sort page at about 0.5 s and its count at about 0.6 s on a busy machine: better, not yet under 300 ms.

What would bring these under 300 ms (not done, because each needs a schema change or a design decision):
1. Store the lowest and highest price on the product row (`Products.PriceFrom`, `PriceTo`), kept in step inside `MarketplaceDbContext.SaveChanges`, with an index on (Status, IsDeleted, PriceFrom, Id). A price sort and a price filter are then an index range and the price tiers are not joined at all.
2. An index on (Status, IsDeleted, Id), so that newest first and the count can use an index that is already in that order.
3. For a short-word search: a full-text index with the ngram parser (`WITH PARSER ngram`) instead of `LIKE '%xl%'`.
4. Keep the total for a few seconds in the memory cache per filter set, or stop sending the exact total beyond page 5.

At 5,000 products none of this matters in practice; the table is what a catalogue four times that size does. The benchmark stays red at 20,000 until one of the steps above is done; run it with `TEXTILECONNECT_PERF_PRODUCTS=5000` for a quick check.
