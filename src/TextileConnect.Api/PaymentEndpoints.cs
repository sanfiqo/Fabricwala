using System.Text.Json;
using TextileConnect.Application.Businesses;
using TextileConnect.Application.Payments;

namespace TextileConnect.Api;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var retailer = app.MapGroup("/api/v1/payments").RequireAuthorization("ApprovedBusiness").RequireAuthorization(policy => policy.RequireRole("Retailer"));
        retailer.MapPost("", async (PaymentRequest request, HttpRequest http, ICurrentBusiness current, IPaymentService payments, CancellationToken ct) =>
        {
            var session = await payments.StartAsync(current.RequireBusinessId(), request.OrderId, http.Headers["Idempotency-Key"].ToString(), ct);
            return Results.Ok(PaymentSessionResponse.From(session, app.Configuration["Razorpay:KeyId"] ?? "dev_key"));
        });
        retailer.MapGet("/{gatewayOrderId}", async (string gatewayOrderId, ICurrentBusiness current, IPaymentService payments, CancellationToken ct) =>
            await payments.GetAsync(current.RequireBusinessId(), gatewayOrderId, ct) is { } session
                ? Results.Ok(PaymentSessionResponse.From(session, app.Configuration["Razorpay:KeyId"] ?? "dev_key")) : Results.NotFound());
        // Test gateway only: lets the frontend finish a payment locally. A real gateway has no such route (404).
        retailer.MapPost("/{gatewayOrderId}/dev-complete", async (string gatewayOrderId, bool? success, ICurrentBusiness current, IPaymentService payments, IPaymentGateway gateway, CancellationToken ct) =>
        {
            if (gateway is not DevelopmentPaymentGateway) return Results.NotFound();
            if (await payments.GetAsync(current.RequireBusinessId(), gatewayOrderId, ct) is null) return Results.NotFound();
            if (success == false) await payments.FailAsync(gatewayOrderId, ct);
            else await payments.ConfirmAsync(gatewayOrderId, $"dev_pay_{Guid.NewGuid():N}", null, ct);
            return Results.Ok(PaymentSessionResponse.From((await payments.GetAsync(current.RequireBusinessId(), gatewayOrderId, ct))!, "dev_key"));
        });

        app.MapPost("/api/v1/payments/webhook", async (HttpRequest request, IPaymentGateway gateway, IPaymentService payments, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var payload = await reader.ReadToEndAsync(ct);
            if (!gateway.VerifyWebhookSignature(payload, request.Headers["X-Razorpay-Signature"].ToString())) return Results.Unauthorized();
            using var json = JsonDocument.Parse(payload);
            var eventName = json.RootElement.TryGetProperty("event", out var e) ? e.GetString() : "payment.captured";
            var entity = json.RootElement.GetProperty("payload").GetProperty("payment").GetProperty("entity");
            var gatewayOrderId = entity.GetProperty("order_id").GetString() ?? string.Empty;
            bool known;
            if (eventName == "payment.failed") known = await payments.FailAsync(gatewayOrderId, ct);
            else if (eventName is "payment.captured" or "payment.authorized" or "order.paid")
            {
                var paise = entity.TryGetProperty("amount", out var amount) && amount.TryGetInt64(out var value) ? value : (long?)null;
                known = await payments.ConfirmAsync(gatewayOrderId, entity.GetProperty("id").GetString() ?? string.Empty, paise / 100m, ct);
            }
            else return Results.Ok(); // other events are acknowledged and ignored
            return known ? Results.Ok() : Results.NotFound();
        });
    }

    public sealed record PaymentRequest(long OrderId);
}
