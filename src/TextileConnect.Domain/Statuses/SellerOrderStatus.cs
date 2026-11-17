namespace TextileConnect.Domain;

/// <summary>Status of one seller order (and of the rows in its status history). The text is stored in the database and sent in the API, so it must not change.</summary>
public static class SellerOrderStatus
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";
    public const string Dispatched = "Dispatched";
    public const string Delivered = "Delivered";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All = [Pending, Accepted, Rejected, Dispatched, Delivered, Cancelled];

    /// <summary>Rejected and cancelled seller orders will never be shipped.</summary>
    public static bool IsDead(string status) => status is Rejected or Cancelled;
}
