using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using TextileConnect.Infrastructure.Payments;

namespace TextileConnect.Tests;

/// <summary>The Razorpay client against a fake server: what it sends, and what it does with each kind of answer. Nothing leaves the machine.</summary>
public sealed class RazorpayGatewayTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, string, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((request, body));
            return reply(request, body);
        }
    }

    private static RazorpayPaymentGateway Gateway(FakeHandler handler, bool withKeys = true)
    {
        var values = new Dictionary<string, string?>();
        if (withKeys) { values["Payments:Razorpay:KeyId"] = "rzp_test_key"; values["Payments:Razorpay:KeySecret"] = "the-secret"; values["Payments:Razorpay:WebhookSecret"] = "hook"; }
        return new RazorpayPaymentGateway(new HttpClient(handler), new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task An_order_is_sent_in_paise_with_this_calls_own_credentials()
    {
        var handler = new FakeHandler((_, _) => Json(HttpStatusCode.OK, "{\"id\":\"order_ABC\"}"));
        var order = await Gateway(handler).CreateOrderAsync(1234.565m, "TC-20261117-0001");

        Assert.Equal("order_ABC", order.GatewayOrderId);
        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal("https://api.razorpay.com/v1/orders", request.RequestUri!.ToString());
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("rzp_test_key:the-secret")), request.Headers.Authorization!.ToString());
        Assert.Contains("\"amount\":123457", body);                      // 1234.565 rupees, half away from zero
        Assert.Contains("\"currency\":\"INR\"", body);
    }

    [Fact]
    public async Task A_receipt_longer_than_forty_characters_is_cut_because_razorpay_refuses_it()
    {
        var handler = new FakeHandler((_, _) => Json(HttpStatusCode.OK, "{\"id\":\"order_ABC\"}"));
        await Gateway(handler).CreateOrderAsync(10m, new string('r', 60));
        Assert.Contains($"\"receipt\":\"{new string('r', 40)}\"", handler.Calls[0].Body);
    }

    [Fact]
    public async Task A_refund_goes_to_the_payment_and_returns_the_refund_id()
    {
        var handler = new FakeHandler((_, _) => Json(HttpStatusCode.OK, "{\"id\":\"rfnd_XYZ\"}"));
        var id = await Gateway(handler).RefundAsync("pay_123", 50m, "refund-7");

        Assert.Equal("rfnd_XYZ", id);
        Assert.Equal("https://api.razorpay.com/v1/payments/pay_123/refund", handler.Calls[0].Request.RequestUri!.ToString());
        Assert.Contains("\"amount\":5000", handler.Calls[0].Body);
    }

    [Fact]
    public async Task A_refusal_carries_razorpays_reason_and_never_the_key()
    {
        var handler = new FakeHandler((_, _) => Json(HttpStatusCode.BadRequest, "{\"error\":{\"code\":\"BAD_REQUEST_ERROR\",\"description\":\"The amount must be at least INR 1.00\"}}"));
        var error = await Assert.ThrowsAsync<PaymentGatewayException>(() => Gateway(handler).CreateOrderAsync(0.5m, "r"));

        Assert.Contains("400", error.Message);
        Assert.Contains("The amount must be at least INR 1.00", error.Message);
        Assert.DoesNotContain("the-secret", error.Message + error.StackTrace);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"nothing\":1}")]
    [InlineData("")]
    public async Task An_answer_without_an_id_is_a_gateway_error_not_a_crash(string body)
    {
        var handler = new FakeHandler((_, _) => Json(HttpStatusCode.OK, body));
        await Assert.ThrowsAsync<PaymentGatewayException>(() => Gateway(handler).CreateOrderAsync(10m, "r"));
    }

    [Fact]
    public async Task A_network_failure_is_a_gateway_error()
    {
        var handler = new FakeHandler((_, _) => throw new HttpRequestException("no route"));
        await Assert.ThrowsAsync<PaymentGatewayException>(() => Gateway(handler).RefundAsync("pay_1", 10m, "r"));
    }

    [Fact]
    public async Task Missing_keys_stop_the_call_before_anything_is_sent()
    {
        var handler = new FakeHandler((_, _) => Json(HttpStatusCode.OK, "{\"id\":\"x\"}"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Gateway(handler, withKeys: false).CreateOrderAsync(10m, "r"));
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Many_calls_at_once_do_not_share_or_mix_credentials()
    {
        var handler = new FakeHandler((_, _) => Json(HttpStatusCode.OK, "{\"id\":\"order_1\"}"));
        var gateway = Gateway(handler);
        await Task.WhenAll(Enumerable.Range(0, 20).Select(i => gateway.CreateOrderAsync(10m + i, $"r{i}")));
        Assert.Equal(20, handler.Calls.Count);
        Assert.All(handler.Calls, call => Assert.NotNull(call.Request.Headers.Authorization));
    }

    /// <summary>
    /// A real call to Razorpay in test mode. It only runs when RAZORPAY_LIVE_TEST=1 and the test keys are in user-secrets
    /// (Payments:Razorpay:KeyId / KeySecret, key id must start with rzp_test_); it never runs with a live key. Creates one order of Rs 1; moves no money.
    /// </summary>
    [Fact]
    public async Task Live_test_mode_creates_a_real_order()
    {
        if (Environment.GetEnvironmentVariable("RAZORPAY_LIVE_TEST") != "1") return;
        var configuration = new ConfigurationBuilder().AddUserSecrets(typeof(TextileConnect.Api.CsvCell).Assembly).AddEnvironmentVariables().Build();
        var keyId = RazorpayPaymentGateway.Find(configuration, "KeyId");
        Assert.StartsWith("rzp_test_", keyId);
        var order = await new RazorpayPaymentGateway(new HttpClient(), configuration).CreateOrderAsync(1m, "tc-live-smoke");
        Assert.StartsWith("order_", order.GatewayOrderId);
    }
}
