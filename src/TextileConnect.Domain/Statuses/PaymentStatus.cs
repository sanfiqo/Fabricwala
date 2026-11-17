namespace TextileConnect.Domain;

/// <summary>Payment status, used by Order.PaymentStatus and Payment.Status. Refunded and PartiallyRefunded only occur on orders.</summary>
public static class PaymentStatus
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
    public const string Refunded = "Refunded";
    public const string PartiallyRefunded = "PartiallyRefunded";

    public static readonly IReadOnlyList<string> All = [Pending, Paid, Failed, Cancelled, Refunded, PartiallyRefunded];
}
