using System.Security.Cryptography;
using System.Text;
using TextileConnect.Application.Payments;

namespace TextileConnect.Tests;

public sealed class PaymentsTests
{
    [Fact]
    public void Razorpay_webhook_signature_uses_hmac_and_rejects_tampering()
    {
        const string payload = "{\"event\":\"payment.captured\"}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("test-secret"));
        var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        Assert.True(RazorpaySignatureVerifier.Verify(payload, signature, "test-secret"));
        Assert.False(RazorpaySignatureVerifier.Verify(payload + "x", signature, "test-secret"));
    }
}
