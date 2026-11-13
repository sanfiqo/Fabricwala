using Microsoft.EntityFrameworkCore;
using TextileConnect.Infrastructure.Persistence;

namespace TextileConnect.Api;

public static class AdminReportEndpoints
{
    public static void MapAdminReportEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/admin/reports/{kind}", async (string kind, MarketplaceDbContext db, CancellationToken ct) =>
        {
            string body = kind.ToLowerInvariant() switch
            {
                "orders" => "id,orderNumber,retailerBusinessId,grandTotal,paymentStatus\n" + string.Join("\n", await db.Orders.AsNoTracking().Select(x => $"{x.Id},{x.OrderNumber},{x.RetailerBusinessId},{x.GrandTotal:0.00},{x.PaymentStatus}").ToListAsync(ct)),
                "payments" => "id,orderId,amount,status,gatewayPaymentId\n" + string.Join("\n", await db.Payments.AsNoTracking().Select(x => $"{x.Id},{x.OrderId},{x.Amount:0.00},{x.Status},{x.GatewayPaymentId}").ToListAsync(ct)),
                "settlements" => "id,sellerOrderId,manufacturerBusinessId,amount,commission,status,settledAt\n" + string.Join("\n", await db.Settlements.AsNoTracking().Select(x => $"{x.Id},{x.SellerOrderId},{x.ManufacturerBusinessId},{x.Amount:0.00},{x.CommissionAmount:0.00},{x.Status},{x.SettledAt:O}").ToListAsync(ct)),
                "businesses" => "id,displayName,businessType,verificationStatus,phone\n" + string.Join("\n", await db.Businesses.AsNoTracking().Select(x => $"{x.Id},{x.DisplayName},{x.BusinessType},{x.VerificationStatus},{x.Phone}").ToListAsync(ct)),
                _ => string.Empty
            };
            return string.IsNullOrEmpty(body) ? Results.NotFound() : Results.Text(body, "text/csv", System.Text.Encoding.UTF8);
        }).RequireAuthorization(p => p.RequireRole("Admin"));
    }
}
