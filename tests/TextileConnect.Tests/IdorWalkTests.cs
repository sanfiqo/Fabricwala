using Microsoft.AspNetCore.Builder;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;

namespace TextileConnect.Tests;

/// <summary>
/// Business A has orders, payments, a shipment, a problem report, photos, saved products, addresses, a cart and an invoice. Business B is a brand new approved business that has nothing.
/// B then calls every signed-in route of the API that takes an id, with the ids of A's things, using every method the route accepts. Nothing may come back as a success:
/// not a read (that would show A's data), not a change (that would touch A's data). A signed-in business sees and changes only its own.
/// The same walk by A itself must succeed on many routes, which proves the ids really belong to something.
/// </summary>
public sealed class IdorWalkTests
{
    /// <summary>Routes where a signed-in business without data may rightly get a 2xx, with the reason. Keep this list short and explain every entry.</summary>
    private static readonly Dictionary<string, string> OpenToAnySignedInBusiness = new()
    {
        ["DELETE /api/v1/saved-products/{productId:long}"] = "removes only the caller's own saved row and says 204 when there is none (A's saved product is checked afterwards)",
        ["PUT /api/v1/saved-products/{productId:long}"] = "any retailer may save a product that is for sale; it creates the caller's own row"
    };

    /// <summary>Routes that list the caller's own records for someone else's id: they must answer an empty list.</summary>
    private static readonly HashSet<string> EmptyListForOthers = ["GET /api/v1/seller-orders/{sellerOrderId:long}/claims"];

    /// <summary>A's things have small ids. The attackers are created later, so what they own (an address from sign-up, say) has a higher id and is never tried.</summary>
    private static readonly long[] CandidateIds = [1, 2, 3, 4];

    private static void MapEverything(WebApplication app)
    {
        app.MapAdminEndpoints(); app.MapFeatureEndpoints(); app.MapRfqEndpoints(); app.MapInvoiceEndpoints(); app.MapStorefrontEndpoints(); app.MapBulkProductEndpoints(); app.MapStockAlertEndpoints();
        app.MapAdminDashboardEndpoints(); app.MapAdminListEndpoints(); app.MapAdminReportEndpoints(); app.MapProductModerationEndpoints();
        app.MapCategoryBannerEndpoints(); app.MapBusinessSuspensionEndpoints();
    }

    private sealed record Route(string Pattern, string Method, RoutePattern Parsed);

    private static IEnumerable<Route> SignedInRoutesWithIds(IServiceProvider services)
        => services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith("/api/v1/", StringComparison.Ordinal) && e.RoutePattern.Parameters.Count > 0
                && e.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0 && e.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"]).Select(m => new Route(e.RoutePattern.RawText!, m, e.RoutePattern)))
            .DistinctBy(r => (r.Pattern, r.Method));

    /// <summary>The address to call: every id parameter takes the candidate id, anything else a plausible word.</summary>
    private static string Fill(Route route, long id)
    {
        var path = route.Pattern;
        foreach (var parameter in route.Parsed.Parameters)
        {
            var value = parameter.Name switch
            {
                "rateType" => "PerPiece",
                "slug" => "puffer",
                "pincode" => "452001",
                "gatewayOrderId" => "dev_unknown",
                "docType" => "Gst",
                _ => id.ToString()
            };
            var start = path.IndexOf("{" + parameter.Name, StringComparison.Ordinal);
            var end = path.IndexOf('}', start);
            path = path[..start] + value + path[(end + 1)..];
        }
        return path;
    }

    /// <summary>
    /// A body that passes the route's own checks, so the request reaches the "is this yours?" question instead of stopping at "the body is wrong" (400).
    /// For each of these routes the attackers must get 403 or 404: that proves the ownership check ran. Routes not listed get an empty JSON body.
    /// </summary>
    private static readonly Dictionary<string, string> RealBodies = new()
    {
        ["POST /api/v1/seller-orders/{sellerOrderId:long}/claims"] = """{"reason":"Damaged","pieces":1,"note":"x"}""",
        ["POST /api/v1/seller/claims/{claimId:long}/respond"] = """{"response":"We will check"}""",
        ["POST /api/v1/seller/orders/{id:long}/reject"] = """{"reason":"x"}""",
        ["POST /api/v1/seller/orders/{id:long}/dispatch"] = """{"lrNumber":"LR-1"}""",
        ["POST /api/v1/seller/orders/{id:long}/status"] = """{"status":"Accepted"}""",
        ["POST /api/v1/seller/orders/{id:long}/shipment"] = """{"status":"InTransit"}""",
        ["POST /api/v1/orders/{id:long}/rating"] = """{"stars":3}""",
        ["PUT /api/v1/account/addresses/{id:long}"] = """{"line1":"Road 1","pincode":"452001"}""",
        ["PUT /api/v1/cart/items/{id:long}"] = """{"variantId":1,"quantity":100}""",
        ["PUT /api/v1/seller/products/{id:long}"] = """{"title":"Hacked","publish":false}""",
        ["PUT /api/v1/seller/products/{id:long}/active"] = """{"active":false}"""
    };

    private static HttpRequestMessage Build(Route route, long id)
    {
        var request = new HttpRequestMessage(new HttpMethod(route.Method), Fill(route, id));
        var key = $"{route.Method} {route.Pattern}";
        if (route.Pattern.EndsWith("/photos") || route.Pattern.TrimEnd('/').EndsWith("/media"))
            request.Content = new MultipartFormDataContent { { new ByteArrayContent(ImageVariantMakerTests.Image(SkiaSharp.SKEncodedImageFormat.Png, 200, 100, SkiaSharp.SKColors.Red)), "file", "p.png" } };
        else if (route.Method is "POST" or "PUT" or "PATCH") request.Content = new StringContent(RealBodies.GetValueOrDefault(key, "{}"), Encoding.UTF8, "application/json");
        return request;
    }

    private static bool IsSuccess(HttpStatusCode status) => (int)status is >= 200 and < 300;

    [Fact]
    public async Task A_business_without_data_gets_no_success_from_any_route_that_takes_an_id_with_another_businesss_idsAsync()
    {
        await using var fixture = await FullJourneyHttpTests.StartAsync(MapEverything);
        var (retailerA, sellerA, adminA, _, _, variantId, slug, addressId) = await FullJourneyHttpTests.PrepareAsync(fixture);

        // ---- business A gets real data of every kind
        var (orderId, sellerOrderId, _) = await FullJourneyHttpTests.OrderAsync(retailerA, variantId, addressId, "idor-order-1", pay: true);
        await sellerA.PostAsync($"/api/v1/seller/orders/{sellerOrderId}/accept", null);
        await sellerA.PostAsJsonAsync($"/api/v1/seller/orders/{sellerOrderId}/dispatch", new { lrNumber = "LR-IDOR" });
        await retailerA.PostAsJsonAsync($"/api/v1/seller-orders/{sellerOrderId}/claims", new { reason = "Damaged", pieces = 5, note = "Wet" });
        using (var form = new MultipartFormDataContent { { new ByteArrayContent(ImageVariantMakerTests.Image(SkiaSharp.SKEncodedImageFormat.Png, 300, 200, SkiaSharp.SKColors.Red)), "file", "p.png" } })
            await sellerA.PostAsync($"/api/v1/seller/products/{CandidateProductId(fixture)}/media", form);
        var productId = CandidateProductId(fixture);
        await retailerA.PutAsync($"/api/v1/saved-products/{productId}", null);
        await retailerA.GetAsync($"/api/v1/invoices/seller-orders/{sellerOrderId}/download");
        await retailerA.PostAsJsonAsync("/api/v1/cart/items", new { variantId, quantity = 100 });
        await retailerA.PostAsync($"/api/v1/orders/{orderId}/confirm-delivery", null);
        await retailerA.PostAsJsonAsync($"/api/v1/orders/{orderId}/rating", new { stars = 5 });

        // ---- control: A itself succeeds on many id routes, so the ids are real
        var routes = SignedInRoutesWithIds(fixture.Services).ToList();
        Assert.True(routes.Count > 40, $"Only {routes.Count} signed-in routes with an id were found.");
        var succeededForOwner = new HashSet<string>();
        foreach (var route in routes.Where(r => r.Method == "GET"))
            foreach (var (client, _) in new[] { (retailerA, "retailer"), (sellerA, "seller"), (adminA, "admin") })
                foreach (var id in CandidateIds)
                    using (var response = await client.SendAsync(Build(route, id)))
                        if (IsSuccess(response.StatusCode)) succeededForOwner.Add(route.Pattern);
        Assert.True(succeededForOwner.Count >= 8, $"A got a success on only {succeededForOwner.Count} id routes: {string.Join(", ", succeededForOwner)}");

        // ---- the attackers: approved, empty
        var shopB = await fixture.ClientAsync("Retailer", "idor-shop-b");
        var shopBId = await FullJourneyHttpTests.SignUpAsync(shopB, "Retailer", "Other Shop", "452001");
        var factoryB = await fixture.ClientAsync("Manufacturer", "idor-factory-b");
        var factoryBId = await FullJourneyHttpTests.SignUpAsync(factoryB, "Manufacturer", "Other Factory", "141001");
        await adminA.PostAsync($"/api/v1/admin/businesses/{shopBId}/approve", null);
        await adminA.PostAsync($"/api/v1/admin/businesses/{factoryBId}/approve", null);
        using var retailerB = await fixture.ClientAsync("Retailer", "idor-shop-b", shopBId);
        using var sellerB = await fixture.ClientAsync("Manufacturer", "idor-factory-b", factoryBId);
        using var anonymous = new HttpClient { BaseAddress = new Uri(fixture.BaseAddress) };

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TextileConnect.Infrastructure.Persistence.MarketplaceDbContext>();
            var theirs = db.BusinessAddresses.Where(x => x.BusinessId == shopBId || x.BusinessId == factoryBId).Select(x => x.Id).ToList();
            Assert.DoesNotContain(theirs, id => CandidateIds.Contains(id)); // test set-up: what the attackers own must not be among the ids that are tried
        }
        var leaks = new SortedSet<string>(StringComparer.Ordinal);
        var notGated = new SortedSet<string>(StringComparer.Ordinal);
        var calls = 0;
        foreach (var route in routes)
        {
            if (OpenToAnySignedInBusiness.ContainsKey($"{route.Method} {route.Pattern}")) { calls += 2 * CandidateIds.Length; continue; }
            foreach (var (client, who) in new[] { (retailerB, "retailer B"), (sellerB, "seller B") })
                foreach (var id in CandidateIds)
                {
                    using var response = await client.SendAsync(Build(route, id));
                    calls++;
                    if (RealBodies.ContainsKey($"{route.Method} {route.Pattern}") && response.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.Forbidden))
                        notGated.Add($"{who}: {route.Method} {Fill(route, id)} answered {(int)response.StatusCode}");
                    if (!IsSuccess(response.StatusCode)) continue;
                    if (EmptyListForOthers.Contains($"{route.Method} {route.Pattern}") && (await response.Content.ReadAsStringAsync()) == "[]") continue;
                    leaks.Add($"{who}: {route.Method} {Fill(route, id)} answered {(int)response.StatusCode}");
                }
            // nobody signed in: always refused
            using var refused = await anonymous.SendAsync(Build(route, 1));
            if (refused.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest))
                leaks.Add($"anonymous: {route.Method} {Fill(route, 1)} answered {(int)refused.StatusCode}");
        }

        Assert.Equal(routes.Count * 2 * CandidateIds.Length, calls); // every route, both attackers, every id

        // B also tried every changing route (POST, PUT, DELETE) with A's ids. Whatever they answered, A's data must be exactly as it was.
        var order = await retailerA.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}");
        Assert.Equal("ProblemReported", order.GetProperty("status").GetString()); // delivered, with A's open problem report
        Assert.Equal("Paid", order.GetProperty("paymentStatus").GetString());
        Assert.Single((await retailerA.GetFromJsonAsync<JsonElement>("/api/v1/account/addresses")).EnumerateArray());
        Assert.Equal(1, (await retailerA.GetFromJsonAsync<JsonElement>("/api/v1/cart")).GetProperty("groups").EnumerateArray().Sum(g => g.GetProperty("items").GetArrayLength()));
        Assert.Single((await sellerA.GetFromJsonAsync<JsonElement>($"/api/v1/seller/products/{productId}")).GetProperty("media").EnumerateArray());
        Assert.True((await retailerA.GetFromJsonAsync<JsonElement>("/api/v1/saved-products")).GetProperty("total").GetInt32() == 1);
        Assert.True(notGated.Count == 0, "These changing routes were given a body that is right, yet did not answer 403 or 404 to a business that owns nothing, so the ownership check may not have been reached:\n  " + string.Join("\n  ", notGated));
        Assert.True(leaks.Count == 0, "These calls by a business with no data, or by nobody, got a success. They would show or change what belongs to someone else:\n  " + string.Join("\n  ", leaks));
    }

    private static long CandidateProductId(MarketplaceHttpFixture fixture)
    {
        using var scope = fixture.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TextileConnect.Infrastructure.Persistence.MarketplaceDbContext>().Products.Select(x => x.Id).OrderBy(x => x).First();
    }
}
