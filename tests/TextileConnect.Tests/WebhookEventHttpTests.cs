using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Tests;

public sealed class WebhookEventHttpTests
{
    private const long RetailerId = 30;

    private static Task<HttpResponseMessage> WebhookAsync(HttpClient client, string payload, string? eventId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook") { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("development-test-secret"));
        request.Headers.Add("X-Razorpay-Signature", Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant());
        if (eventId is not null) request.Headers.Add("X-Razorpay-Event-Id", eventId);
        return client.SendAsync(request);
    }

    private static string Captured(string gatewayOrderId, long paise)
        => JsonSerializer.Serialize(new { @event = "payment.captured", payload = new { payment = new { entity = new { id = "pay_" + gatewayOrderId, order_id = gatewayOrderId, amount = paise } } } });

    private static async Task<(long OrderId, string GatewayOrderId, long Paise)> NewOrderAsync(HttpClient client, string key)
    {
        var order = await PaymentHttpTests.PlaceOrderAsync(client);
        var orderId = order.GetProperty("id").GetInt64();
        var gatewayOrderId = (await PaymentHttpTests.JsonAsync(await PaymentHttpTests.StartPaymentAsync(client, orderId, key))).GetProperty("gatewayOrderId").GetString()!;
        return (orderId, gatewayOrderId, (long)(order.GetProperty("grandTotal").GetDecimal() * 100));
    }

    private static async Task<string> PaymentStatusAsync(HttpClient client, long orderId)
        => (await client.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}")).GetProperty("paymentStatus").GetString()!;

    [Fact]
    public async Task Replaying_an_event_id_returns_200_and_has_no_second_effectAsync()
    {
        await using var fixture = await PaymentHttpTests.StartAsync();
        using var client = await fixture.ClientAsync("Retailer", "retailer-user", RetailerId);
        using var gateway = await fixture.ClientAsync(null);
        var first = await NewOrderAsync(client, "evt-pay-1");
        var second = await NewOrderAsync(client, "evt-pay-2");

        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync(gateway, Captured(first.GatewayOrderId, first.Paise), "evt_100")).StatusCode);
        Assert.Equal("Paid", await PaymentStatusAsync(client, first.OrderId));

        // The same event id again, even with a body that would pay the other order: it is acknowledged and ignored.
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync(gateway, Captured(second.GatewayOrderId, second.Paise), "evt_100")).StatusCode);
        Assert.Equal("Pending", await PaymentStatusAsync(client, second.OrderId));

        // A new event id is handled normally.
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync(gateway, Captured(second.GatewayOrderId, second.Paise), "evt_101")).StatusCode);
        Assert.Equal("Paid", await PaymentStatusAsync(client, second.OrderId));

        using var scope = fixture.Services.CreateScope();
        var events = await scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>().PaymentWebhookEvents.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(new[] { "evt_100", "evt_101" }, events.Select(x => x.EventId));
        Assert.All(events, x => { Assert.Equal("Razorpay", x.Gateway); Assert.Equal("payment.captured", x.EventType); Assert.NotNull(x.ProcessedAtUtc); });
    }

    [Fact]
    public async Task An_event_that_could_not_be_applied_is_handled_when_the_gateway_sends_it_againAsync()
    {
        await using var fixture = await PaymentHttpTests.StartAsync();
        using var client = await fixture.ClientAsync("Retailer", "retailer-user", RetailerId);
        using var gateway = await fixture.ClientAsync(null);
        var order = await NewOrderAsync(client, "evt-pay-3");

        // The amount is wrong, so nothing is applied and the event stays open.
        Assert.Equal(HttpStatusCode.BadRequest, (await WebhookAsync(gateway, Captured(order.GatewayOrderId, order.Paise - 100), "evt_200")).StatusCode);
        Assert.Equal("Pending", await PaymentStatusAsync(client, order.OrderId));
        using (var scope = fixture.Services.CreateScope())
            Assert.Null((await scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>().PaymentWebhookEvents.SingleAsync(x => x.EventId == "evt_200")).ProcessedAtUtc);

        // Same event id, correct amount: it is handled and then closed.
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync(gateway, Captured(order.GatewayOrderId, order.Paise), "evt_200")).StatusCode);
        Assert.Equal("Paid", await PaymentStatusAsync(client, order.OrderId));
        using (var scope = fixture.Services.CreateScope())
            Assert.NotNull((await scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>().PaymentWebhookEvents.SingleAsync(x => x.EventId == "evt_200")).ProcessedAtUtc);
    }

    /// <summary>Called from AuditTrailTests: a webhook has no signed-in user, so its audit row says "anonymous" and keeps the IP of the caller.</summary>
    internal static async Task PaidByWebhookAndAuditedAsync()
    {
        await using var fixture = await PaymentHttpTests.StartAsync();
        using var client = await fixture.ClientAsync("Retailer", "retailer-user", RetailerId);
        using var gateway = await fixture.ClientAsync(null);
        var order = await NewOrderAsync(client, "audit-pay-1");
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync(gateway, Captured(order.GatewayOrderId, order.Paise), "evt_audit_1")).StatusCode);

        using var scope = fixture.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>().AuditLogs.SingleAsync(x => x.Action == "payment.paid");
        Assert.Equal("anonymous", row.UserId);
        Assert.True(System.Net.IPAddress.TryParse(row.IpAddress, out _));
        Assert.Contains($"order {order.OrderId}", row.Details);
    }

    [Fact]
    public async Task Without_an_event_id_header_an_identical_body_counts_as_the_same_deliveryAsync()
    {
        await using var fixture = await PaymentHttpTests.StartAsync();
        using var client = await fixture.ClientAsync("Retailer", "retailer-user", RetailerId);
        using var gateway = await fixture.ClientAsync(null);
        var order = await NewOrderAsync(client, "evt-pay-4");
        var body = Captured(order.GatewayOrderId, order.Paise);

        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync(gateway, body, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync(gateway, body, null)).StatusCode);
        using var scope = fixture.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>().PaymentWebhookEvents.SingleAsync();
        Assert.StartsWith("sha256:", row.EventId);
    }
}
