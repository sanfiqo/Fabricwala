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
        var keyId = Setting("KeyId");
        var keySecret = Setting("KeySecret");
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        using var response = await httpClient.PostAsJsonAsync("https://api.razorpay.com/v1/orders", new { amount = (long)(amount * 100), currency = "INR", receipt }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var order = await response.Content.ReadFromJsonAsync<RazorpayOrder>(cancellationToken: cancellationToken) ?? throw new InvalidOperationException("Razorpay returned an empty order.");
        return new PaymentOrder(order.Id, amount);
    }

    public async Task<string> RefundAsync(string gatewayPaymentId, decimal amount, string receipt, CancellationToken cancellationToken = default)
    {
        var keyId = Setting("KeyId");
        var keySecret = Setting("KeySecret");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.razorpay.com/v1/payments/{Uri.EscapeDataString(gatewayPaymentId)}/refund")
        { Content = JsonContent.Create(new { amount = (long)decimal.Round(amount * 100, 0), receipt }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}")));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var refund = await response.Content.ReadFromJsonAsync<RazorpayOrder>(cancellationToken: cancellationToken) ?? throw new InvalidOperationException("Razorpay returned an empty refund.");
        return refund.Id;
    }

    public bool VerifyWebhookSignature(string payload, string signature)
        => RazorpaySignatureVerifier.Verify(payload, signature, Setting("WebhookSecret"));

    /// <summary>The documented name is Payments:Razorpay:Name (docs/SECRETS.md); the older Razorpay:Name is still accepted.</summary>
    public static string? Find(IConfiguration configuration, string name) => configuration[$"Payments:Razorpay:{name}"] ?? configuration[$"Razorpay:{name}"];

    private string Setting(string name) => Find(configuration, name) ?? throw new InvalidOperationException($"Payments:Razorpay:{name} is required.");

    private sealed record RazorpayOrder(string Id);
}
