using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Api;
using TextileConnect.Domain.Businesses;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class PaymentHttpTests
{
    internal static async Task<MarketplaceHttpFixture> StartAsync(Action<Microsoft.AspNetCore.Builder.WebApplication>? extra = null)
    {
        var fixture = await CheckoutHttpTests.StartAsync(extra: app => { app.MapPaymentEndpoints(); extra?.Invoke(app); });
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        db.BusinessUsers.AddRange(new BusinessUser { BusinessId = 10, UserId = "seller-user", Role = BusinessMemberRole.Owner },
            new BusinessUser { BusinessId = 30, UserId = "retailer-user", Role = BusinessMemberRole.Owner });
        await db.SaveChangesAsync();
        return fixture;
    }

    internal static async Task<JsonElement> PlaceOrderAsync(HttpClient client)
    {
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/cart/items", new { variantId = 1, quantity = 60 })).StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/checkout") { Content = JsonContent.Create(new { deliveryAddressId = 100 }) };
        request.Headers.Add("Idempotency-Key", "order-key-" + Guid.NewGuid().ToString("N"));
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    internal static Task<HttpResponseMessage> StartPaymentAsync(HttpClient client, long orderId, string? key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments") { Content = JsonContent.Create(new { orderId }) };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> WebhookAsync(HttpClient client, string payload, string? secret = "development-test-secret")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook") { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        if (secret is not null)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            request.Headers.Add("X-Razorpay-Signature", Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant());
        }
        return client.SendAsync(request);
    }

    private static string Captured(string gatewayOrderId, long paise, string eventName = "payment.captured")
        => JsonSerializer.Serialize(new { @event = eventName, payload = new { payment = new { entity = new { id = "pay_" + gatewayOrderId, order_id = gatewayOrderId, amount = paise } } } });

    internal static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Payment_start_is_idempotent_and_completion_marks_the_order_paid_onceAsync()
    {
        await using var fixture = await StartAsync();
        using var client = await fixture.ClientAsync("Retailer", "retailer-user", RetailerId);
        var order = await PlaceOrderAsync(client);
        var orderId = order.GetProperty("id").GetInt64();
        var total = order.GetProperty("grandTotal").GetDecimal();

        Assert.Equal(HttpStatusCode.BadRequest, (await StartPaymentAsync(client, orderId, null)).StatusCode);
        var session = await JsonAsync(await StartPaymentAsync(client, orderId, "pay-key-0001"));
        var gatewayOrderId = session.GetProperty("gatewayOrderId").GetString()!;
        Assert.Equal(total, session.GetProperty("amount").GetDecimal());
        Assert.Equal("INR", session.GetProperty("currency").GetString());
        Assert.Equal("Pending", session.GetProperty("status").GetString());
        Assert.Equal(gatewayOrderId, (await JsonAsync(await StartPaymentAsync(client, orderId, "pay-key-0001"))).GetProperty("gatewayOrderId").GetString());
        Assert.Equal(gatewayOrderId, (await JsonAsync(await StartPaymentAsync(client, orderId, "pay-key-0002"))).GetProperty("gatewayOrderId").GetString()); // pending payment reused

        var paid = await JsonAsync(await client.PostAsync($"/api/v1/payments/{gatewayOrderId}/dev-complete", null));
        Assert.Equal("Paid", paid.GetProperty("status").GetString());
        await JsonAsync(await client.PostAsync($"/api/v1/payments/{gatewayOrderId}/dev-complete", null)); // repeat changes nothing
        Assert.Equal("Paid", (await client.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}")).GetProperty("paymentStatus").GetString());
        Assert.Equal("Paid", (await client.GetFromJsonAsync<JsonElement>($"/api/v1/payments/{gatewayOrderId}")).GetProperty("status").GetString());

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var sellerOrder = await db.SellerOrders.Include(x => x.History).SingleAsync(x => x.OrderId == orderId);
        Assert.InRange((sellerOrder.AcceptDeadlineUtc!.Value - DateTime.UtcNow).TotalHours, 23.9, 24.1);
        Assert.Equal(2, sellerOrder.History.Count);
        Assert.Equal(1, await db.Notifications.CountAsync(x => x.UserId == "seller-user" && x.Title == "New order"));
        Assert.Equal(1, await db.Notifications.CountAsync(x => x.UserId == "retailer-user" && x.Title == "Payment received"));
        var blocked = await StartPaymentAsync(client, orderId, "pay-key-0003");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("order_not_payable", (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Failed_payment_can_be_retried_with_a_new_gateway_orderAsync()
    {
        await using var fixture = await StartAsync();
        using var client = await fixture.ClientAsync("Retailer", "retailer-user", RetailerId);
        var orderId = (await PlaceOrderAsync(client)).GetProperty("id").GetInt64();
        var first = (await JsonAsync(await StartPaymentAsync(client, orderId, "pay-key-1001"))).GetProperty("gatewayOrderId").GetString()!;
        var failed = await JsonAsync(await client.PostAsync($"/api/v1/payments/{first}/dev-complete?success=false", null));
        Assert.Equal("Failed", failed.GetProperty("status").GetString());
        Assert.Equal("Pending", (await client.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}")).GetProperty("paymentStatus").GetString());
        var second = (await JsonAsync(await StartPaymentAsync(client, orderId, "pay-key-1002"))).GetProperty("gatewayOrderId").GetString()!;
        Assert.NotEqual(first, second);
        Assert.Equal("Paid", (await JsonAsync(await client.PostAsync($"/api/v1/payments/{second}/dev-complete", null))).GetProperty("status").GetString());
        // A late failure for the first attempt never downgrades a paid order.
        Assert.Equal("Paid", (await client.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}")).GetProperty("paymentStatus").GetString());
    }

    [Fact]
    public async Task Webhook_verifies_signature_amount_and_is_idempotentAsync()
    {
        await using var fixture = await StartAsync();
        using var client = await fixture.ClientAsync("Retailer", "retailer-user", RetailerId);
        var order = await PlaceOrderAsync(client);
        var orderId = order.GetProperty("id").GetInt64();
        var paise = (long)(order.GetProperty("grandTotal").GetDecimal() * 100);
        var gatewayOrderId = (await JsonAsync(await StartPaymentAsync(client, orderId, "pay-key-2001"))).GetProperty("gatewayOrderId").GetString()!;
        using var anonymous = await fixture.ClientAsync(null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await WebhookAsync(anonymous, Captured(gatewayOrderId, paise), "wrong-secret")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await WebhookAsync(anonymous, Captured(gatewayOrderId, paise), null)).StatusCode);
        var mismatch = await WebhookAsync(anonymous, Captured(gatewayOrderId, paise - 100));
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        Assert.Equal("payment_amount_mismatch", (await mismatch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal("Pending", (await client.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}")).GetProperty("paymentStatus").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await WebhookAsync(anonymous, Captured("unknown_order", paise))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync(anonymous, Captured(gatewayOrderId, paise, "refund.created"))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync(anonymous, Captured(gatewayOrderId, paise))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync(anonymous, Captured(gatewayOrderId, paise))).StatusCode); // gateway retry
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync(anonymous, Captured(gatewayOrderId, paise, "payment.failed"))).StatusCode); // late failure
        Assert.Equal("Paid", (await client.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}")).GetProperty("paymentStatus").GetString());
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        Assert.Equal(1, await db.Notifications.CountAsync(x => x.UserId == "seller-user" && x.Title == "New order"));
        Assert.Equal("Paid", (await db.Payments.SingleAsync(x => x.GatewayOrderId == gatewayOrderId)).Status);
    }

    [Fact]
    public async Task Payments_are_isolated_by_business_and_retailer_onlyAsync()
    {
        await using var fixture = await StartAsync();
        using var owner = await fixture.ClientAsync("Retailer", "retailer-user", RetailerId);
        var orderId = (await PlaceOrderAsync(owner)).GetProperty("id").GetInt64();
        var gatewayOrderId = (await JsonAsync(await StartPaymentAsync(owner, orderId, "pay-key-3001"))).GetProperty("gatewayOrderId").GetString()!;
        using var other = await fixture.ClientAsync("Retailer", "other", 31);
        Assert.Equal(HttpStatusCode.NotFound, (await StartPaymentAsync(other, orderId, "pay-key-3002")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/payments/{gatewayOrderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/v1/payments/{gatewayOrderId}/dev-complete", null)).StatusCode);
        using var seller = await fixture.ClientAsync("Manufacturer", "seller-user", 10);
        Assert.Equal(HttpStatusCode.Forbidden, (await StartPaymentAsync(seller, orderId, "pay-key-3003")).StatusCode);
        using var anonymous = await fixture.ClientAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await StartPaymentAsync(anonymous, orderId, "pay-key-3004")).StatusCode);
        Assert.Equal("Pending", (await owner.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}")).GetProperty("paymentStatus").GetString());
    }

    private const long RetailerId = 30;
}
