using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TextileConnect.Application.Payments;

namespace TextileConnect.Infrastructure.Payments;

/// <summary>Raised when Razorpay refuses a call or cannot be reached. The message holds Razorpay's own reason, never a key.</summary>
public sealed class PaymentGatewayException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class RazorpayPaymentGateway(HttpClient httpClient, IConfiguration configuration) : IPaymentGateway
{
    private const string BaseUrl = "https://api.razorpay.com/v1/";

    public async Task<PaymentOrder> CreateOrderAsync(decimal amount, string receipt, CancellationToken cancellationToken = default)
    {
        // Razorpay allows a receipt of at most 40 characters.
        var order = await PostAsync("orders", new { amount = Paise(amount), currency = "INR", receipt = receipt.Length > 40 ? receipt[..40] : receipt }, cancellationToken);
        return new PaymentOrder(order, amount);
    }

    public Task<string> RefundAsync(string gatewayPaymentId, decimal amount, string receipt, CancellationToken cancellationToken = default)
        => PostAsync($"payments/{Uri.EscapeDataString(gatewayPaymentId)}/refund", new { amount = Paise(amount), receipt = receipt.Length > 40 ? receipt[..40] : receipt }, cancellationToken);

    public bool VerifyWebhookSignature(string payload, string signature)
        => RazorpaySignatureVerifier.Verify(payload, signature, Setting("WebhookSecret"));

    /// <summary>Sends one JSON call with this request's own credentials (the shared client keeps no header) and returns the id of the created object.</summary>
    private async Task<string> PostAsync(string path, object body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Setting("KeyId")}:{Setting("KeySecret")}")));
        HttpResponseMessage response;
        try { response = await httpClient.SendAsync(request, cancellationToken); }
        catch (HttpRequestException ex) { throw new PaymentGatewayException("Razorpay could not be reached.", ex); }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested) { throw new PaymentGatewayException("Razorpay did not answer in time.", ex); }
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode) throw new PaymentGatewayException($"Razorpay refused the call ({(int)response.StatusCode}): {ErrorDescription(text)}");
            try
            {
                using var json = JsonDocument.Parse(text);
                return json.RootElement.GetProperty("id").GetString() ?? throw new PaymentGatewayException("Razorpay answered without an id.");
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                throw new PaymentGatewayException("Razorpay answered with something unexpected.", ex);
            }
        }
    }

    private static string ErrorDescription(string body)
    {
        try { return JsonDocument.Parse(body).RootElement.GetProperty("error").GetProperty("description").GetString() ?? "no reason given"; }
        catch (Exception) { return "no reason given"; }
    }

    /// <summary>The documented name is Payments:Razorpay:Name (docs/SECRETS.md); the older Razorpay:Name is still accepted.</summary>
    public static string? Find(IConfiguration configuration, string name) => configuration[$"Payments:Razorpay:{name}"] ?? configuration[$"Razorpay:{name}"];

    /// <summary>Rupees as the whole paise Razorpay wants, rounded like every other amount in the app (half away from zero).</summary>
    public static long Paise(decimal rupees) => (long)decimal.Round(rupees * 100m, 0, MidpointRounding.AwayFromZero);

    private string Setting(string name) => Find(configuration, name) ?? throw new InvalidOperationException($"Payments:Razorpay:{name} is required.");
}
