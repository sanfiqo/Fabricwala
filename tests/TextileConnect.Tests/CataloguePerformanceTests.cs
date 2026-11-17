using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

/// <summary>A test that is only run on purpose: it needs a MySQL server and TEXTILECONNECT_PERF=1, and it takes a minute or more.</summary>
public sealed class PerfFactAttribute : FactAttribute
{
    public PerfFactAttribute()
    {
        if (!MySqlStore.IsConfigured) Skip = $"Set {MySqlStore.Variable} (see docs/TESTING.md) to run the benchmark.";
        else if (Environment.GetEnvironmentVariable("TEXTILECONNECT_PERF") != "1") Skip = "Set TEXTILECONNECT_PERF=1 to run the benchmark (docs/PERFORMANCE.md).";
    }
}

public sealed class CataloguePerformanceTests
{
    [Fact]
    public async Task The_seeder_makes_valid_searchable_products_in_the_in_memory_storeAsync()
    {
        await using var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapProductEndpoints());
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        await CatalogueSeeder.SeedAsync(db, 300, sellers: 5);

        Assert.Equal(300, await db.Products.CountAsync(x => x.Slug.StartsWith("seed-product-")));
        using var client = await fixture.ClientAsync(null);
        var list = await client.GetFromJsonAsyncSafe("/api/v1/products?pageSize=20");
        Assert.Equal(300, list.GetProperty("total").GetInt32());
        Assert.Equal(20, list.GetProperty("items").GetArrayLength());
        Assert.True((await client.GetFromJsonAsyncSafe("/api/v1/products?q=jacket")).GetProperty("total").GetInt32() > 0);
        Assert.True((await client.GetFromJsonAsyncSafe("/api/v1/products/filters")).GetProperty("materials").GetArrayLength() >= 5);
    }

    private sealed record Measurement(string Name, double P50, double P95, double Max, int Requests, int Failures);

    private static async Task<Measurement> MeasureAsync(HttpClient client, string name, string path, int requests = 100, int parallel = 1)
    {
        for (var i = 0; i < 5; i++) await client.GetAsync(path); // warm up: connections, query plans
        var times = new List<double>();
        var failures = 0;
        async Task OneAsync()
        {
            var watch = Stopwatch.StartNew();
            using var response = await client.GetAsync(path);
            await response.Content.ReadAsByteArrayAsync();
            watch.Stop();
            lock (times) { times.Add(watch.Elapsed.TotalMilliseconds); if (response.StatusCode != HttpStatusCode.OK) failures++; }
        }
        if (parallel == 1) for (var i = 0; i < requests; i++) await OneAsync();
        else for (var round = 0; round < requests / parallel; round++) await Task.WhenAll(Enumerable.Range(0, parallel).Select(_ => OneAsync()));
        times.Sort();
        double At(double q) => times[Math.Min(times.Count - 1, (int)Math.Ceiling(q * times.Count) - 1)];
        return new Measurement(name, At(0.5), At(0.95), times[^1], times.Count, failures);
    }

    private static readonly (string Name, string Path, int Parallel)[] Cases = {
        ("List, newest first", "/api/v1/products?pageSize=20", 1),
        ("List, page 50", "/api/v1/products?pageSize=20&page=50", 1),
        ("List, most popular", "/api/v1/products?sort=popular&pageSize=20", 1),
        ("List, price low to high", "/api/v1/products?sort=price-asc&pageSize=20", 1),
        ("Search, one word (full-text)", "/api/v1/products?q=jacket", 1),
        ("Search, two words (full-text)", "/api/v1/products?q=quilted+wool", 1),
        ("Search, short word (contains)", "/api/v1/products?q=xl", 1),
        ("Filtered search", "/api/v1/products?q=wool&material=Wool&minPrice=200&maxPrice=900&size=M&sort=price-asc", 1),
        ("Filter counts, no filter", "/api/v1/products/filters", 1),
        ("Filter counts, with search and filters", "/api/v1/products/filters?q=wool&size=M&minPrice=200", 1),
        ("Search, 8 requests at a time", "/api/v1/products?q=jacket", 8),
        ("List, 8 requests at a time", "/api/v1/products?pageSize=20", 8)
    };

    /// <summary>20,000 products, 40 sellers. Writes a table and fails when a p95 is 300 ms or more.</summary>
    [PerfFact]
    public Task Catalogue_list_search_and_filters_stay_under_300_ms_at_p95_with_20000_products() => MySqlStore.RunAsync(async () =>
    {
        var products = int.TryParse(Environment.GetEnvironmentVariable("TEXTILECONNECT_PERF_PRODUCTS"), out var n) ? n : 20_000;
        MarketplaceHttpFixture.SearchLimitOverride.Value = 1_000_000;   // the benchmark sends thousands of searches from one address: the per-client limit is not what it measures
        // The real app keeps the copy of the catalogue behind the filter counts for 30 seconds (it has a memory cache); so does this one. The cold cost is in the profile test.
        await using var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapProductEndpoints(), builder => builder.Services.AddMemoryCache());
        var seeding = Stopwatch.StartNew();
        using (var scope = fixture.Services.CreateScope())
            await CatalogueSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>(), products);
        seeding.Stop();
        using var client = await fixture.ClientAsync(null);

        var cases = Cases;
        var results = new List<Measurement>();
        foreach (var (name, path, parallel) in cases) results.Add(await MeasureAsync(client, name, path, requests: parallel == 1 ? 100 : 160, parallel));

        var report = new StringBuilder();
        report.AppendLine($"Measured {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC on {Environment.MachineName}: {products} products, {Environment.ProcessorCount} CPU threads, in-process API, MySQL as configured in TEXTILECONNECT_TEST_MYSQL. Seeding took {seeding.Elapsed.TotalSeconds:0} s.");
        report.AppendLine();
        report.AppendLine("| Request | p50 ms | p95 ms | max ms | requests | failed |");
        report.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");
        foreach (var r in results) report.AppendLine($"| {r.Name} | {r.P50:0} | {r.P95:0} | {r.Max:0} | {r.Requests} | {r.Failures} |");
        var path2 = Environment.GetEnvironmentVariable("TEXTILECONNECT_PERF_REPORT");
        if (!string.IsNullOrWhiteSpace(path2)) await File.WriteAllTextAsync(path2, report.ToString());

        Assert.True(results.All(r => r.Failures == 0), "Some requests failed.\n" + report);
        Assert.True(results.All(r => r.P95 < 300), "A p95 is 300 ms or more.\n" + report);
    });

    /// <summary>
    /// Finds out where the time goes: for each request of the benchmark it sends it once (after a warm-up) and writes every SQL command with its time to the file in
    /// TEXTILECONNECT_PERF_PROFILE. Not a pass or fail test.
    /// </summary>
    [PerfFact]
    public Task Profile_the_sql_of_the_benchmark_requests() => MySqlStore.RunAsync(async () =>
    {
        var target = Environment.GetEnvironmentVariable("TEXTILECONNECT_PERF_PROFILE");
        if (string.IsNullOrWhiteSpace(target)) return;
        var products = int.TryParse(Environment.GetEnvironmentVariable("TEXTILECONNECT_PERF_PRODUCTS"), out var n) ? n : 20_000;
        MarketplaceHttpFixture.SearchLimitOverride.Value = 1_000_000;
        PerfSql.Enabled = true;
        await using var fixture = await MarketplaceHttpFixture.StartAsync(app => app.MapProductEndpoints());
        using (var scope = fixture.Services.CreateScope())
            await CatalogueSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>(), products);
        using var client = await fixture.ClientAsync(null);

        var text = new StringBuilder();
        text.AppendLine($"SQL profile, {products} products, {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        foreach (var (name, path, parallel) in Cases.Where(c => c.Parallel == 1))
        {
            await client.GetAsync(path);                                   // warm-up: connection, plan, JIT
            PerfSql.Lines.Clear();
            var watch = Stopwatch.StartNew();
            using var response = await client.GetAsync(path);
            watch.Stop();
            text.AppendLine().AppendLine($"## {name}  [{path}]  status {(int)response.StatusCode}, {watch.ElapsedMilliseconds} ms in total");
            foreach (var line in PerfSql.Lines.ToArray())
                text.AppendLine(line.Length > 2500 ? line[..2500] + " ..." : line).AppendLine();
        }
        await File.WriteAllTextAsync(target, text.ToString());
    });
}

internal static class HttpJsonExtensions
{
    public static async Task<System.Text.Json.JsonElement> GetFromJsonAsyncSafe(this HttpClient client, string path)
        => await System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsync<System.Text.Json.JsonElement>(client, path);
}
