namespace TextileConnect.Domain;

/// <summary>
/// The only place that says which status a seller order may move to. Who triggers each move:
/// Pending to Accepted, Rejected or Dispatched-after-Accepted by the seller; Pending to Cancelled by the system (not accepted in time, not paid in time);
/// Dispatched to Delivered only by the retailer, never by the seller. Rejected, Cancelled and Delivered are final.
/// </summary>
public static class SellerOrderTransitions
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Allowed = new Dictionary<string, IReadOnlyList<string>>
    {
        [SellerOrderStatus.Pending] = [SellerOrderStatus.Accepted, SellerOrderStatus.Rejected, SellerOrderStatus.Cancelled],
        [SellerOrderStatus.Accepted] = [SellerOrderStatus.Dispatched],
        [SellerOrderStatus.Dispatched] = [SellerOrderStatus.Delivered],
        [SellerOrderStatus.Rejected] = [],
        [SellerOrderStatus.Cancelled] = [],
        [SellerOrderStatus.Delivered] = []
    };

    public static IReadOnlyList<string> From(string status) => Allowed.TryGetValue(status, out var next) ? next : [];

    public static bool CanMove(string from, string to) => From(from).Contains(to);
}
