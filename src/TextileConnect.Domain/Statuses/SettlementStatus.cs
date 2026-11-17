namespace TextileConnect.Domain;

/// <summary>Payout to the seller: held (Pending) until it is Released, or Cancelled when the order is cancelled.</summary>
public static class SettlementStatus
{
    public const string Pending = "Pending";
    public const string Released = "Released";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All = [Pending, Released, Cancelled];
}
