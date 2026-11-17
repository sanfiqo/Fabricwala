using TextileConnect.Domain;
namespace TextileConnect.Api;

/// <summary>Retailer-facing order status and list group derived from payment, seller order statuses and open claims.</summary>
public static class OrderStatusRules
{
    public const string Active = "active";
    public const string Delivered = "delivered";
    public const string Problems = "problems";

    public static (string Status, string Group) Derive(string paymentStatus, IReadOnlyCollection<string> sellerStatuses, bool hasOpenClaim)
    {
        bool Dead(string s) => SellerOrderStatus.IsDead(s);
        if (sellerStatuses.Count > 0 && sellerStatuses.All(Dead)) return ("Cancelled", Problems);
        if (sellerStatuses.Any(Dead)) return ("PartiallyCancelled", Problems);
        if (hasOpenClaim) return ("ProblemReported", Problems);
        if (!string.Equals(paymentStatus, PaymentStatus.Paid, StringComparison.OrdinalIgnoreCase)) return ("AwaitingPayment", Active);
        if (sellerStatuses.Count > 0 && sellerStatuses.All(s => s == SellerOrderStatus.Delivered)) return ("Delivered", Delivered);
        var status = sellerStatuses.Any(s => s == SellerOrderStatus.Pending) ? "WaitingForSeller"
            : sellerStatuses.Any(s => s == SellerOrderStatus.Accepted) ? "Packing" : "Shipped";
        return (status, Active);
    }
}
