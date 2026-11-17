# Search, filters and popularity

## Searching by words (`GET /api/v1/products?q=`)

- A search made only of plain English words of three letters or more goes through the MySQL full-text index `FTX_Products_Search` (title, description, material). Every word must be there, in any order, and each word may be the start of a longer word: `jack quilted` finds "Quilted puffer jacket". It is added by the migration `AddProductFullText`.
- Everything else falls back to "contains" matching on the same three fields: one or two letters (`xl`), a word the index ignores (`the`, `for`, `with` ...), more than eight words, or letters that are not English (Hindi). The fallback is slower on a big catalogue but always finds things.
- The words are reduced to letters and digits before they reach MySQL, so quotes, `+`, `-`, `(`, `*` and SQL cannot change the query. [FilterFacetsHttpTests, ProductSearchMySqlTests]
- Other filters (category, price, size, colour, city, state, material, season, MOQ) apply on top of the text search. The response, the paging and the sort are the same as before.
- The result is not sorted by how well it matches; the sort is the one the caller asks for (`popular`, `price-asc`, `price-desc`, `moq-asc`, newest by default).
- The in-memory test store has no full-text index, so tests there use the "contains" path. `ProductSearchMySqlTests` (needs a MySQL server, see TESTING.md) checks the index path.

## Filter counts (`GET /api/v1/products/filters`)

Same query parameters as the list (page, page size and sort are ignored). It returns `total` and, for each filter, the values with the number of products they would give: `categories` and `cities` (id, name, count), `states` (id, name, code, count), `materials`, `seasons`, `sizes`, `colours` (value, count), and `price` (`min`, `max` of the lowest tier price).

Each list is counted with every other filter applied but **not its own**, so after choosing "Cotton" the sheet still shows how many "Wool" products the other filters leave. Sizes and colours count only active variants, and a product counts once for a size even if it has several colours of it. Each list holds at most 50 values, biggest first.

## Popularity (`sort=popular`, `unitsSold`)

`Product.UnitsSold` is a stored number: the pieces of seller orders that the retailer has confirmed as delivered. It is raised once, at confirmation. Rejected, cancelled and expired orders never raise it; a problem report or a refund does not lower it. (Before F5.1 it was a sum over order items worked out for every product on every search, and it counted paid orders.) A new product therefore has no popularity until its first delivery. The migration fills the counter for existing products from their delivered orders.

A concurrent delivery of the same product can lose one increment; for a sort order that is accepted.

## Reserved name

The product page name `filters` is reserved for the counts route: a seller who calls a product "Filters" gets the page name `filters-product`.
