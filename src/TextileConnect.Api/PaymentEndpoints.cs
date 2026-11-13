using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Payments;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/payments", async (PaymentRequest request, ClaimsPrincipal principal, ICurrentBusiness current, MarketplaceDbContext db, IPaymentGateway gateway, CancellationToken ct) =>
        {
            var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == request.OrderId && x.RetailerBusinessId == current.RequireBusinessId(), ct);
            if (order is null) return Results.NotFound();
            var payment = await gateway.CreateOrderAsync(order.GrandTotal, order.OrderNumber, ct);
            db.Payments.Add(new Domain.Payments.Payment { OrderId = order.Id, GatewayOrderId = payment.GatewayOrderId, Amount = payment.Amount, Status = "Pending" });
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { orderId = order.Id, gatewayOrderId = payment.GatewayOrderId, amount = payment.Amount, currency = "INR", keyId = app.Configuration["Razorpay:KeyId"] ?? "dev_key" });
        }).RequireAuthorization("ApprovedBusiness");
        app.MapPost("/api/v1/payments/webhook", async (HttpRequest request, MarketplaceDbContext db, IPaymentGateway gateway, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var payload = await reader.ReadToEndAsync(ct);
            var signature = request.Headers["X-Razorpay-Signature"].ToString();
            if (!gateway.VerifyWebhookSignature(payload, signature)) return Results.Unauthorized();
            using var json = JsonDocument.Parse(payload);
            var entity = json.RootElement.GetProperty("payload").GetProperty("payment").GetProperty("entity");
            var gatewayOrderId = entity.GetProperty("order_id").GetString();
            var gatewayPaymentId = entity.GetProperty("id").GetString();
            var payment = await db.Payments.SingleOrDefaultAsync(x => x.GatewayOrderId == gatewayOrderId, ct);
            if (payment is null) return Results.NotFound();
            if (!string.Equals(payment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                payment.Status = "Paid"; payment.GatewayPaymentId = gatewayPaymentId; payment.PaidAt = DateTime.UtcNow;
                var order = await db.Orders.SingleAsync(x => x.Id == payment.OrderId, ct);
                order.PaymentStatus = "Paid";
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok();
        });
    }
    public sealed record PaymentRequest(long OrderId);
}
