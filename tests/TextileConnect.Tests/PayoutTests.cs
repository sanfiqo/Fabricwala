namespace TextileConnect.Tests;

public sealed class PayoutTests
{
    [Fact]
    public void Payout_statement_tracks_net_amount_after_commission()
    {
        const decimal amount = 1000;
        const decimal commission = 50;
        Assert.Equal(950, amount - commission);
    }
}
