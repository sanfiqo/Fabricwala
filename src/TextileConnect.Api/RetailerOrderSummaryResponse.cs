namespace TextileConnect.Api;

/// <summary>One row of the retailer's orders list. Group is active, delivered or problems. Items are the order lines (same shape as in the order detail), so the list needs no second request.</summary>
public sealed record RetailerOrderSummaryResponse(long Id, string OrderNumber, DateTime PlacedAt, decimal GrandTotal, string PaymentStatus,
    string Status, string Group, int ItemCount, int TotalQuantity, string FirstItemTitle, IReadOnlyList<string> SellerNames, IReadOnlyList<OrderItemResponse> Items);
