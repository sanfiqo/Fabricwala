using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Configuration;
using TextileConnect.Application.Payments;

namespace TextileConnect.Infrastructure.Payments;

public sealed class RazorpayPaymentGateway(HttpClient httpClient, IConfiguration configuration) : IPaymentGateway
{
    public async Task<PaymentOrder> CreateOrderAsync(decimal amount, string receipt, CancellationToken cancellationToken = default)
    {
        var keyId = configuration["Razorpay:KeyId"] ?? throw new InvalidOperationException("Razorpay:KeyId is required.");
        var keySecret = configuration["Razorpay:KeySecret"] ?? throw new InvalidOperationException("Razorpay:KeySecret is required.");
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        using var response = await httpClient.PostAsJsonAsync("https://api.razorpay.com/v1/orders", new { amount = (long)(amount * 100), currency = "INR", receipt }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<RazorpayOrder>(cancellationToken: cancellationToken) ?? throw new InvalidOperationException("Razorpay returned an empty order.");
        return new PaymentOrder(order.Id, amount);
    }

    public bool VerifyWebhookSignature(string payload, string signature)
        => RazorpaySignatureVerifier.Verify(payload, signature, configuration["Razorpay:WebhookSecret"] ?? throw new InvalidOperationException("Razorpay:WebhookSecret is required."));

    private sealed record RazorpayOrder(string Id);
}
