namespace TextileConnect.Api;

/// <summary>One row of the retailer's orders list. Group is active, delivered or problems.</summary>
public sealed record RetailerOrderSummaryResponse(long Id, string OrderNumber, DateTime PlacedAt, decimal GrandTotal, string PaymentStatus,
    string Status, string Group, int ItemCount, int TotalQuantity, string FirstItemTitle, IReadOnlyList<string> SellerNames);
