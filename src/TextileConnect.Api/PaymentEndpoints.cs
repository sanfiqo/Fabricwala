using System.Security.Claims;
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
    }
    public sealed record PaymentRequest(long OrderId);
}
